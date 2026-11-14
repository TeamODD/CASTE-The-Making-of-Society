using System;

[Serializable]
public struct Stat
{
    public int faith;
    public int order;
    public int economy;
    public int unrest;

    public static Stat Default => new Stat(50, 50, 50, 50);

    /// <summary>
    /// 신앙, 치안, 경제, 불만 스탯을 전달받은 값으로 설정합니다.
    /// </summary>
    public Stat(int faith, int order, int economy, int unrest)
    {
        this.faith = faith;
        this.order = order;
        this.economy = economy;
        this.unrest = unrest;
    }

    /// <summary>
    /// 두 스탯 묶음에서 같은 항목끼리 더해 합산값을 반환합니다.
    /// </summary>
    public static Stat operator +(Stat a, Stat b)
    {
        return new Stat(
            a.faith + b.faith,
            a.order + b.order,
            a.economy + b.economy,
            a.unrest + b.unrest
        );
    }

    /// <summary>
    /// 각 스탯을 0~100 범위로 조정한 값을 반환합니다.
    /// </summary>
    public Stat Clamp()
    {
        return new Stat(
            Math.Max(0, Math.Min(100, faith)),
            Math.Max(0, Math.Min(100, order)),
            Math.Max(0, Math.Min(100, economy)),
            Math.Max(0, Math.Min(100, unrest))
        );
    }

    /// <summary>
    /// 스탯 중 하나라도 0 또는 100인지 확인합니다.
    /// </summary>
    public bool IsAtLimit()
    {
        return faith == 0 || faith == 100 || order == 0 || order == 100
            || economy == 0 || economy == 100 || unrest == 0 || unrest == 100;
    }

    /// <summary>
    /// 네 스탯을 각각 기본값 50으로 되돌립니다.
    /// </summary>
    public void Clear()
    {
        this = Stat.Default;
    }
}
