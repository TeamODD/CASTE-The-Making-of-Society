using System;
using System.Collections.Generic;

public class GameState
{
    public event Action CurrentCardChanged;

    private Stat stats;
    private readonly List<string> flags;
    private readonly List<string> cardHist; // 전체 계층 기록
    private CardData currentCard;

    public GameState(Stat? stats = null, List<string> flags = null, List<string> cardHist = null) // 현재는 무조건 null
    {
        this.stats = stats ?? Stat.Default;
        this.flags = flags == null ? new List<string>() : new List<string>(flags);
        this.cardHist = cardHist == null ? new List<string>() : new List<string>(cardHist);
    }

    public CardData GetCurrentCard()
    {
        return currentCard;
    }

    public void UpdateCurrentCard(CardData card)
    {
        currentCard = card;
        CurrentCardChanged?.Invoke();
    }

    public Stat GetStats()
    {
        return stats;
    }

    public void SetStats(Stat value)
    {
        stats = value;
    }

    public void ApplyStatDelta(Stat delta)
    {
        stats = (stats + delta).Clamp();
    }

    public void SetFlags(List<string> savedFlags)
    {
        flags.Clear();
        if (savedFlags != null)
        {
            flags.AddRange(savedFlags);
        }
    }

    public bool HasFlag(string flag)
    {
        return flags.Contains(flag);
    }

    public void AddFlag(string flag)
    {
        if (!string.IsNullOrWhiteSpace(flag) && !flags.Contains(flag))
        {
            flags.Add(flag);
        }
    }

    public bool RemoveFlag(string flag)
    {
        return !string.IsNullOrWhiteSpace(flag) && flags.Remove(flag);
    }

    public void InitCardHist()
    {
        cardHist.Clear();
    }

    public bool HasSeenCard(string eventId) // 중복 카드 방지
    {
        return cardHist.Contains(eventId);
    }

    public void MarkEventSeen(string eventId) // 나온 카드 기록
    {
        if (!string.IsNullOrWhiteSpace(eventId) && !cardHist.Contains(eventId))
        {
            cardHist.Add(eventId);
        }
    }

    public List<string> CopyFlags()
    {
        return new List<string>(flags);
    }

    public List<string> CopySeenEventIds()
    {
        return new List<string>(cardHist);
    }

    public void SetSeenEventIds(List<string> savedIds) //로드용
    {
        cardHist.Clear();
        if (savedIds != null)
        {
            cardHist.AddRange(savedIds);
        }
    }
}
