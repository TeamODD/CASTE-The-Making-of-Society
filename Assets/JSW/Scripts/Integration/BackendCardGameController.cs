namespace Caste.Integration
{
    // using을 namespace 안에 둔다: 전역 CardData(JMS)보다 Caste.Cards.CardData를 먼저 찾게 하기 위함.
    using System;
    using Caste.Cards;
    using Caste.CardSwipe;
    using Caste.Stats;
    using Caste.Tutorial;
    using UnityEngine;
    using UnityEngine.Events;
    using UnityEngine.Serialization;
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

        [Header("튜토리얼 (선택)")]
        [Tooltip("카드를 보여주기 전에 끼어들 컴포넌트 (ICardRevealGate 구현, 예: TutorialDirector). 비워두면 튜토리얼 없이 진행")]
        [SerializeField] private MonoBehaviour _revealGate;

        [Header("이벤트 (팝업·연출 연결용)")]
        [Tooltip("계층이 바뀌었을 때 (이전 계층 순서, 새 계층 순서)")]
        public UnityEvent<int, int> onCasteChanged;
        [Tooltip("카드가 화면에 나올 때마다 그 카드의 계층 코드(B, K, V, S, C)를 넘긴다 → CasteEmblemView.ShowCaste에 연결")]
        public UnityEvent<string> onCasteCodeShown;
        [Tooltip("카드가 화면에 나올 때마다 그 카드의 계층 이름(브라만, 크샤트리아 ...)을 넘긴다 → CasteNameView.ShowName에 연결")]
        public UnityEvent<string> onCasteNameShown;
        [Tooltip("카드가 화면에 나올 때마다 (계층 코드, 계층 이름)을 함께 넘긴다 → CasteChangeAnnouncer.OnCasteShown에 연결")]
        public UnityEvent<string, string> onCasteShown;
        [Tooltip("마지막 계층까지 끝나서 더 낼 카드가 없을 때")]
        public UnityEvent onGameFinished;
        [Tooltip("엔딩이 나왔을 때 엔딩 문장을 넘긴다 → EndingScreenController.ShowText에 연결")]
        public UnityEvent<string> onEndingReached;
        [Tooltip("엔딩이 나왔을 때 엔딩 문장 + 일러스트를 넘긴다 → EndingScreenController.ShowEnding에 연결 (일러스트 없으면 null)")]
        public UnityEvent<string, Sprite> onEndingWithArtReached;
        [Tooltip("엔딩을 다 본 뒤 (게임오버·완주 상관없이) → 타이틀 씬 로드 등에 연결")]
        [FormerlySerializedAs("onCompletionFinished")]
        public UnityEvent onReturnToTitle;

        public StatModel Stats { get; private set; }

        private JmsCardConverter _converter;
        private bool _cardInFlight; // 카드가 날아가는 중에는 내용 교체를 미룬다
        private ICardRevealGate _gate;
        private int _choicesMade;   // 이번 판에서 고른 횟수 (튜토리얼 시점 판단용)

        private void Awake()
        {
            Stats = new StatModel(_stats);
            _converter = new JmsCardConverter(_stats, _portraits);
            _statPanel.Bind(Stats);

            _gate = _revealGate as ICardRevealGate;
            if (_revealGate != null && _gate == null)
                Debug.LogWarning($"[Backend] {_revealGate.name}은(는) ICardRevealGate를 구현하지 않아 튜토리얼이 연결되지 않습니다.", this);

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
            // 저장은 있지만 불러오지 못한 경우(예: CSV가 바뀌어 저장된 카드가 사라짐) → 새 게임으로
            if (_gameManager.State == null) StartNewGame();
        }

        // 백엔드 호출보다 먼저 BeginRun: 백엔드가 즉시 상태 변경을 알려 카드 표시가 바로 일어나기 때문
        public void StartNewGame() { BeginRun(true); Run(_gameManager.StartNewGame); }
        public void ResumeGame() { BeginRun(false); Run(_gameManager.ResumeGame); }
        public void BackToCheckpoint() { BeginRun(false); Run(_gameManager.BackToStart); }

        private void BeginRun(bool isNewGame)
        {
            _choicesMade = 0;
            _gate?.BeginRun(isNewGame);
        }

        // ───────── 입력 → 백엔드 ─────────

        // 확정 즉시 백엔드에 전달 → 스탯 변경이 바로 오므로 카드가 날아가는 동안 게이지가 움직인다
        private void OnChoiceCommitted(SwipeDirection dir)
        {
            _cardInFlight = true;
            _choicesMade++;
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

            var raw = state.GetCurrentCard();

            // 튜토리얼 등이 끼어들면 카드 표시를 미루고, 끝나면 이 메서드를 다시 불러준다
            if (raw != null && _gate != null && _gate.TryHold(_choicesMade, ShowCurrentCard))
            {
                _swipe.enabled = false;
                return;
            }

            CardData card = _converter.Convert(raw);
            _cardView.Show(card);
            _statPanel.SetCard(card);
            _swipe.enabled = card != null;

            // 스와이프 활성화 뒤에 알린다 → 연출 쪽에서 조작을 잠가도 바로 다시 켜지지 않는다
            if (raw != null)
            {
                onCasteCodeShown?.Invoke(raw.casteCode);
                onCasteNameShown?.Invoke(raw.casteName);
                onCasteShown?.Invoke(raw.casteCode, raw.casteName);
            }

            if (card != null) return;

            // 더 낼 카드가 없음 = 게임 종료. 백엔드가 고른 엔딩 문장을 엔딩 화면으로 넘긴다.
            var ending = state.Ending;
            if (ending != null)
            {
                onEndingWithArtReached?.Invoke(ending.Text, global::EndingArt.Load(ending.Id));
                onEndingReached?.Invoke(ending.Text);
            }
            onGameFinished?.Invoke();
        }

        /// <summary>
        /// 엔딩 화면을 다 본 뒤 호출 (EndingScreenController.onEndingFinished에 연결 가능).
        /// 게임오버·완주 상관없이 무조건 타이틀로 보낸다. (체크포인트 복귀 안 함 — 2026-11-17 결정)
        /// </summary>
        public void ContinueAfterEnding()
        {
            var state = _gameManager.State;
            if (state == null || !state.IsFinished) return;
            onReturnToTitle?.Invoke();
        }

        // 백엔드는 저장 실패를 예외로 알려준다. 진행 상태는 유지되므로 같은 선택을 다시 시도할 수 있다.
        private void Run(Action action)
        {
            try { action(); }
            catch (Exception e) { Debug.LogException(e, this); }
        }
    }
}
