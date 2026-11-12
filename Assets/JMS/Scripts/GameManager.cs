using System;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [SerializeField] private TextAsset csvFile;

    public event Action<GameState> GameStateReady;

    private GameSession session;

    private void Start()
    {
        var cardDatabase = new CardDatabase(csvFile.text);
        session = new GameSession(cardDatabase, new CardSelector(), new SaveSystem());
        session.StartOrResume();
        GameStateReady?.Invoke(session.State);
    }

    public void ChooseLeft()
    {
        session.Choose(false);
    }

    public void ChooseRight()
    {
        session.Choose(true);
    }

    public void BackToStart()
    {
        session.RestoreCheckpoint();
    }
}
