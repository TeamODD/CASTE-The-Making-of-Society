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

    /// <summary>
    /// 카드 데이터, 선택 규칙, 저장소를 연결하고 초기 턴 한도와 계층 진행 순서를 정합니다.
    /// </summary>
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

    /// <summary>
    /// 세션 생성 시 정한 턴 한도로 저장을 불러오고, 저장이 없으면 새 게임을 시작합니다.
    /// </summary>
    public void StartOrResume()
    {
        StartOrResume(initialTurnLimit);
    }

    /// <summary>
    /// 저장된 진행을 엽니다. 저장이 없으면 지정한 턴 한도로 새 게임을 시작합니다.
    /// </summary>
    public bool StartOrResume(int turnsPerCaste)
    {
        return OpenSavedGame(turnsPerCaste, startWhenMissing: true);
    }

    /// <summary>
    /// 저장된 진행을 엽니다. 저장이 없으면 새 게임을 만들지 않고 실패를 반환합니다.
    /// </summary>
    public bool TryResume(int turnsPerCaste)
    {
        return OpenSavedGame(turnsPerCaste, startWhenMissing: false);
    }

    /// <summary>
    /// 처리 중인 입력이 없을 때 저장을 불러오고, 작업이 끝나면 입력 처리 상태를 해제합니다.
    /// </summary>
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

    /// <summary>
    /// 기존 진행을 새 게임으로 교체합니다. 다른 입력을 처리 중이면 요청을 건너뜁니다.
    /// </summary>
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

    /// <summary>
    /// 저장을 읽고 카드 참조를 검증합니다. 구형 저장을 해석하고 손상된 저장은 새 게임으로 교체합니다.
    /// </summary>
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

    /// <summary>
    /// 기본 스탯으로 첫 계층에 진입하고 첫 카드와 체크포인트를 저장한 뒤 상태를 공개합니다.
    /// </summary>
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

    /// <summary>
    /// 현재 카드의 선택 효과를 계산하고 다음 카드 또는 계층을 결정합니다. 스탯이 한계에 닿으면 체크포인트를 복원하고, 정상 진행은 저장한 뒤 알립니다.
    /// </summary>
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

    /// <summary>
    /// 다른 입력을 처리 중이 아닐 때 최근 체크포인트로 복귀를 시도하고 성공 여부를 반환합니다.
    /// </summary>
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

    /// <summary>
    /// 최근 계층 진입 당시의 카드, 스탯, 플래그를 복원하고 턴과 사건 기록을 초기화해 저장합니다.
    /// </summary>
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

    /// <summary>
    /// 계층 진입 당시 플래그와 현재 진행, 남은 턴을 사용해 같은 계층의 다음 카드를 고릅니다.
    /// </summary>
    private CardData SelectCard(SaveData data)
    {
        ProgressData progress = data.progress;
        var state = new GameState(progress.stats, progress.flags, progress.seenEventIds);
        return selector.PickNextCard(cards, progress.casteOrder, data.GetLastCheckpoint().flags,
            state, progress.turnsPerCaste - progress.turnsUsedInCaste);
    }

    /// <summary>
    /// 카드를 낼 수 있는 다음 계층으로 진입하고 진행 데이터에 첫 카드와 체크포인트를 기록합니다.
    /// </summary>
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

    /// <summary>
    /// 구형 저장에 계층 순서, 첫 카드, 턴 정보를 채우고 현재 카드와 즉시 저장 여부를 결정합니다.
    /// </summary>
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

    /// <summary>
    /// 저장된 체크포인트와 현재 카드가 CSV의 계층 정보와 일치하는지 검사하고, 진행 중이면 현재 카드를 반환합니다.
    /// </summary>
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

    /// <summary>
    /// 변형 ID로 카드를 찾고 계층 순서와 코드가 저장 내용과 일치하는지 확인합니다.
    /// </summary>
    private CardData ResolveCard(string variantId, int casteOrder, string casteCode)
    {
        CardData card = cardDatabase.FindByVariantId(variantId);
        if (card == null || card.casteOrder != casteOrder || card.casteCode != casteCode)
            throw new InvalidDataException($"Unknown or mismatched card variant '{variantId}'.");
        return card;
    }

    /// <summary>
    /// 기존 체크포인트 목록을 복사하고 새 진행 정보를 담은 저장 데이터를 만듭니다.
    /// </summary>
    private SaveData WithProgress(ProgressData progress)
    {
        return new SaveData
        {
            schemaVersion = SaveData.CurrentSchemaVersion,
            checkpoints = new List<CheckpointData>(saveData.checkpoints),
            progress = progress
        };
    }

    /// <summary>
    /// 다음 진행을 저장하고, 저장에 성공한 뒤 게임 상태에 반영합니다.
    /// </summary>
    private void SaveAndPublish(SaveData next, CardData card)
    {
        saveStore.Save(next);
        PublishState(next, card);
    }

    /// <summary>
    /// 저장 데이터를 세션에 반영하고 스탯, 플래그, 사건 기록, 현재 카드 순서로 상태를 갱신합니다.
    /// </summary>
    private void PublishState(SaveData next, CardData card)
    {
        saveData = next;
        State.SetStats(next.progress.stats);
        State.SetFlags(next.progress.flags);
        State.SetSeenEventIds(next.progress.seenEventIds);
        State.UpdateCurrentCard(card);
    }
}
