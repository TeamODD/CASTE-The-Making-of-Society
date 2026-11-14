using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private TextAsset csvFile;
    [Min(0)] public int TurnsPerCaste = 12;

    /// <summary>
    /// 현재 게임 상태를 반환합니다. 새 게임이나 이어하기가 성공하기 전에는 <see langword="null"/>입니다.
    /// </summary>
    public GameState State => session != null && session.CasteOrder > 0 ? session.State : null;

    /// <summary>
    /// 로컬 저장 파일이 있는지 확인합니다. 저장 데이터의 유효성은 <see cref="ResumeGame"/>에서 확인합니다.
    /// </summary>
    public bool CanResume => GetSaveStore().HasSave;

    /// <summary>
    /// 새 게임이나 이어하기가 성공하면 초기 화면에 표시할 게임 상태를 전달합니다.
    /// 이어서 <see cref="GameStateChanged"/>가 발생합니다.
    /// </summary>
    public event Action<GameState> GameStateReady;

    /// <summary>
    /// 새 게임, 이어하기, 선택 처리 또는 체크포인트 복원이 완료되면 갱신된 게임 상태를 전달합니다.
    /// 화면에 표시할 카드와 스탯은 전달받은 상태에서 읽습니다.
    /// </summary>
    public event Action<GameState> GameStateChanged;

    /// <summary>
    /// 다음 계층의 첫 카드와 체크포인트를 저장한 뒤 이전 계층과 새 계층의 순서 번호를 차례대로 전달합니다.
    /// <see cref="GameStateChanged"/> 이후에 발생합니다.
    /// </summary>
    public event Action<int, int> CasteChanged;

    private ISaveStore saveStore;
    private GameSession session;
    private bool openingGame;

    /// <summary>
    /// 매니저가 파괴될 때 세션의 카드 변경과 계층 전환 이벤트 구독을 해제합니다.
    /// </summary>
    private void OnDestroy()
    {
        if (session == null)
            return;

        session.State.CurrentCardChanged -= PublishStateChanged;
        session.CasteChanged -= PublishCasteChanged;
    }

    /// <summary>
    /// 첫 계층의 첫 카드와 체크포인트를 저장하고 새 게임을 시작합니다. 기존 저장은 새 진행으로 교체됩니다.
    /// </summary>
    public void StartNewGame()
    {
        OpenGame((game, turnLimit) => game.StartNewGame(turnLimit));
    }

    /// <summary>
    /// 로컬 저장에서 진행을 불러옵니다. 저장 파일이 없으면 현재 상태를 유지하고, 손상된 파일은 새 진행으로 교체합니다.
    /// </summary>
    public void ResumeGame()
    {
        OpenGame((game, turnLimit) => game.TryResume(turnLimit));
    }

    /// <summary>
    /// 현재 카드의 왼쪽 선택을 처리합니다. 왼쪽 스와이프가 확정될 때 한 번 호출합니다.
    /// </summary>
    public void ChooseLeft()
    {
        Choose(isRight: false);
    }

    /// <summary>
    /// 현재 카드의 오른쪽 선택을 처리합니다. 오른쪽 스와이프가 확정될 때 한 번 호출합니다.
    /// </summary>
    public void ChooseRight()
    {
        Choose(isRight: true);
    }

    /// <summary>
    /// 최근 체크포인트의 첫 카드, 스탯, 유산 플래그를 복원하고 해당 진행을 저장합니다.
    /// </summary>
    public void BackToStart()
    {
        if (!openingGame)
            session?.RestoreCheckpoint();
    }

    /// <summary>
    /// 게임을 여는 중이 아닐 때 방향에 해당하는 선택을 현재 세션에 전달합니다.
    /// </summary>
    private void Choose(bool isRight)
    {
        if (!openingGame)
            session?.Choose(isRight);
    }

    /// <summary>
    /// 필요하면 CSV와 저장소로 세션을 만들고, 새 게임 또는 이어하기가 성공했을 때 상태를 알립니다.
    /// </summary>
    private void OpenGame(Func<GameSession, int, bool> open)
    {
        if (openingGame)
            return;

        openingGame = true;
        try
        {
            if (session == null)
            {
                if (csvFile == null)
                    throw new InvalidOperationException("GameManager requires an event CSV.");

                var cardDatabase = new CardDatabase(csvFile.text);
                session = new GameSession(cardDatabase, new CardSelector(), GetSaveStore(), TurnsPerCaste);
                session.State.CurrentCardChanged += PublishStateChanged;
                session.CasteChanged += PublishCasteChanged;
            }

            if (open(session, TurnsPerCaste))
            {
                GameStateReady?.Invoke(State);
                GameStateChanged?.Invoke(State);
            }
        }
        finally
        {
            openingGame = false;
        }
    }

    /// <summary>
    /// 기본 경로를 쓰는 로컬 저장소를 만듭니다. 테스트에서는 재정의해 다른 저장소를 연결할 수 있습니다.
    /// </summary>
    protected virtual ISaveStore CreateSaveStore()
    {
        return new SaveSystem();
    }

    /// <summary>
    /// 처음 사용할 때 저장소를 만들고 이후 호출에서 같은 인스턴스를 반환합니다.
    /// </summary>
    private ISaveStore GetSaveStore()
    {
        return saveStore ??= CreateSaveStore();
    }

    /// <summary>
    /// 게임을 여는 중이 아닐 때 세션의 카드 변경을 게임 상태 변경 이벤트로 전달합니다.
    /// </summary>
    private void PublishStateChanged()
    {
        if (!openingGame)
            GameStateChanged?.Invoke(State);
    }

    /// <summary>
    /// 세션이 전달한 이전 계층과 새 계층의 순서 번호를 구독자에게 알립니다.
    /// </summary>
    private void PublishCasteChanged(int previousCasteOrder, int nextCasteOrder)
    {
        CasteChanged?.Invoke(previousCasteOrder, nextCasteOrder);
    }
}
