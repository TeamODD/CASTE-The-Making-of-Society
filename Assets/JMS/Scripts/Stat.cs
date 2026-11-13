using System;

[Serializable]
public struct Stat
{
    public int faith;
    public int order;
    public int economy;
    public int unrest;

    public static Stat Default => new Stat(50, 50, 50, 50);

    public Stat(int faith, int order, int economy, int unrest)
    {
        this.faith = faith;
        this.order = order;
        this.economy = economy;
        this.unrest = unrest;
    }

    public static Stat operator +(Stat a, Stat b)
    {
        return new Stat(
            a.faith + b.faith,
            a.order + b.order,
            a.economy + b.economy,
            a.unrest + b.unrest
        );
    }

    public Stat Clamp()
    {
        return new Stat(
            Math.Max(0, Math.Min(100, faith)),
            Math.Max(0, Math.Min(100, order)),
            Math.Max(0, Math.Min(100, economy)),
            Math.Max(0, Math.Min(100, unrest))
        );
    }

    public bool IsAtLimit()
    {
        return faith == 0 || faith == 100 || order == 0 || order == 100
            || economy == 0 || economy == 100 || unrest == 0 || unrest == 100;
    }

    public void Clear()
    {
        this = Stat.Default;
    }
}
