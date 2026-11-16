using System;
using Newtonsoft.Json;
using UnityEngine.Serialization;

[Serializable]
public struct Stat
{
    public int faith;
    public int order;
    public int economy;
    [FormerlySerializedAs("unrest")]
    [JsonProperty("unrest")]
    public int publicOp;

    /// <summary>
    /// 기존 화면 코드에서 사용하는 민심 스탯의 호환 이름입니다.
    /// </summary>
    [JsonIgnore]
    public int unrest
    {
        get => publicOp;
        set => publicOp = value;
    }

    public static Stat Default => new Stat(50, 50, 50, 50);

    /// <summary>
    /// 신앙, 치안, 경제, 민심 스탯을 전달받은 값으로 설정합니다.
    /// </summary>
    public Stat(int faith, int order, int economy, int publicOp)
    {
        this.faith = faith;
        this.order = order;
        this.economy = economy;
        this.publicOp = publicOp;
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
            a.publicOp + b.publicOp
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
            Math.Max(0, Math.Min(100, publicOp))
        );
    }

    /// <summary>
    /// 네 스탯을 각각 기본값 50으로 되돌립니다.
    /// </summary>
    public void Clear()
    {
        this = Stat.Default;
    }
}
