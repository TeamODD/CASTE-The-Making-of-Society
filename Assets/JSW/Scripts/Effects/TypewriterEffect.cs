using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace Caste.Effects
{
    /// <summary>
    /// TMP 텍스트를 타자기처럼 한 글자씩 보여준다.
    /// 텍스트가 있는 오브젝트에 이 스크립트 하나만 붙이면 동작한다. (SRP: "글자 공개 타이밍"만 담당)
    /// 글자를 지우고 다시 쓰지 않고 maxVisibleCharacters만 올리므로 GC가 없고, 리치 텍스트 태그(&lt;color&gt; 등)도 안전하다.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Caste/Effects/Typewriter Effect")]
    public class TypewriterEffect : MonoBehaviour
    {
        [Header("대상 (비워두면 같은 오브젝트의 TMP_Text 자동 사용)")]
        [SerializeField] private TMP_Text _target;

        [Header("켜기 / 끄기")]
        [Tooltip("끄면 타자기 효과 없이 글자가 한 번에 나온다. (설정창의 타자기 스위치가 이 값을 바꾼다)")]
        [SerializeField] private bool _typingEnabled = true;

        [Header("속도")]
        [Tooltip("1초에 몇 글자를 보여줄지. 클수록 빠르다.")]
        [SerializeField, Min(1f)] private float _charactersPerSecond = 30f;
        [Tooltip("재생을 시작하기 전 대기 시간(초).")]
        [SerializeField, Min(0f)] private float _startDelay = 0f;

        [Header("끊어 읽기 (문장부호에서 잠깐 멈춤)")]
        [SerializeField] private bool _pauseOnPunctuation = true;
        [Tooltip("문장부호를 찍은 뒤 추가로 멈추는 시간(초).")]
        [SerializeField, Min(0f)] private float _punctuationPause = 0.2f;
        [Tooltip("멈춤 대상 글자들.")]
        [SerializeField] private string _punctuationChars = ".,!?…:;";

        [Header("재생 옵션")]
        [Tooltip("켜면 오브젝트가 활성화될 때 현재 텍스트로 자동 재생.")]
        [SerializeField] private bool _playOnEnable = true;
        [Tooltip("켜면 Time.timeScale(일시정지)의 영향을 받지 않는다.")]
        [SerializeField] private bool _useUnscaledTime = true;

        [Header("이벤트 (효과음 연결 등)")]
        [Tooltip("글자가 하나 나올 때마다 호출. 타자 효과음을 여기에 연결하면 된다.")]
        [SerializeField] private UnityEvent _onCharacterTyped;
        [Tooltip("모든 글자가 나온 뒤(스킵 포함) 한 번 호출.")]
        [SerializeField] private UnityEvent _onCompleted;

        private Coroutine _routine;

        public bool IsPlaying => _routine != null;

        /// <summary>런타임에서 속도를 바꾸고 싶을 때 사용.</summary>
        public float CharactersPerSecond
        {
            get => _charactersPerSecond;
            set => _charactersPerSecond = Mathf.Max(1f, value);
        }

        /// <summary>타자기 효과 사용 여부. 재생 중에 끄면 남은 글자를 즉시 보여준다.</summary>
        public bool TypingEnabled
        {
            get => _typingEnabled;
            set
            {
                _typingEnabled = value;
                if (!value) Skip();
            }
        }

        private void Reset() => _target = GetComponent<TMP_Text>();

        private void Awake()
        {
            if (_target == null) _target = GetComponent<TMP_Text>();
            if (_target == null)
            {
                Debug.LogError($"[{nameof(TypewriterEffect)}] TMP_Text를 찾지 못했습니다. 텍스트와 같은 오브젝트에 붙이거나 Target을 지정하세요.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_playOnEnable) Play();
        }

        private void OnDisable()
        {
            // 도중에 꺼져도 글자가 숨겨진 채로 남지 않게 정리
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }
            if (_target != null) _target.maxVisibleCharacters = int.MaxValue;
        }

        /// <summary>현재 텍스트를 처음부터 타자기로 재생한다. (꺼져 있으면 즉시 전체 표시)</summary>
        public void Play()
        {
            if (_target == null) return;
            if (_routine != null) { StopCoroutine(_routine); _routine = null; }

            if (!_typingEnabled)
            {
                _target.maxVisibleCharacters = int.MaxValue;
                _onCompleted?.Invoke();
                return;
            }

            _routine = StartCoroutine(Run());
        }

        /// <summary>새 문장으로 바꾸고 재생한다. (대사 코드에서 호출용)</summary>
        public void Play(string text)
        {
            if (_target == null) return;
            _target.text = text;
            Play();
        }

        /// <summary>재생을 건너뛰고 전체 글자를 즉시 보여준다. (터치 시 스킵 버튼 등에 연결)</summary>
        public void Skip()
        {
            if (_routine == null) return;
            StopCoroutine(_routine);
            _routine = null;
            _target.maxVisibleCharacters = int.MaxValue;
            _onCompleted?.Invoke();
        }

        private IEnumerator Run()
        {
            _target.maxVisibleCharacters = 0;
            _target.ForceMeshUpdate(); // 태그를 제외한 실제 글자 수를 얻기 위해 필요
            int total = _target.textInfo.characterCount;

            for (float t = 0f; t < _startDelay; t += DeltaTime()) yield return null;

            int visible = 0;
            float timer = 0f;

            while (visible < total)
            {
                timer += DeltaTime();
                float interval = 1f / _charactersPerSecond; // 재생 중 인스펙터에서 바꿔도 즉시 반영

                // 고속에서도 프레임당 여러 글자를 정확한 속도로 공개
                while (timer >= interval && visible < total)
                {
                    timer -= interval;
                    visible++;
                    _target.maxVisibleCharacters = visible;
                    _onCharacterTyped?.Invoke();

                    if (_pauseOnPunctuation && IsPausePoint(visible - 1))
                        timer -= _punctuationPause;
                }
                yield return null;
            }

            _target.maxVisibleCharacters = int.MaxValue;
            _routine = null;
            _onCompleted?.Invoke();
        }

        private bool IsPausePoint(int index)
        {
            var infos = _target.textInfo.characterInfo;
            return index >= 0 && index < infos.Length
                   && _punctuationChars.IndexOf(infos[index].character) >= 0;
        }

        private float DeltaTime() => _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }
}
