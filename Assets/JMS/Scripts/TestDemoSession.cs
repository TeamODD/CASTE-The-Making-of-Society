using System;
using System.IO;
using static TestSessionData;

internal sealed class TestDemoSession : IDisposable
{
    private readonly TestSessionData data;
    private readonly SaveSystem store;
    private readonly Func<int> readTurnLimit;
    private readonly GameSession session;

    public TestDemoSession(TestSessionData data, Func<int> readTurnLimit, string persistentDataPath)
    {
        this.data = data;
        this.readTurnLimit = readTurnLimit;
        store = new SaveSystem(Path.Combine(persistentDataPath, "TestDemo", "save.json"));
        session = data.CreateSession(store);
        session.State.CurrentCardChanged += ForwardCardChange;
    }

    public string SavePath => store.SavePath;
    public bool HasSave => store.HasSave;
    public string SaveBeforeAction { get; private set; }
    public string CurrentSave => HasSave ? File.ReadAllText(SavePath) : null;
    public GameState State => session.CasteOrder > 0 ? session.State : null;

    public event Action CurrentCardChanged;

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

    public void StartNewGame()
    {
        SaveBeforeAction = CurrentSave;
        session.StartNewGame(readTurnLimit());
    }

    public void Choose(bool right)
    {
        Require(State?.GetCurrentCard() != null, "먼저 테스트 카드를 지정해 주세요.");
        SaveBeforeAction = CurrentSave;
        session.Choose(right);
    }

    public void Restore()
    {
        Require(State != null, "먼저 테스트를 초기화해 주세요.");
        SaveBeforeAction = CurrentSave;
        Require(session.RestoreCheckpoint(), "테스트 저장에 체크포인트가 없습니다.");
    }

    public void ResumeGame()
    {
        SaveBeforeAction = CurrentSave;
        Require(session.TryResume(readTurnLimit()), "이어할 테스트 저장 파일이 없습니다.");
    }

    public void AssignDemoCard()
    {
        Require(State != null, "먼저 테스트를 초기화해 주세요.");
        Require(data.DemoCard.casteOrder == session.CasteOrder, "현재 계층에 속하는 테스트 카드만 지정할 수 있습니다.");
        SaveBeforeAction = CurrentSave;
        SaveData save = store.LoadOrNull();
        Require(save != null, "이어할 테스트 저장 파일이 없습니다.");
        save.progress.currentVariantId = data.DemoCard.variantId;
        save.progress.isFinished = false;
        store.Save(save);
        session.StartOrResume(readTurnLimit());
    }

    public void Dispose()
    {
        session.State.CurrentCardChanged -= ForwardCardChange;
    }

    private void ForwardCardChange()
    {
        CurrentCardChanged?.Invoke();
    }
}
