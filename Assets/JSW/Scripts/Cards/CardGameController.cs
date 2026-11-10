using Caste.CardSwipe;
using Caste.Stats;
using UnityEngine;

namespace Caste.Cards
{
    /// <summary>
    /// 게임 한 판의 흐름: 카드 표시 → 선택 확정 → 스탯 반영 → 다음 카드.
    /// 각 부품(스와이프, 스탯 계산, 화면 표시, 카드 공급)을 연결만 하고, 직접 계산하거나 그리지 않는다.
    /// </summary>
    public class CardGameController : MonoBehaviour
    {
        [SerializeField] private StatDefinition[] _stats;
        [SerializeField] private CardSwipeController _swipe;
        [SerializeField] private CardView _cardView;
        [SerializeField] private StatPanelView _statPanel;
        [Tooltip("ICardProvider를 구현한 컴포넌트 (CardDeck 등)")]
        [SerializeField] private MonoBehaviour _cardSource;

        public StatModel Stats { get; private set; }

        private ICardProvider _cards;
        private CardData _current;

        private void Awake()
        {
            Stats = new StatModel(_stats);
            _cards = _cardSource as ICardProvider;
            if (_cards == null) Debug.LogError("[CardGame] Card Source가 ICardProvider가 아닙니다.", this);
            _statPanel.Bind(Stats);
        }

        private void OnEnable()
        {
            _swipe.ChoiceCommitted += OnChoiceCommitted;
            _swipe.ChoiceSelected += OnChoiceSelected;
        }

        private void OnDisable()
        {
            _swipe.ChoiceCommitted -= OnChoiceCommitted;
            _swipe.ChoiceSelected -= OnChoiceSelected;
        }

        private void Start() => ShowNextCard();

        // 확정 즉시 스탯 반영 → 카드가 날아가는 동안 게이지가 미리보기에서 이어서 차오른다
        private void OnChoiceCommitted(SwipeDirection dir)
        {
            if (_current != null) Stats.Apply(_current.GetChoice(dir).Effects);
        }

        // 카드가 화면 밖으로 나간 뒤 내용 교체 → 새 카드 등장
        private void OnChoiceSelected(SwipeDirection dir) => ShowNextCard();

        private void ShowNextCard()
        {
            _current = _cards?.Next();
            _cardView.Show(_current);
            _statPanel.SetCard(_current);
        }
    }
}
