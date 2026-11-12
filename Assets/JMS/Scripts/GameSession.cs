using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public sealed class GameSession
{
    private readonly CardDatabase cardDatabase;
    private readonly CardSelector selector;
    private readonly ISaveStore saveStore;
    private readonly List<CardData> cards;
    private readonly int[] casteOrders;
    private readonly int initialTurnLimit;
    private SaveData saveData;
    private bool processingInput;

    public event Action<int, int> CasteChanged;

    public GameState State { get; } = new GameState();
    public int CasteOrder => saveData?.progress.casteOrder ?? 0;
    public int TurnsUsedInCaste => saveData?.progress.turnsUsedInCaste ?? 0;

    public GameSession(
        CardDatabase cardDatabase,
        CardSelector selector,
        ISaveStore saveStore,
        int turnsPerCaste = 12)
    {
        this.cardDatabase = cardDatabase ?? throw new ArgumentNullException(nameof(cardDatabase));
        this.selector = selector ?? throw new ArgumentNullException(nameof(selector));
        this.saveStore = saveStore ?? throw new ArgumentNullException(nameof(saveStore));
        initialTurnLimit = Math.Max(0, turnsPerCaste);
        cards = cardDatabase.GetAllCards();
        casteOrders = cards.Select(card => card.casteOrder)
            .Where(order => order > 0).Distinct().OrderBy(order => order).ToArray();
    }

    public void StartOrResume()
    {
        StartOrResume(initialTurnLimit);
    }

    public bool StartOrResume(int turnsPerCaste)
    {
        return OpenSavedGame(turnsPerCaste, startWhenMissing: true);
    }

    public bool TryResume(int turnsPerCaste)
    {
        return OpenSavedGame(turnsPerCaste, startWhenMissing: false);
    }

    private bool OpenSavedGame(int turnsPerCaste, bool startWhenMissing)
    {
        if (processingInput)
            return false;

        processingInput = true;
        try
        {
            return LoadSavedGame(Math.Max(0, turnsPerCaste), startWhenMissing);
        }
        finally
        {
            processingInput = false;
        }
    }

    public bool StartNewGame(int turnsPerCaste)
    {
        if (processingInput)
            return false;

        processingInput = true;
        try
        {
            SaveNewGame(Math.Max(0, turnsPerCaste));
            return true;
        }
        finally
        {
            processingInput = false;
        }
    }

    private bool LoadSavedGame(int turnsPerCaste, bool startWhenMissing)
    {
        SaveData next;
        CardData card = null;
        bool saveOnStart = false;
        try
        {
            next = saveStore.LoadOrNull();
            if (next != null)
            {
                if (next.schemaVersion == null)
                    next = PrepareLegacySave(next, turnsPerCaste, out card, out saveOnStart);
                else
                    card = ValidateSavedCards(next);
            }
        }
        catch (InvalidDataException)
        {
            SaveNewGame(turnsPerCaste);
            return true;
        }

        if (next == null)
        {
            if (!startWhenMissing)
                return false;

            SaveNewGame(turnsPerCaste);
            return true;
        }

        if (saveOnStart)
            saveStore.Save(next);
        PublishState(next, card);
        return true;
    }

    private void SaveNewGame(int turnsPerCaste)
    {
        var next = new SaveData
        {
            schemaVersion = SaveData.CurrentSchemaVersion,
            progress = new ProgressData { stats = Stat.Default, turnsPerCaste = turnsPerCaste }
        };
        CardData card = EnterNextCaste(next, afterCasteOrder: 0);
        if (card == null)
            throw new InvalidOperationException("No starting card is available.");

        SaveAndPublish(next, card);
    }

    public void Choose(bool isRight)
    {
        CardData currentCard = State.GetCurrentCard();
        ChoiceData choice = isRight ? currentCard?.rightChoice : currentCard?.leftChoice;
        if (processingInput || choice == null)
            return;

        processingInput = true;
        try
        {
            int previousCaste = CasteOrder;
            var updated = new GameState(State.GetStats(), State.CopyFlags(), State.CopySeenEventIds());
            updated.ApplyStatDelta(choice.delta);
            if (updated.GetStats().IsAtLimit())
            {
                RestoreLastCheckpoint();
                return;
            }

            updated.AddFlag(choice.flagOnSelect);
            updated.MarkEventSeen(currentCard.eventId);
            int limit = saveData.progress.turnsPerCaste;
            int used = TurnsUsedInCaste < limit ? TurnsUsedInCaste + 1 : TurnsUsedInCaste;
            SaveData next = WithProgress(new ProgressData
            {
                casteOrder = previousCaste,
                turnsUsedInCaste = used,
                turnsPerCaste = limit,
                stats = updated.GetStats(),
                flags = updated.CopyFlags(),
                seenEventIds = updated.CopySeenEventIds()
            });

            CardData card = SelectCard(next);
            if (card == null)
                card = EnterNextCaste(next, previousCaste);
            next.progress.currentVariantId = card?.variantId;
            next.progress.isFinished = card == null;

            SaveAndPublish(next, card);
            if (card != null && previousCaste != next.progress.casteOrder)
                CasteChanged?.Invoke(previousCaste, next.progress.casteOrder);
        }
        finally
        {
            processingInput = false;
        }
    }

    public bool RestoreCheckpoint()
    {
        if (processingInput || saveData == null)
            return false;

        processingInput = true;
        try
        {
            return RestoreLastCheckpoint();
        }
        finally
        {
            processingInput = false;
        }
    }

    private bool RestoreLastCheckpoint()
    {
        CheckpointData checkpoint = saveData.GetLastCheckpoint();
        if (checkpoint == null)
            return false;

        CardData card = ResolveCard(checkpoint.firstVariantId, checkpoint.casteOrder, checkpoint.casteCode);
        SaveData next = WithProgress(new ProgressData
        {
            casteOrder = checkpoint.casteOrder,
            turnsPerCaste = saveData.progress.turnsPerCaste,
            stats = checkpoint.stats,
            flags = new List<string>(checkpoint.flags),
            currentVariantId = card.variantId
        });
        SaveAndPublish(next, card);
        return true;
    }

    private CardData SelectCard(SaveData data)
    {
        ProgressData progress = data.progress;
        var state = new GameState(progress.stats, progress.flags, progress.seenEventIds);
        return selector.PickNextCard(cards, progress.casteOrder, data.GetLastCheckpoint().flags,
            state, progress.turnsPerCaste - progress.turnsUsedInCaste);
    }

    private CardData EnterNextCaste(SaveData data, int afterCasteOrder)
    {
        ProgressData progress = data.progress;
        var entry = new GameState(progress.stats, progress.flags);
        foreach (int casteOrder in casteOrders)
        {
            if (casteOrder <= afterCasteOrder)
                continue;

            CardData card = selector.PickNextCard(cards, casteOrder, progress.flags, entry, progress.turnsPerCaste);
            if (card == null)
                continue;

            progress.casteOrder = casteOrder;
            progress.turnsUsedInCaste = 0;
            progress.seenEventIds.Clear();
            progress.currentVariantId = card.variantId;
            data.checkpoints.Add(new CheckpointData
            {
                casteOrder = casteOrder,
                casteCode = card.casteCode,
                stats = progress.stats,
                flags = new List<string>(progress.flags),
                firstVariantId = card.variantId
            });
            return card;
        }
        return null;
    }

    private SaveData PrepareLegacySave(SaveData legacy, int turnsPerCaste, out CardData card, out bool saveOnStart)
    {
        var checkpoints = new List<CheckpointData>();
        foreach (CheckpointData old in legacy.checkpoints)
        {
            CardData caste = cards.FirstOrDefault(item => item.casteCode == old.casteCode);
            if (caste == null)
                throw new InvalidDataException($"Unknown legacy checkpoint caste '{old.casteCode}'.");

            var entry = new GameState(old.stats, old.flags);
            CardData first = selector.PickNextCard(cards, caste.casteOrder, old.flags, entry, turnsPerCaste);
            if (first == null)
                throw new InvalidDataException($"No first card for legacy checkpoint '{old.casteCode}'.");

            checkpoints.Add(new CheckpointData
            {
                casteOrder = caste.casteOrder,
                casteCode = old.casteCode,
                stats = old.stats,
                flags = new List<string>(old.flags),
                firstVariantId = first.variantId
            });
        }

        ProgressData saved = legacy.progress;
        int casteOrder = checkpoints.Last().casteOrder;
        var casteEvents = new HashSet<string>(cards.Where(item => item.casteOrder == casteOrder)
            .Select(item => item.eventId));
        List<string> seen = saved.seenEventIds.Where(casteEvents.Contains).Distinct().ToList();
        var next = new SaveData
        {
            schemaVersion = SaveData.CurrentSchemaVersion,
            checkpoints = checkpoints,
            progress = new ProgressData
            {
                casteOrder = casteOrder,
                turnsUsedInCaste = Math.Min(seen.Count, turnsPerCaste),
                turnsPerCaste = turnsPerCaste,
                stats = saved.stats,
                flags = new List<string>(saved.flags),
                seenEventIds = seen,
                currentVariantId = saved.currentVariantId
            }
        };

        saveOnStart = false;
        if (!string.IsNullOrWhiteSpace(saved.currentVariantId))
        {
            card = ResolveCard(saved.currentVariantId, casteOrder, checkpoints.Last().casteCode);
            return next;
        }

        card = SelectCard(next);
        if (card == null)
        {
            card = EnterNextCaste(next, casteOrder);
            saveOnStart = true;
        }

        next.progress.currentVariantId = card?.variantId;
        next.progress.isFinished = card == null;
        return next;
    }

    private CardData ValidateSavedCards(SaveData saved)
    {
        foreach (CheckpointData checkpoint in saved.checkpoints)
            ResolveCard(checkpoint.firstVariantId, checkpoint.casteOrder, checkpoint.casteCode);

        CheckpointData latest = saved.GetLastCheckpoint();
        if (latest.casteOrder != saved.progress.casteOrder)
            throw new InvalidDataException("The progress caste does not match the latest checkpoint.");

        return saved.progress.isFinished
            ? null
            : ResolveCard(saved.progress.currentVariantId, latest.casteOrder, latest.casteCode);
    }

    private CardData ResolveCard(string variantId, int casteOrder, string casteCode)
    {
        CardData card = cardDatabase.FindByVariantId(variantId);
        if (card == null || card.casteOrder != casteOrder || card.casteCode != casteCode)
            throw new InvalidDataException($"Unknown or mismatched card variant '{variantId}'.");
        return card;
    }

    private SaveData WithProgress(ProgressData progress)
    {
        return new SaveData
        {
            schemaVersion = SaveData.CurrentSchemaVersion,
            checkpoints = new List<CheckpointData>(saveData.checkpoints),
            progress = progress
        };
    }

    private void SaveAndPublish(SaveData next, CardData card)
    {
        saveStore.Save(next);
        PublishState(next, card);
    }

    private void PublishState(SaveData next, CardData card)
    {
        saveData = next;
        State.SetStats(next.progress.stats);
        State.SetFlags(next.progress.flags);
        State.SetSeenEventIds(next.progress.seenEventIds);
        State.UpdateCurrentCard(card);
    }
}
