using UnityEngine;

namespace Caste.Cards
{
    /// <summary>카드 목록에서 순서대로(또는 무작위로) 한 장씩 꺼낸다. 끝나면 처음부터 반복.</summary>
    public class CardDeck : MonoBehaviour, ICardProvider
    {
        [SerializeField] private CardData[] _cards;
        [Tooltip("켜면 무작위 (같은 카드 연속 방지)")]
        [SerializeField] private bool _shuffle;

        private int _index = -1;

        public CardData Next()
        {
            if (_cards == null || _cards.Length == 0) return null;

            if (_shuffle && _cards.Length > 1)
            {
                int next;
                do next = Random.Range(0, _cards.Length); while (next == _index);
                _index = next;
            }
            else
            {
                _index = (_index + 1) % _cards.Length;
            }
            return _cards[_index];
        }
    }
}
