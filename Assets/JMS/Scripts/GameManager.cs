using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private TextAsset csvFile;
    [Min(0)] public int TurnsPerCaste = 12;

    public GameState State => session != null && session.CasteOrder > 0 ? session.State : null;
    public bool CanResume => GetSaveStore().HasSave;
    public event Action<GameState> GameStateReady;
    public event Action<GameState> GameStateChanged;
    public event Action<int, int> CasteChanged;

    private ISaveStore saveStore;
    private GameSession session;
    private bool openingGame;

    private void OnDestroy()
    {
        if (session == null)
            return;

        session.State.CurrentCardChanged -= PublishStateChanged;
        session.CasteChanged -= PublishCasteChanged;
    }

    public void StartNewGame()
    {
        OpenGame((game, turnLimit) => game.StartNewGame(turnLimit));
    }

    public void ResumeGame()
    {
        OpenGame((game, turnLimit) => game.TryResume(turnLimit));
    }

    public void ChooseLeft()
    {
        Choose(isRight: false);
    }

    public void ChooseRight()
    {
        Choose(isRight: true);
    }

    public void BackToStart()
    {
        if (!openingGame)
            session?.RestoreCheckpoint();
    }

    private void Choose(bool isRight)
    {
        if (!openingGame)
            session?.Choose(isRight);
    }

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

    protected virtual ISaveStore CreateSaveStore()
    {
        return new SaveSystem();
    }

    private ISaveStore GetSaveStore()
    {
        return saveStore ??= CreateSaveStore();
    }

    private void PublishStateChanged()
    {
        if (!openingGame)
            GameStateChanged?.Invoke(State);
    }

    private void PublishCasteChanged(int previousCasteOrder, int nextCasteOrder)
    {
        CasteChanged?.Invoke(previousCasteOrder, nextCasteOrder);
    }
}
