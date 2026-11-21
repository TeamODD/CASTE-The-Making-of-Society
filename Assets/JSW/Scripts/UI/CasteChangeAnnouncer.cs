using System.Collections;
using Caste.Endings;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Caste.UI
{
    /// <summary>
    /// 계급이 바뀌는 순간의 "상승" 연출.
    ///   ① 화면이 어두워지고 → ② 아래에서 상징물이 후광과 함께 떠오르며 튀어나오고
    ///   → ③ 문구와 계급 이름이 나타났다가 → ④ 상징물이 왼쪽 아래 자리로 날아가 들어간다.
    /// 계급이 바뀌었는지는 직접 판단한다 (같은 계급이면 무시, 게임을 처음 열 때의 첫 카드도 무시).
    /// 연출만 담당하고 게임 진행은 모른다 → BackendCardGameController.onCasteShown에 OnCasteShown을 연결해서 쓴다. (SRP, DIP)
    /// </summary>
    [AddComponentMenu("Caste/UI/Caste Change Announcer")]
    public class CasteChangeAnnouncer : MonoBehaviour
    {
        [Header("연결")]
        [Tooltip("계층 코드 → 상징물 표 (예: Data/CasteEmblemTable)")]
        [SerializeField] private ScriptableObject _emblemSource;
        [Tooltip("연출 전체를 묶는 CanvasGroup (투명도 조절 + 연출 중 클릭 차단)")]
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private Image _glow;
        [SerializeField] private Image _emblem;
        [SerializeField] private TMP_Text _title;
        [SerializeField] private TMP_Text _casteName;
        [Tooltip("상징물이 마지막에 날아가 들어갈 자리 (왼쪽 아래 상징물). 비우면 제자리에서 사라진다")]
        [SerializeField] private RectTransform _flyTarget;
        [Tooltip("연출 중 꺼둘 컴포넌트 (예: CardSwipeController → 카드 조작 잠금)")]
        [SerializeField] private Behaviour[] _disableWhilePlaying;

        [Header("문구")]
        [SerializeField] private string _titleText = "계급이 상승했습니다";
        [Tooltip("{0} = 계급 이름")]
        [SerializeField] private string _nameFormat = "[ {0} ]";

        [Header("시간 (초)")]
        [SerializeField, Min(0f)] private float _fadeInTime = 0.3f;
        [SerializeField, Min(0.01f)] private float _riseTime = 0.7f;
        [SerializeField, Min(0f)] private float _holdTime = 1.1f;
        [SerializeField, Min(0.01f)] private float _flyTime = 0.55f;

        [Header("움직임")]
        [Tooltip("상징물이 얼마나 아래에서 떠오르는지 (px)")]
        [SerializeField] private float _riseDistance = 260f;
        [Tooltip("튀어나올 때 커지는 정도 (1 = 튀지 않음)")]
        [SerializeField, Range(1f, 1.6f)] private float _overshoot = 1.15f;
        [Tooltip("후광 회전 속도 (도/초)")]
        [SerializeField] private float _glowSpinSpeed = 25f;

        [Header("색")]
        [Tooltip("후광 색 (투명도 = 최대 밝기)")]
        [SerializeField] private Color _glowColor = new(1f, 0.82f, 0.4f, 0.85f);
        [Tooltip("상징물이 나타나는 순간 번쩍이는 색 → 원래 색으로 돌아온다")]
        [SerializeField] private Color _flashColor = new(1f, 0.95f, 0.7f, 1f);
        [Tooltip("화면 어둡게 하는 정도 (Dim 이미지 색은 Dim 오브젝트에서)")]
        [SerializeField, Range(0f, 1f)] private float _maxGroupAlpha = 1f;

        [Header("이벤트")]
        public UnityEvent onStarted;
        public UnityEvent onFinished;

        [Header("테스트 (Play 중 컴포넌트 ⋮ 메뉴 > 연출 미리보기)")]
        [SerializeField] private string _testCode = "K";
        [SerializeField] private string _testName = "크샤트리아";

        private ICasteEmblemProvider _emblems;
        private string _currentCode;
        private Coroutine _playing;
        private RectTransform _emblemRect;
        private Vector2 _emblemHomePos, _emblemHomeSize;

        private void Awake()
        {
            _emblems = _emblemSource as ICasteEmblemProvider;
            if (_emblems == null)
                Debug.LogError($"[CasteChange] {name}: Emblem Source가 ICasteEmblemProvider가 아닙니다.", this);

            _emblemRect = _emblem.rectTransform;
            _emblemHomePos = _emblemRect.anchoredPosition;
            _emblemHomeSize = _emblemRect.sizeDelta;
            Hide();
        }

        /// <summary>카드가 나올 때마다 호출. 계급이 바뀌었을 때만 연출한다.</summary>
        public void OnCasteShown(string casteCode, string casteName)
        {
            if (string.IsNullOrEmpty(casteCode) || casteCode == _currentCode) return;
            bool first = _currentCode == null;
            _currentCode = casteCode;
            if (!first) Play(casteCode, casteName);
        }

        public void Play(string casteCode, string casteName)
        {
            if (_playing != null) StopCoroutine(_playing);
            _playing = StartCoroutine(PlayRoutine(casteCode, casteName));
        }

        [ContextMenu("연출 미리보기")]
        private void PlayTest()
        {
            if (!Application.isPlaying) { Debug.LogWarning("[CasteChange] Play 중에만 미리보기할 수 있습니다."); return; }
            Play(_testCode, _testName);
        }

        private IEnumerator PlayRoutine(string code, string casteName)
        {
            SetLocked(true);
            onStarted?.Invoke();

            _emblem.sprite = _emblems?.GetEmblem(code);
            _emblem.preserveAspect = true;
            _emblem.enabled = _emblem.sprite != null;
            _title.text = _titleText;
            _casteName.text = string.Format(_nameFormat, casteName);
            _emblemRect.anchoredPosition = _emblemHomePos - new Vector2(0f, _riseDistance);
            _emblemRect.sizeDelta = _emblemHomeSize;
            _emblemRect.localScale = Vector3.zero;
            SetAlpha(_glow, 0f);
            SetAlpha(_title, 0f);
            SetAlpha(_casteName, 0f);
            _group.gameObject.SetActive(true);
            _group.blocksRaycasts = true;

            // ① 화면 어둡게
            yield return Tween(_fadeInTime, t => _group.alpha = t * _maxGroupAlpha);

            // ② 상징물이 아래에서 떠오르며 튀어나옴 + 후광 + 번쩍임
            Color emblemBase = Color.white;
            yield return Tween(_riseTime, t =>
            {
                float rise = EaseOutCubic(t);
                _emblemRect.anchoredPosition = _emblemHomePos - new Vector2(0f, _riseDistance * (1f - rise));
                _emblemRect.localScale = Vector3.one * EaseOutBack(t, _overshoot);
                _emblem.color = Color.Lerp(_flashColor, emblemBase, t);
                _glow.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1f, rise);
                SetAlpha(_glow, _glowColor.a * rise);
                SetAlpha(_title, Mathf.InverseLerp(0.4f, 1f, t));
                SetAlpha(_casteName, Mathf.InverseLerp(0.55f, 1f, t));
            });

            // ③ 잠시 보여줌
            yield return Tween(_holdTime, _ => { });

            // ④ 상징물이 왼쪽 아래 자리로 날아가며 나머지는 사라짐
            Vector3 fromPos = _emblemRect.position;
            Vector2 fromSize = _emblemRect.sizeDelta;
            Vector2 toSize = _flyTarget != null ? _flyTarget.rect.size : Vector2.zero;
            yield return Tween(_flyTime, t =>
            {
                float e = EaseInCubic(t);
                if (_flyTarget != null)
                {
                    _emblemRect.position = Vector3.Lerp(fromPos, _flyTarget.TransformPoint(_flyTarget.rect.center), e);
                    _emblemRect.sizeDelta = Vector2.Lerp(fromSize, toSize, e);
                }
                else
                {
                    _emblemRect.localScale = Vector3.one * (1f - e);
                }
                float fade = 1f - t;
                SetAlpha(_glow, _glowColor.a * fade);
                SetAlpha(_title, fade);
                SetAlpha(_casteName, fade);
                _group.alpha = Mathf.Lerp(_maxGroupAlpha, 0f, e);
                SetAlpha(_emblem, 1f - Mathf.InverseLerp(0.8f, 1f, t)); // 자리에 닿는 순간 자연스럽게 사라짐
            });

            Hide();
            SetLocked(false);
            onFinished?.Invoke();
            _playing = null;
        }

        private void Update()
        {
            if (_group.gameObject.activeSelf)
                _glow.rectTransform.Rotate(0f, 0f, -_glowSpinSpeed * Time.unscaledDeltaTime);
        }

        private void Hide()
        {
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.gameObject.SetActive(false);
            _emblemRect.anchoredPosition = _emblemHomePos;
            _emblemRect.sizeDelta = _emblemHomeSize;
            _emblem.color = Color.white;
            _glow.color = _glowColor;
        }

        private void SetLocked(bool locked)
        {
            if (_disableWhilePlaying == null) return;
            foreach (var b in _disableWhilePlaying)
                if (b != null) b.enabled = !locked;
        }

        private void OnDisable()
        {
            // 연출 도중 씬이 바뀌거나 꺼져도 조작이 잠긴 채로 남지 않게
            if (_playing == null) return;
            _playing = null;
            SetLocked(false);
        }

        private static IEnumerator Tween(float duration, System.Action<float> step)
        {
            for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                step(time / duration);
                yield return null;
            }
            step(1f);
        }

        private static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        private static float EaseInCubic(float t) => t * t * t;

        // 목표를 살짝 넘었다가(overshoot) 돌아오는 곡선. overshoot = 최대 크기 비율
        private static float EaseOutBack(float t, float overshoot)
        {
            float s = (overshoot - 1f) * 10f;
            float x = t - 1f;
            return 1f + x * x * ((s + 1f) * x + s);
        }

        private static void SetAlpha(Graphic g, float a)
        {
            Color c = g.color;
            c.a = a;
            g.color = c;
        }
    }
}
