using UnityEngine;

public class StatManager : MonoBehaviour
{
    private GameState gameState = new GameState();

    public Stat GetStats()
    {
        return gameState.GetStats();
    }

    public void SetStats(Stat value)
    {
        gameState.SetStats(value);
    }

    public bool HasFlag(string flag)
    {
        return gameState.HasFlag(flag);
    }

    public void AddFlag(string flag)
    {
        gameState.AddFlag(flag);
    }

    public void ApplyDelta(Stat delta)
    {
        Stat result = GetStats() + delta;

        result.faith = Mathf.Clamp(result.faith, 0, 100);
        result.order = Mathf.Clamp(result.order, 0, 100);
        result.economy = Mathf.Clamp(result.economy, 0, 100);
        result.unrest = Mathf.Clamp(result.unrest, 0, 100);

        SetStats(result);
    }

    public void ApplyChoice(ChoiceData choice)
    {
        if (choice == null)
            return;

        ApplyDelta(choice.delta);
        AddFlag(choice.flagOnSelect);
    }
}