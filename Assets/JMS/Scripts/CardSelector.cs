using System;
using System.Collections.Generic;
using System.Linq;

public sealed class CardSelector
{
    private readonly Random random;

    public CardSelector(Random random = null)
    {
        this.random = random ?? new Random();
    }

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

    private static bool MeetsLegacyCondition(CardData card, IReadOnlyCollection<string> flags)
    {
        string required = card.requiredLegacyFlag;
        return string.IsNullOrWhiteSpace(required) || required == "NONE" || flags.Contains(required);
    }

    private static int GetMainEventNumber(string eventId)
    {
        int suffix = eventId.LastIndexOf("-M", StringComparison.Ordinal);
        return suffix >= 0 && int.TryParse(eventId.Substring(suffix + 2), out int number)
            ? number
            : int.MaxValue;
    }
}
