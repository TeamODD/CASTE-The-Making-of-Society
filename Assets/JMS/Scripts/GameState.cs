using System;
using System.Collections.Generic;

public class GameState
{
    public event Action CurrentCardChanged;

    private Stat stats;
    private readonly List<string> flags;
    private readonly List<string> cardHist; // 전체 계층 기록
    private CardData currentCard;

    /// <summary>
    /// 기본값 또는 지정한 스탯으로 상태를 만들고 플래그와 사건 기록을 복사합니다.
    /// </summary>
    public GameState(Stat? stats = null, List<string> flags = null, List<string> cardHist = null) // 현재는 무조건 null
    {
        this.stats = stats ?? Stat.Default;
        this.flags = flags == null ? new List<string>() : new List<string>(flags);
        this.cardHist = cardHist == null ? new List<string>() : new List<string>(cardHist);
    }

    /// <summary>
    /// 현재 카드 참조를 반환합니다. 진행에 카드가 없으면 <see langword="null"/>를 반환합니다.
    /// </summary>
    public CardData GetCurrentCard()
    {
        return currentCard;
    }

    /// <summary>
    /// 현재 카드 참조를 교체하고 카드 변경 이벤트를 보냅니다.
    /// </summary>
    public void UpdateCurrentCard(CardData card)
    {
        currentCard = card;
        CurrentCardChanged?.Invoke();
    }

    /// <summary>
    /// 현재 스탯을 값으로 반환합니다.
    /// </summary>
    public Stat GetStats()
    {
        return stats;
    }

    /// <summary>
    /// 현재 스탯을 전달받은 값으로 교체합니다.
    /// </summary>
    public void SetStats(Stat value)
    {
        stats = value;
    }

    /// <summary>
    /// 각 스탯에 변화량을 더하고 결과를 0~100으로 제한합니다.
    /// </summary>
    public void ApplyStatDelta(Stat delta)
    {
        stats = (stats + delta).Clamp();
    }

    /// <summary>
    /// 기존 플래그를 지우고 전달받은 목록의 내용을 복사합니다.
    /// </summary>
    public void SetFlags(List<string> savedFlags)
    {
        flags.Clear();
        if (savedFlags != null)
        {
            flags.AddRange(savedFlags);
        }
    }

    /// <summary>
    /// 지정한 유산 플래그를 보유하고 있는지 확인합니다.
    /// </summary>
    public bool HasFlag(string flag)
    {
        return flags.Contains(flag);
    }

    /// <summary>
    /// 비어 있지 않고 아직 보유하지 않은 유산 플래그를 추가합니다.
    /// </summary>
    public void AddFlag(string flag)
    {
        if (!string.IsNullOrWhiteSpace(flag) && !flags.Contains(flag))
        {
            flags.Add(flag);
        }
    }

    /// <summary>
    /// 지정한 플래그를 제거하고 실제로 제거했는지 반환합니다.
    /// </summary>
    public bool RemoveFlag(string flag)
    {
        return !string.IsNullOrWhiteSpace(flag) && flags.Remove(flag);
    }

    /// <summary>
    /// 현재 계층에서 본 사건 기록을 비웁니다.
    /// </summary>
    public void InitCardHist()
    {
        cardHist.Clear();
    }

    /// <summary>
    /// 사건 ID가 본 사건 기록에 있는지 확인합니다.
    /// </summary>
    public bool HasSeenCard(string eventId) // 중복 카드 방지
    {
        return cardHist.Contains(eventId);
    }

    /// <summary>
    /// 아직 기록되지 않은 유효한 사건 ID를 본 사건 목록에 추가합니다.
    /// </summary>
    public void MarkEventSeen(string eventId) // 나온 카드 기록
    {
        if (!string.IsNullOrWhiteSpace(eventId) && !cardHist.Contains(eventId))
        {
            cardHist.Add(eventId);
        }
    }

    /// <summary>
    /// 현재 유산 플래그 목록의 복사본을 반환합니다.
    /// </summary>
    public List<string> CopyFlags()
    {
        return new List<string>(flags);
    }

    /// <summary>
    /// 본 사건 ID 목록의 복사본을 반환합니다.
    /// </summary>
    public List<string> CopySeenEventIds()
    {
        return new List<string>(cardHist);
    }

    /// <summary>
    /// 기존 사건 기록을 지우고 전달받은 ID 목록의 내용을 복사합니다.
    /// </summary>
    public void SetSeenEventIds(List<string> savedIds) //로드용
    {
        cardHist.Clear();
        if (savedIds != null)
        {
            cardHist.AddRange(savedIds);
        }
    }
}
