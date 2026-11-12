using Caste.CardSwipe;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Caste.Turn
{
    /// <summary>
    /// 카드를 넘길 때마다 "N / Max" 숫자를 올려서 보여준다. Max 다음에는 1로 돌아간다.
    /// 카드 코드는 수정하지 않고 CardSwipeController의 이벤트만 듣는다. (OCP)
    /// 붙이는 곳: 숫자를 보여줄 TMP 텍스트 오브젝트
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("Caste/Turn/Turn Counter View")]
    public class TurnCounterView : MonoBehaviour
    {
        public enum CountTiming
        {
            [Tooltip("손을 떼서 선택이 확정되는 순간")] OnCommit,
            [Tooltip("카드가 화면 밖으로 나가고 새 카드가 나올 때")] OnNextCard
        }

        [SerializeField] private CardSwipeController _swipe;

        [Header("숫자")]
        [Tooltip("한 바퀴의 최대값. 이 숫자 다음에는 1로 돌아간다.")]
        [Min(1)] [SerializeField] private int _max = 12;
        [Tooltip("게임 시작 시 숫자")]
        [Min(1)] [SerializeField] private int _startValue = 1;
        [Tooltip("언제 숫자를 올릴지")]
        [SerializeField] private CountTiming _timing = CountTiming.OnNextCard;

        [Header("표시 형식")]
        [Tooltip("{0} = 현재 숫자, {1} = 최대값. 예: \"{0}/{1}\", \"{0}월\", \"{0} / {1} 턴\"")]
        [SerializeField] private string _format = "{0}/{1}";

        [Header("이벤트")]
        [Tooltip("최대값을 넘어 1로 돌아갈 때 호출 (예: 1년 경과 연출)")]
        public UnityEvent onLooped;

        public LoopingCounter Counter { get; private set; }

        private TMP_Text _text;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            Counter = new LoopingCounter(_max, _startValue);
            Counter.Changed += HandleChanged;
            Counter.Looped += onLooped.Invoke;
            HandleChanged(Counter.Current);

            if (_swipe == null)
                Debug.LogError($"[TurnCounter] {name}: CardSwipeController가 연결되지 않았습니다.", this);
        }

        private void OnEnable()
        {
            if (_swipe == null) return;
            _swipe.ChoiceCommitted += HandleCommitted;
            _swipe.ChoiceSelected += HandleSelected;
        }

        private void OnDisable()
        {
            if (_swipe == null) return;
            _swipe.ChoiceCommitted -= HandleCommitted;
            _swipe.ChoiceSelected -= HandleSelected;
        }

        // 에디터에서 형식 / 숫자를 바꾸면 미리보기
        private void OnValidate()
        {
            var text = GetComponent<TMP_Text>();
            if (text != null && !Application.isPlaying)
                text.text = string.Format(_format, Mathf.Clamp(_startValue, 1, _max), _max);
        }

        private void HandleCommitted(SwipeDirection _)
        {
            if (_timing == CountTiming.OnCommit) Counter.Increment();
        }

        private void HandleSelected(SwipeDirection _)
        {
            if (_timing == CountTiming.OnNextCard) Counter.Increment();
        }

        private void HandleChanged(int value) => _text.text = string.Format(_format, value, Counter.Max);
    }
}
