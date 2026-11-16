using System;
using System.Collections.Generic;
using System.IO;
using static TestSessionData;

internal sealed class TestDemoSession : IDisposable
{
    private readonly TestSessionData data;
    private readonly SaveSystem store;
    private readonly EndingDatabase endings;
    private readonly EndingArchiveStore archive;
    private readonly Func<int> readTurnLimit;
    private readonly GameSession session;

    /// <summary>
    /// 수동 테스트 전용 저장 경로에 세션을 만들고 카드 변경 알림을 연결합니다.
    /// </summary>
    public TestDemoSession(TestSessionData data, Func<int> readTurnLimit, string persistentDataPath)
    {
        this.data = data;
        this.readTurnLimit = readTurnLimit;
        store = new SaveSystem(Path.Combine(persistentDataPath, "TestDemo", "save.json"));
        archive = new EndingArchiveStore(Path.Combine(Path.GetDirectoryName(store.SavePath), "archive.json"));
        endings = EndingDatabase.LoadDefault();
        session = data.CreateSession(store, endingArchive: archive, endings: endings);
        session.State.CurrentCardChanged += ForwardCardChange;
    }

    public string SavePath => store.SavePath;
    public bool HasSave => store.HasSave;
    public bool CanResume => GameSession.HasResumableSave(store);
    public string SaveBeforeAction { get; private set; }
    public string CurrentSave => HasSave ? File.ReadAllText(SavePath) : null;
    public GameState State => session.CasteOrder > 0 ? session.State : null;

    public event Action CurrentCardChanged;

    /// <summary>
    /// 수동 테스트에서 해금한 엔딩을 계층별 도감으로 반환합니다.
    /// </summary>
    public IReadOnlyList<EndingCollectionGroup> GetEndingCollection()
    {
        return endings.GetCollection(archive.LoadSeenIds());
    }

    /// <summary>
    /// 테스트 세이브의 현재 카드 제목을 조회합니다. 제목을 찾거나 저장을 읽을 수 없으면 <see langword="null"/>를 반환합니다.
    /// </summary>
    public string GetSavedCardTitle()
    {
        try
        {
            string variantId = store.LoadOrNull()?.progress.currentVariantId;
            return data.Database.FindByVariantId(variantId)?.title;
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// 기존 테스트 저장 내용을 기록한 뒤 현재 턴 설정으로 새 테스트 게임을 시작합니다.
    /// </summary>
    public void StartNewGame()
    {
        SaveBeforeAction = CurrentSave;
        session.StartNewGame(readTurnLimit());
    }

    /// <summary>
    /// 현재 테스트 카드가 있는지 확인하고 저장 내용을 기록한 뒤 좌우 선택을 적용합니다.
    /// </summary>
    public void Choose(bool right)
    {
        Require(State?.GetCurrentCard() != null, "먼저 테스트 카드를 지정해 주세요.");
        SaveBeforeAction = CurrentSave;
        session.Choose(right);
    }

    /// <summary>
    /// 현재 테스트 저장 내용을 기록한 뒤 가장 최근 체크포인트를 복원합니다.
    /// </summary>
    public void Restore()
    {
        Require(State != null, "먼저 테스트를 초기화해 주세요.");
        SaveBeforeAction = CurrentSave;
        Require(session.RestoreCheckpoint(), "테스트 저장에 체크포인트가 없습니다.");
    }

    /// <summary>
    /// 현재 테스트 저장 내용을 기록하고 기존 테스트 세이브에서 진행을 재개합니다.
    /// </summary>
    public void ResumeGame()
    {
        SaveBeforeAction = CurrentSave;
        Require(session.TryResume(readTurnLimit()), "이어할 진행 중인 테스트 세이브가 없습니다.");
    }

    /// <summary>
    /// 종료된 테스트 진행에서 그 계층의 체크포인트를 복원합니다.
    /// </summary>
    public void ResumeFromEnding()
    {
        SaveBeforeAction = CurrentSave;
        Require(session.ResumeFromEnding(), "이어할 엔딩 체크포인트가 없습니다.");
    }

    /// <summary>
    /// 현재 계층에 속하는 테스트 카드의 변형 ID를 저장한 뒤 해당 카드로 세션을 다시 엽니다.
    /// </summary>
    public void AssignDemoCard()
    {
        Require(State != null, "먼저 테스트를 초기화해 주세요.");
        Require(data.DemoCard.casteOrder == session.CasteOrder, "현재 계층에 속하는 테스트 카드만 지정할 수 있습니다.");
        SaveBeforeAction = CurrentSave;
        SaveData save = store.LoadOrNull();
        Require(save != null, "이어할 테스트 저장 파일이 없습니다.");
        save.progress.currentVariantId = data.DemoCard.variantId;
        save.progress.isFinished = false;
        save.progress.endingId = null;
        store.Save(save);
        session.StartOrResume(readTurnLimit());
    }

    /// <summary>
    /// 수동 테스트 세션의 카드 변경 구독을 해제합니다.
    /// </summary>
    public void Dispose()
    {
        session.State.CurrentCardChanged -= ForwardCardChange;
    }

    /// <summary>
    /// 세션의 카드 변경을 수동 테스트 화면의 구독자에게 전달합니다.
    /// </summary>
    private void ForwardCardChange()
    {
        CurrentCardChanged?.Invoke();
    }
}
