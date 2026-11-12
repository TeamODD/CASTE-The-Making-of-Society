using System;
using System.Collections.Generic;
using System.IO;

public sealed class GameSession
{
    private readonly CardDatabase cardDatabase;
    private readonly CardSelector selector;
    private readonly ISaveStore saveStore; //SaveSystem
    private SaveData saveData = new();

    public GameState State { get; } = new GameState();

    public GameSession(CardDatabase cardDatabase, CardSelector selector, ISaveStore saveStore)
    {
        this.cardDatabase = cardDatabase ?? throw new ArgumentNullException(nameof(cardDatabase));
        this.selector = selector ?? throw new ArgumentNullException(nameof(selector));
        this.saveStore = saveStore ?? throw new ArgumentNullException(nameof(saveStore));
    }

    public void StartOrResume()
    {
        LoadGame(saveStore.LoadOrNull() ?? new SaveData());
    }

    public void Choose(bool isRight)
    {
        CardData currentCard = State.GetCurrentCard();
        ChoiceData choice = isRight ? currentCard?.rightChoice : currentCard?.leftChoice;
        if (choice == null)
        {
            return;
        }

        State.ApplyStatDelta(choice.delta);
        State.AddFlag(choice.flagOnSelect);
        State.MarkEventSeen(currentCard.eventId);
        State.UpdateCurrentCard(selector.PickNextCard(State));
        SaveProgress();
    }

    public bool RestoreCheckpoint()
    {
        CheckpointData checkpoint = saveData.GetLastCheckpoint();
        if (checkpoint == null)
            return false;

        saveData.progress = new ProgressData
        {
            stats = checkpoint.stats,
            flags = checkpoint.flags == null
                ? new List<string>()
                : new List<string>(checkpoint.flags)
        };

        LoadGame(saveData);
        SaveProgress();
        return true;
    }

    private void LoadGame(SaveData loaded)
    {
        saveData = loaded; // progress와 checkpoints를 모두 보관

        ProgressData progress = loaded.progress;
        State.SetStats(progress?.stats ?? Stat.Default);
        State.SetFlags(progress?.flags);
        State.SetSeenEventIds(progress?.seenEventIds);
        State.UpdateCurrentCard(progress?.currentCard);
    }

    private void SaveProgress() //현재 진행상황 저장
    {
        saveData.UpdateProgress(State);
        saveStore.Save(saveData); //세이브파일 생성
    }
}
