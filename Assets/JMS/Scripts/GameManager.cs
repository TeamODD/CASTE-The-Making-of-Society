using System;
using System.Collections.Generic;
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
    /// 타이틀의 이어하기 버튼을 활성화할 수 있는지 확인합니다. 종료된 회차는 이어할 수 없습니다.
    /// </summary>
    public bool CanResume => GameSession.HasResumableSave(GetSaveStore());

    /// <summary>
    /// 새 게임이나 이어하기가 성공하면 초기 화면에 표시할 게임 상태를 전달합니다.
    /// 이어서 <see cref="GameStateChanged"/>가 발생합니다.
    /// </summary>
    public event Action<GameState> GameStateReady;

    /// <summary>
    /// 새 게임, 이어하기, 선택 처리 또는 체크포인트 복원이 완료되면 갱신된 게임 상태를 전달합니다.
    /// 화면에 표시할 카드와 스탯은 전달받은 상태에서 읽습니다.
    /// <see cref="GameState.IsFinished"/>가 참이면 엔딩 화면을 표시합니다.
    /// </summary>
    public event Action<GameState> GameStateChanged;

    /// <summary>
    /// 다음 계층의 첫 카드와 체크포인트를 저장한 뒤 이전 계층과 새 계층의 순서 번호를 차례대로 전달합니다.
    /// <see cref="GameStateChanged"/> 이후에 발생합니다.
    /// </summary>
    public event Action<int, int> CasteChanged;

    private ISaveStore saveStore;
    private EndingDatabase endings;
    private EndingArchiveStore endingArchive;
    private GameSession session;
    private bool openingGame;

    /// <summary>
    /// 게임 세션을 열기 전에도 계층별 엔딩 도감을 조회합니다.
    /// 잠긴 항목도 목록에 포함되며 ID, 본문과 이미지는 null입니다.
    /// </summary>
    public IReadOnlyList<EndingCollectionGroup> GetEndingCollection()
    {
        return GetEndingDatabase().GetCollection(GetEndingArchive().LoadSeenIds());
    }

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
    /// 브라만부터 새 게임을 시작합니다. 엔딩 화면의 새로하기 버튼에서 호출할 수 있습니다.
    /// 첫 카드와 체크포인트를 저장한 뒤 기존 진행을 교체합니다.
    /// </summary>
    public void StartNewGame()
    {
        OpenGame((game, turnLimit) => game.StartNewGame(turnLimit));
    }

    /// <summary>
    /// 로컬 저장에서 진행 중인 회차를 불러옵니다. 종료된 회차는 타이틀에서 이어할 수 없습니다.
    /// </summary>
    public void ResumeGame()
    {
        OpenGame((game, turnLimit) => game.TryResume(turnLimit));
    }

    /// <summary>
    /// 엔딩 화면의 이어하기를 처리합니다. 종료한 계층의 첫 카드와 진입 상태를 복원해 저장합니다.
    /// 복원에 성공하면 <see cref="GameStateChanged"/>로 갱신된 상태를 전달합니다.
    /// </summary>
    public void ResumeFromEnding()
    {
        if (!openingGame)
            session?.ResumeFromEnding();
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
                session = new GameSession(cardDatabase, new CardSelector(), GetSaveStore(), TurnsPerCaste,
                    GetEndingDatabase(), GetEndingArchive());
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
    /// 게임 진행과 도감에서 같은 엔딩 CSV를 사용합니다.
    /// </summary>
    private EndingDatabase GetEndingDatabase()
    {
        return endings ??= EndingDatabase.LoadDefault();
    }

    /// <summary>
    /// 도감 조회와 엔딩 기록에서 사용할 저장소를 반환합니다.
    /// </summary>
    private EndingArchiveStore GetEndingArchive()
    {
        return endingArchive ??= new EndingArchiveStore();
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
