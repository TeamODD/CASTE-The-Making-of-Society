using System;
using System.Collections.Generic;
using System.IO;

internal sealed class TestSessionData
{
    public string CsvHeader { get; }
    public CardDatabase Database { get; }
    public CardData DemoCard { get; }

    /// <summary>
    /// 테스트 씬의 CSV와 매니저 상태를 확인하고 수동 검사에 사용할 카드를 찾습니다.
    /// </summary>
    public TestSessionData(string csvText, bool managerDisabled, string demoVariantId)
    {
        Require(csvText != null, "Test의 CSV 파일을 연결해 주세요.");
        Require(managerDisabled, "실제 세이브 보호를 위해 이 씬의 GameManager는 비활성화해야 합니다.");

        CsvHeader = csvText.Split('\n')[0].TrimEnd('\r');
        Database = new CardDatabase(csvText);
        DemoCard = Database.FindByVariantId(demoVariantId);
        Require(DemoCard != null, $"테스트 카드 ID를 찾을 수 없습니다: {demoVariantId}");
    }

    /// <summary>
    /// 테스트용 카드 데이터와 지정한 저장소 및 턴 한도로 세션을 만듭니다.
    /// </summary>
    public GameSession CreateSession(ISaveStore store, int turnLimit = 12)
    {
        return new GameSession(Database, new CardSelector(), store, turnLimit);
    }

    /// <summary>
    /// 검사할 카드와 스탯에 맞춰 진행과 첫 체크포인트가 포함된 저장 데이터를 만듭니다.
    /// </summary>
    public SaveData CreateSaveFixture(CardData card, Stat stats, int turnLimit = 12)
    {
        CardData casteCard = card ?? Database.FindByVariantId("B-M01");
        var flags = new List<string>();
        if (!string.IsNullOrWhiteSpace(casteCard.requiredLegacyFlag) && casteCard.requiredLegacyFlag != "NONE")
            flags.Add(casteCard.requiredLegacyFlag);

        CardData firstCard = new CardSelector().PickNextCard(Database.GetAllCards(), casteCard.casteOrder,
            flags, new GameState(flags: flags), turnLimit);
        return new SaveData
        {
            schemaVersion = SaveData.CurrentSchemaVersion,
            checkpoints = new List<CheckpointData>
            {
                new CheckpointData
                {
                    casteCode = casteCard.casteCode,
                    casteOrder = casteCard.casteOrder,
                    stats = Stat.Default,
                    flags = new List<string>(flags),
                    firstVariantId = firstCard.variantId
                }
            },
            progress = new ProgressData
            {
                casteOrder = casteCard.casteOrder,
                turnsPerCaste = turnLimit,
                stats = stats,
                flags = flags,
                currentVariantId = card?.variantId,
                isFinished = card == null
            }
        };
    }

    /// <summary>
    /// 사건 유형, 계층, 유산 조건이 지정된 테스트용 CSV 행을 만듭니다.
    /// </summary>
    public static string CreateCsvRow(string id, string type, string casteCode, int casteOrder,
        string requiredFlag = "NONE", string variantId = null)
    {
        return string.Join(",", id, variantId ?? id, type, casteCode, casteCode, casteOrder, requiredFlag,
            "Test", "Speaker", "Dialogue", "Left", 0, 0, 0, 0, "", "Right", 0, 0, 0, 0, "");
    }

    /// <summary>
    /// 지정한 임시 경로에서 저장 데이터를 읽습니다.
    /// </summary>
    public static SaveData ReadTemporarySave(string path)
    {
        ISaveStore store = new SaveSystem(path);
        return store.LoadOrNull();
    }

    /// <summary>
    /// 지정한 임시 경로에 검사에 사용할 저장 데이터를 기록합니다.
    /// </summary>
    public static void WriteTemporarySave(SaveData data, string path)
    {
        ISaveStore store = new SaveSystem(path);
        store.Save(data);
    }

    /// <summary>
    /// 검사에 전용 임시 저장 경로를 넘기고 실행이 끝나면 파일과 디렉터리를 정리합니다.
    /// </summary>
    public static void WithTemporarySave(Action<string> check)
    {
        string path = CreateTemporarySavePath();
        try
        {
            check(path);
        }
        finally
        {
            RemoveTemporarySave(path);
        }
    }

    /// <summary>
    /// 테스트 저장 파일과 임시 디렉터리를 삭제합니다.
    /// </summary>
    public static void RemoveTemporarySave(string path)
    {
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
            return;

        File.Delete(path + ".tmp");
        File.Delete(path);
        Directory.Delete(directory);
    }

    /// <summary>
    /// 검사마다 고유한 임시 디렉터리를 만들고 그 안의 저장 파일 경로를 반환합니다.
    /// </summary>
    public static string CreateTemporarySavePath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CASTE-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "save.json");
    }

    /// <summary>
    /// 검사 조건이 거짓이면 지정한 메시지로 예외를 발생시킵니다.
    /// </summary>
    public static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

internal sealed class IndexedCardRandom : System.Random
{
    private readonly int index;
    private readonly int candidateCount;

    /// <summary>
    /// 추첨에 돌려줄 인덱스와 예상 후보 수를 지정합니다.
    /// </summary>
    public IndexedCardRandom(int index, int candidateCount)
    {
        this.index = index;
        this.candidateCount = candidateCount;
    }

    /// <summary>
    /// 추첨 시 요청된 후보 수를 검증하고 미리 지정한 인덱스를 반환합니다.
    /// </summary>
    public override int Next(int maxValue)
    {
        TestSessionData.Require(maxValue == candidateCount, "사건마다 추첨 기회가 한 번이어야 합니다.");
        return index;
    }
}

internal sealed class MemorySaveStore : ISaveStore
{
    public SaveData Saved { get; private set; }
    public bool HasSave => Saved != null;
    public bool FailWrites { get; set; }
    public bool FailReads { get; set; }
    public bool InvalidOnRead { get; set; }

    /// <summary>
    /// 메모리의 저장을 반환하거나 설정에 따라 읽기 실패 또는 손상 저장을 재현합니다.
    /// </summary>
    public SaveData LoadOrNull()
    {
        if (FailReads)
            throw new IOException("Read access failure for test.");
        if (InvalidOnRead)
            throw new InvalidDataException("Invalid save for test.");
        return Saved;
    }

    /// <summary>
    /// 진행을 메모리에 저장하거나 설정에 따라 쓰기 실패를 재현합니다.
    /// </summary>
    public void Save(SaveData data)
    {
        if (FailWrites)
            throw new IOException("Write failure for test.");
        Saved = data;
    }
}
