namespace Caste.Cards
{
    /// <summary>
    /// "다음 카드를 주는 쪽"의 약속. 지금은 덱(CardDeck)이지만,
    /// 나중에 스토리 순서·조건부 카드로 바꿔도 게임 흐름 코드는 그대로다. (DIP)
    /// </summary>
    public interface ICardProvider
    {
        CardData Next();
    }
}
