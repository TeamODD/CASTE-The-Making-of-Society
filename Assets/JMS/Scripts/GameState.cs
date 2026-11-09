using System;
using System.Collections.Generic;

[Serializable]
public class GameState
{
    private Stat stats;
    private readonly List<string> flags = new();

    public Stat GetStats()
    {
        return stats;
    }

    public void SetStats(Stat value)
    {

        value.faith = Math.Min(value.faith, 100);
        value.order = Math.Min(value.order, 100);
        value.economy = Math.Min(value.economy, 100);
        value.unrest = Math.Min(value.unrest, 100);

        stats = value;
    }

    public bool HasFlag(string flag)
    {
        return flags.Contains(flag);
    }

    public void AddFlag(string flag)
    {
        if (string.IsNullOrWhiteSpace(flag))
            return;

        if (!flags.Contains(flag))
            flags.Add(flag);
    }
}