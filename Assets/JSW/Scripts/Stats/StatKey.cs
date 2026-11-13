namespace Caste.Stats
{
    /// <summary>
    /// 화면용 스탯(StatDefinition 에셋)이 백엔드 스탯 값의 어느 칸과 연결되는지.
    /// 프론트는 백엔드 타입을 직접 알지 않고 이 키로만 연결한다.
    /// </summary>
    public enum StatKey
    {
        None,
        Faith,   // 신앙
        Order,   // 치안
        Economy, // 재정
        Unrest,  // 불만
    }
}
