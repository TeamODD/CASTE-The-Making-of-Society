using Caste.Effects;
using Caste.UI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Caste.Ending
{
    /// <summary>
    /// 엔딩 화면: 검은 배경 위에 문장을 타자기로 한 줄(페이지)씩 보여준다.
    /// - 화면 표시/입력 차단은 PopupView에 맡긴다. (팝업이 열려 있으면 카드 스와이프·키보드 선택이 자동으로 막힘)
    /// - 글자 공개는 TypewriterEffect에 맡긴다.
    /// → 이 클래스는 "엔딩 문장 진행 순서"만 담당한다. (SRP)
    /// 붙이는 곳: 엔딩 화면 루트 (PopupView와 같은 오브젝트)
    /// </summary>
    [AddComponentMenu("Caste/Ending/Ending Screen Controller")]
    public class EndingScreenController : MonoBehaviour, IPointerClickHandler
    {
        [Header("구성 요소")]
        [Tooltip("검은 배경 팝업. Dim Alpha = 1 이면 완전한 검은 화면")]
        [SerializeField] private PopupView _popup;
        [Tooltip("엔딩 문장이 출력될 텍스트의 TypewriterEffect")]
        [SerializeField] private TypewriterEffect _typewriter;

        [Header("엔딩 문장 (한 칸 = 한 페이지)")]
        [TextArea(2, 6)]
        [SerializeField] private string[] _lines =
        {
            "그렇게 한 시대가 저물었다.",
            "사람들은 그 시절을 기억할까.",
            "- END -"
        };

        [Header("진행 방식")]
        [Tooltip("화면을 터치/클릭하면: 출력 중이면 스킵, 다 나왔으면 다음 페이지")]
        [SerializeField] private bool _advanceOnTap = true;
        [Tooltip("PC용: Space / Enter 로 진행")]
        [SerializeField] private bool _advanceOnKeyboard = true;
        [Tooltip("한 페이지가 다 나온 뒤 자동으로 넘어가기까지의 시간(초). 0 = 자동 넘김 끔")]
        [SerializeField, Min(0f)] private float _autoAdvanceDelay = 0f;

        [Header("이벤트")]
        [Tooltip("엔딩이 시작될 때 (BGM 교체 등)")]
        public UnityEvent onEndingStarted;
        [Tooltip("마지막 페이지까지 다 본 뒤 한 번 더 진행했을 때 (타이틀로 이동 등은 나중에 여기에 연결)")]
        public UnityEvent onEndingFinished;

        public bool IsShowing { get; private set; }

        private string[] _current;
        private int _index;
        private float _waitTimer;
        private bool _finished;

        private void Awake()
        {
            if (_popup == null || _typewriter == null)
            {
                Debug.LogError($"[Ending] {name}: PopupView 또는 TypewriterEffect가 연결되지 않았습니다.", this);
                enabled = false;
            }
        }

        /// <summary>인스펙터에 적힌 문장으로 엔딩 시작.</summary>
        public void Show() => Show(_lines);

        /// <summary>코드에서 엔딩별 문장을 넘겨 시작. (나중에 스탯별 엔딩 연결용)</summary>
        public void Show(string[] lines)
        {
            if (!enabled || lines == null || lines.Length == 0) return;

            _current = lines;
            _index = 0;
            _waitTimer = 0f;
            _finished = false;
            IsShowing = true;

            _popup.Open();
            _typewriter.Play(_current[0]);
            onEndingStarted?.Invoke();
        }

        public void Hide()
        {
            if (!IsShowing) return;
            IsShowing = false;
            _typewriter.Skip();
            _popup.Close();
        }

        /// <summary>출력 중이면 스킵, 다 나왔으면 다음 페이지, 마지막이면 종료 이벤트.</summary>
        public void Advance()
        {
            if (!IsShowing) return;

            if (_typewriter.IsPlaying) { _typewriter.Skip(); return; }

            _waitTimer = 0f;
            if (_index < _current.Length - 1)
            {
                _index++;
                _typewriter.Play(_current[_index]);
            }
            else if (!_finished)
            {
                _finished = true;
                onEndingFinished?.Invoke();
            }
        }

        private void Update()
        {
            if (!IsShowing) return;

            if (_advanceOnKeyboard && AdvanceKeyPressed()) { Advance(); return; }

            if (_autoAdvanceDelay > 0f && !_typewriter.IsPlaying && !_finished)
            {
                _waitTimer += Time.unscaledDeltaTime;
                if (_waitTimer >= _autoAdvanceDelay) Advance();
            }
        }

        // 검은 배경(Dim)을 터치하면 클릭 이벤트가 부모인 이 오브젝트로 올라온다.
        public void OnPointerClick(PointerEventData eventData)
        {
            if (_advanceOnTap) Advance();
        }

#if ENABLE_INPUT_SYSTEM
        private static bool AdvanceKeyPressed()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame);
        }
#else
        private static bool AdvanceKeyPressed() =>
            Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return);
#endif
    }
}
