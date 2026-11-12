using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Caste.UI
{
    /// <summary>
    /// 어두운 배경 + 가운데 패널로 된 모달 팝업의 "열기 / 닫기 / 표시"만 담당한다. (SRP)
    /// 버튼에 어떤 기능이 붙는지는 모른다 → 각 Controller 담당.
    /// 팝업이 여러 개 겹쳐도 뒤로가기는 "맨 위" 팝업만 닫는다.
    /// 붙이는 곳: 팝업 루트 (화면 전체 크기, CanvasGroup 자동 추가)
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class PopupView : MonoBehaviour
    {
        [Header("구성 요소")]
        [Tooltip("화면 전체를 덮는 어두운 배경 Image")]
        [SerializeField] private Image _dimImage;
        [Tooltip("가운데에 뜨는 패널 (열릴 때 살짝 커지는 연출용)")]
        [SerializeField] private RectTransform _panel;

        [Header("배경 어둡기")]
        [Tooltip("배경 색 (보통 검정)")]
        [SerializeField] private Color _dimColor = Color.black;
        [Tooltip("0 = 투명, 1 = 완전히 가림")]
        [Range(0f, 1f)]
        [SerializeField] private float _dimAlpha = 0.6f;

        [Header("연출")]
        [Tooltip("열기/닫기 애니메이션 시간(초). 0이면 즉시")]
        [Min(0f)]
        [SerializeField] private float _animDuration = 0.15f;
        [Tooltip("열릴 때 패널 시작 크기 비율")]
        [Range(0.5f, 1f)]
        [SerializeField] private float _panelStartScale = 0.9f;

        [Header("닫기 방식")]
        [Tooltip("안드로이드 뒤로가기(PC는 ESC)로 닫기")]
        [SerializeField] private bool _closeOnBackButton = true;

        [Header("이벤트 (애니메이션이 끝난 뒤 호출)")]
        public UnityEvent onOpened;
        public UnityEvent onClosed;

        /// <summary>열기 직전 / 닫기 직전에 호출 (코드용). 값 새로고침, 저장 등에 사용.</summary>
        public event Action Opening;
        public event Action Closing;

        // 열린 순서대로 쌓아서 뒤로가기는 맨 위만 닫는다
        private static readonly List<PopupView> s_openStack = new List<PopupView>();
        private static int s_lastBackFrame = -1;

        /// <summary>열려 있는 팝업 수. UI 이벤트를 안 쓰는 입력(키보드 등)을 막을 때 확인용.</summary>
        public static int OpenCount => s_openStack.Count;
        public static bool IsAnyOpen => s_openStack.Count > 0;

        public bool IsOpen { get; private set; }
        private bool IsTop => s_openStack.Count > 0 && s_openStack[s_openStack.Count - 1] == this;

        private CanvasGroup _canvasGroup;
        private Coroutine _animRoutine;

        // Enter Play Mode 설정에서 도메인 리로드를 꺼도 이전 기록이 남지 않게
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_openStack.Clear();
            s_lastBackFrame = -1;
        }

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            ApplyDim();
            SetVisibleImmediate(false);
        }

        // 인스펙터에서 값을 바꾸면 바로 반영 (플레이 중에도)
        private void OnValidate() => ApplyDim();

        private void OnDisable()
        {
            if (!IsOpen) return;
            IsOpen = false;
            s_openStack.Remove(this);
        }

        private void Update()
        {
            if (!IsOpen || !_closeOnBackButton || !IsTop) return;
            // 같은 프레임에 아래 팝업까지 연달아 닫히는 것 방지
            if (s_lastBackFrame == Time.frameCount || !BackPressed()) return;

            s_lastBackFrame = Time.frameCount;
            Close();
        }

        public void Open()
        {
            if (IsOpen) return;
            Opening?.Invoke();
            IsOpen = true;
            s_openStack.Remove(this);
            s_openStack.Add(this);
            transform.SetAsLastSibling(); // 다른 UI보다 위에 그리기
            Animate(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            Closing?.Invoke();
            IsOpen = false;
            s_openStack.Remove(this);
            Animate(false);
        }

        public void Toggle()
        {
            if (IsOpen) Close(); else Open();
        }

        private void ApplyDim()
        {
            if (_dimImage == null) return;
            Color c = _dimColor;
            c.a = _dimAlpha;
            _dimImage.color = c;
            _dimImage.raycastTarget = true; // 뒤쪽 UI(카드 드래그 포함) 터치 차단
        }

        private void Animate(bool open)
        {
            if (_animRoutine != null) StopCoroutine(_animRoutine);

            if (_animDuration <= 0f || !gameObject.activeInHierarchy)
            {
                SetVisibleImmediate(open);
                (open ? onOpened : onClosed)?.Invoke();
                return;
            }

            _animRoutine = StartCoroutine(AnimateRoutine(open));
        }

        private IEnumerator AnimateRoutine(bool open)
        {
            _canvasGroup.blocksRaycasts = true;   // 애니메이션 중에도 뒤쪽 터치 차단
            _canvasGroup.interactable = false;    // 버튼 연타 방지

            float from = _canvasGroup.alpha;
            float to = open ? 1f : 0f;
            float t = 0f;

            while (t < _animDuration)
            {
                // timeScale = 0 이어도 동작하도록 unscaled 사용
                t += Time.unscaledDeltaTime;
                float a = Mathf.Lerp(from, to, Mathf.Clamp01(t / _animDuration));
                _canvasGroup.alpha = a;
                if (_panel != null)
                    _panel.localScale = Vector3.one * Mathf.Lerp(_panelStartScale, 1f, a);
                yield return null;
            }

            SetVisibleImmediate(open);
            _animRoutine = null;
            (open ? onOpened : onClosed)?.Invoke();
        }

        private void SetVisibleImmediate(bool visible)
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
            if (_panel != null)
                _panel.localScale = Vector3.one * (visible ? 1f : _panelStartScale);
        }

#if ENABLE_INPUT_SYSTEM
        private static bool BackPressed()
        {
            var kb = Keyboard.current;   // 안드로이드 뒤로가기는 Escape로 들어온다
            return kb != null && kb.escapeKey.wasPressedThisFrame;
        }
#else
        private static bool BackPressed() => Input.GetKeyDown(KeyCode.Escape);
#endif
    }
}
