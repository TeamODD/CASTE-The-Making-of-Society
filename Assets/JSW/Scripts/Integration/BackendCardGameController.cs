namespace Caste.Integration
{
    // using을 namespace 안에 둔다: 전역 CardData(JMS)보다 Caste.Cards.CardData를 먼저 찾게 하기 위함.
    using System;
    using Caste.Cards;
    using Caste.CardSwipe;
    using Caste.Stats;
    using UnityEngine;
    using UnityEngine.Events;
    using BackendManager = global::GameManager;
    using BackendState = global::GameState;

    /// <summary>
    /// 프론트(JSW 스와이프·카드·게이지)와 백엔드(JMS GameManager)를 잇는 어댑터.
    /// 규칙 계산과 저장은 하지 않는다. 입력을 백엔드에 넘기고, 백엔드가 확정한 카드와 스탯을 화면에 보여주기만 한다.
    /// (프로토타입용 CardGameController는 그대로 두고, 씬에서 이 컴포넌트로 바꿔 끼운다 → OCP)
    /// </summary>
    public class BackendCardGameController : MonoBehaviour
    {
        [Header("백엔드 (JMS)")]
        [SerializeField] private BackendManager _gameManager;
        [Tooltip("시작할 때 자동으로 게임을 연다")]
        [SerializeField] private bool _autoStart = true;
        [Tooltip("켜면: 저장이 있으면 이어하기, 없으면 새 게임 / 끄면: 항상 새 게임")]
        [SerializeField] private bool _resumeIfPossible = true;

        [Header("프론트 (JSW)")]
        [Tooltip("각 에셋의 Key(Faith/Order/Economy/Unrest)가 지정돼 있어야 값이 연결된다")]
        [SerializeField] private StatDefinition[] _stats;
        [SerializeField] private CardSwipeController _swipe;
        [SerializeField] private CardView _cardView;
        [SerializeField] private StatPanelView _statPanel;
        [Tooltip("(선택) 화자 → 초상화 표")]
        [SerializeField] private PortraitTable _portraits;

        [Header("이벤트 (팝업·연출 연결용)")]
        [Tooltip("계층이 바뀌었을 때 (이전 계층 순서, 새 계층 순서)")]
        public UnityEvent<int, int> onCasteChanged;
        [Tooltip("마지막 계층까지 끝나서 더 낼 카드가 없을 때")]
        public UnityEvent onGameFinished;

        public StatModel Stats { get; private set; }

        private JmsCardConverter _converter;
        private bool _cardInFlight; // 카드가 날아가는 중에는 내용 교체를 미룬다

        private void Awake()
        {
            Stats = new StatModel(_stats);
            _converter = new JmsCardConverter(_stats, _portraits);
            _statPanel.Bind(Stats);

            foreach (var stat in _stats)
                if (stat != null && stat.Key == StatKey.None)
                    Debug.LogWarning($"[Backend] {stat.name}의 Key가 None이라 백엔드 값과 연결되지 않습니다.", stat);
        }

        private void OnEnable()
        {
            _swipe.ChoiceCommitted += OnChoiceCommitted;
            _swipe.ChoiceSelected += OnChoiceSelected;
            _gameManager.GameStateChanged += OnGameStateChanged;
            _gameManager.CasteChanged += OnCasteChanged;
        }

        private void OnDisable()
        {
            _swipe.ChoiceCommitted -= OnChoiceCommitted;
            _swipe.ChoiceSelected -= OnChoiceSelected;
            if (_gameManager == null) return;
            _gameManager.GameStateChanged -= OnGameStateChanged;
            _gameManager.CasteChanged -= OnCasteChanged;
        }

        private void Start()
        {
            if (_autoStart) OpenGame();
        }

        private void OnDestroy() => _converter?.Clear();

        // ───────── 버튼에서 호출할 수 있는 공개 메서드 ─────────

        public void OpenGame()
        {
            if (_resumeIfPossible && _gameManager.CanResume) ResumeGame();
            else StartNewGame();
        }

        public void StartNewGame() => Run(_gameManager.StartNewGame);
        public void ResumeGame() => Run(_gameManager.ResumeGame);
        public void BackToCheckpoint() => Run(_gameManager.BackToStart);

        // ───────── 입력 → 백엔드 ─────────

        // 확정 즉시 백엔드에 전달 → 스탯 변경이 바로 오므로 카드가 날아가는 동안 게이지가 움직인다
        private void OnChoiceCommitted(SwipeDirection dir)
        {
            _cardInFlight = true;
            Run(dir == SwipeDirection.Left ? _gameManager.ChooseLeft : _gameManager.ChooseRight);
        }

        // 카드가 화면 밖으로 나간 뒤에 새 카드 내용으로 교체
        private void OnChoiceSelected(SwipeDirection dir)
        {
            _cardInFlight = false;
            ShowCurrentCard();
        }

        // ───────── 백엔드 → 화면 ─────────

        private void OnGameStateChanged(BackendState state)
        {
            if (state == null) return;
            SyncStats(state);
            if (!_cardInFlight) ShowCurrentCard();
        }

        private void OnCasteChanged(int previous, int next) => onCasteChanged?.Invoke(previous, next);

        private void SyncStats(BackendState state)
        {
            var values = state.GetStats();
            foreach (var stat in _stats)
                if (stat != null && stat.Key != StatKey.None)
                    Stats.Set(stat, JmsCardConverter.Read(values, stat.Key));
        }

        private void ShowCurrentCard()
        {
            var state = _gameManager.State;
            if (state == null) return; // 아직 게임이 열리지 않음

            CardData card = _converter.Convert(state.GetCurrentCard());
            _cardView.Show(card);
            _statPanel.SetCard(card);

            if (card == null)
            {
                _swipe.enabled = false;
                onGameFinished?.Invoke();
            }
        }

        // 백엔드는 저장 실패를 예외로 알려준다. 진행 상태는 유지되므로 같은 선택을 다시 시도할 수 있다.
        private void Run(Action action)
        {
            try { action(); }
            catch (Exception e) { Debug.LogException(e, this); }
        }
    }
}
