using System;
using System.Collections.Generic;
using System.IO;

internal sealed class TestSessionData
{
    public string CsvHeader { get; }
    public CardDatabase Database { get; }
    public CardData DemoCard { get; }

    public TestSessionData(string csvText, bool managerDisabled, string demoVariantId)
    {
        Require(csvText != null, "Test의 CSV 파일을 연결해 주세요.");
        Require(managerDisabled, "실제 세이브 보호를 위해 이 씬의 GameManager는 비활성화해야 합니다.");

        CsvHeader = csvText.Split('\n')[0].TrimEnd('\r');
        Database = new CardDatabase(csvText);
        DemoCard = Database.FindByVariantId(demoVariantId);
        Require(DemoCard != null, $"테스트 카드 ID를 찾을 수 없습니다: {demoVariantId}");
    }

    public GameSession CreateSession(ISaveStore store, int turnLimit = 12)
    {
        return new GameSession(Database, new CardSelector(), store, turnLimit);
    }

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

    public static string CreateCsvRow(string id, string type, string casteCode, int casteOrder,
        string requiredFlag = "NONE", string variantId = null)
    {
        return string.Join(",", id, variantId ?? id, type, casteCode, casteCode, casteOrder, requiredFlag,
            "Test", "Speaker", "Dialogue", "Left", 0, 0, 0, 0, "", "Right", 0, 0, 0, 0, "");
    }

    public static SaveData ReadTemporarySave(string path)
    {
        ISaveStore store = new SaveSystem(path);
        return store.LoadOrNull();
    }

    public static void WriteTemporarySave(SaveData data, string path)
    {
        ISaveStore store = new SaveSystem(path);
        store.Save(data);
    }

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

    public static void RemoveTemporarySave(string path)
    {
        string directory = Path.GetDirectoryName(path);
        if (!Directory.Exists(directory))
            return;

        File.Delete(path + ".tmp");
        File.Delete(path);
        Directory.Delete(directory);
    }

    public static string CreateTemporarySavePath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CASTE-Test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "save.json");
    }

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

    public IndexedCardRandom(int index, int candidateCount)
    {
        this.index = index;
        this.candidateCount = candidateCount;
    }

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

    public SaveData LoadOrNull()
    {
        if (FailReads)
            throw new IOException("Read access failure for test.");
        if (InvalidOnRead)
            throw new InvalidDataException("Invalid save for test.");
        return Saved;
    }

    public void Save(SaveData data)
    {
        if (FailWrites)
            throw new IOException("Write failure for test.");
        Saved = data;
    }
}
