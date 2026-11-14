using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CardSelector
{
    private readonly Random random;

    /// <summary>
    /// 다음 카드 추첨에 사용할 난수 생성기를 지정합니다.
    /// </summary>
    public CardSelector(Random random = null)
    {
        this.random = random ?? new Random();
    }

    /// <summary>
    /// 필요한 턴을 메인 사건에 먼저 확보하고, 여유 턴에는 다음 메인 사건과 일반 사건을 함께 추첨합니다. 후보는 사건 ID마다 하나씩 고릅니다.
    /// </summary>
    public CardData PickNextCard(
        IReadOnlyList<CardData> cards,
        int casteOrder,
        IReadOnlyCollection<string> entryFlags,
        GameState state,
        int remainingTurns)
    {
        var remainingMainCards = cards
            .Where(card => card.casteOrder == casteOrder && card.eventType == "main"
                && !state.HasSeenCard(card.eventId) && MeetsLegacyCondition(card, entryFlags))
            .GroupBy(card => card.eventId)
            .Select(variants => variants.First())
            .OrderBy(card => GetMainEventNumber(card.eventId))
            .ToList();

        CardData nextMainCard = remainingMainCards.FirstOrDefault();
        if (remainingTurns <= remainingMainCards.Count)
            return nextMainCard;

        var currentFlags = state.CopyFlags();
        var candidates = cards
            .Where(card => card.casteOrder == casteOrder && card.eventType != "main"
                && !state.HasSeenCard(card.eventId) && MeetsLegacyCondition(card, currentFlags))
            .GroupBy(card => card.eventId)
            .Select(variants => variants.First())
            .ToList();
        if (nextMainCard != null)
            candidates.Add(nextMainCard);

        return candidates.Count == 0 ? null : candidates[random.Next(candidates.Count)];
    }

    /// <summary>
    /// 카드에 유산 조건이 없거나, 전달받은 플래그에 필요한 유산이 있는지 확인합니다.
    /// </summary>
    private static bool MeetsLegacyCondition(CardData card, IReadOnlyCollection<string> flags)
    {
        string required = card.requiredLegacyFlag;
        return string.IsNullOrWhiteSpace(required) || required == "NONE" || flags.Contains(required);
    }

    /// <summary>
    /// 사건 ID의 -M 뒤에서 순서 번호를 읽습니다. 번호를 읽을 수 없는 사건은 정렬의 끝에 배치합니다.
    /// </summary>
    private static int GetMainEventNumber(string eventId)
    {
        int suffix = eventId.LastIndexOf("-M", StringComparison.Ordinal);
        return suffix >= 0 && int.TryParse(eventId.Substring(suffix + 2), out int number)
            ? number
            : int.MaxValue;
    }
}
