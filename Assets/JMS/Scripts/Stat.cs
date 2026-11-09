using System;

[Serializable]
public struct Stat
{
    public int faith;
    public int order;
    public int economy;
    public int unrest;

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
}