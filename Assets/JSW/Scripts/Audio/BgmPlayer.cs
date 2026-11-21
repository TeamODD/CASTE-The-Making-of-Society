using System.Collections;
using Caste.Settings;
using UnityEngine;

namespace Caste.Audio
{
    /// <summary>
    /// 배경음 재생기: AudioSource 2개를 번갈아 쓰며 곡 사이를 크로스페이드한다.
    /// - "어떤 곡을 언제 틀지"는 모른다 → BgmCue 같은 요청자가 정한다. (SRP)
    /// - 음량은 GameSettings.BgmVolumeChanged를 듣기만 한다 → 설정창과 서로 모름. (DIP)
    /// - 씬이 바뀌어도 유지되므로 타이틀 → 인게임처럼 같은 곡이면 끊기지 않고 이어진다.
    /// 붙이는 곳: 각 씬의 최상위 빈 오브젝트 (먼저 생긴 하나만 남고 나머지는 스스로 사라짐)
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Caste/Audio/Bgm Player")]
    public class BgmPlayer : MonoBehaviour, IBgmPlayer
    {
        [Header("설정 연결")]
        [Tooltip("설정창의 배경음 슬라이더 값이 들어있는 에셋")]
        [SerializeField] private GameSettings _settings;

        [Header("음량")]
        [Tooltip("곡 자체가 너무 크거나 작을 때 보정하는 전체 배율 (설정창 값과 곱해짐)")]
        [Range(0f, 1f)] [SerializeField] private float _masterVolume = 1f;

        [Header("페이드")]
        [Tooltip("곡이 바뀔 때 겹쳐서 넘어가는 시간(초). 0 = 즉시 교체")]
        [SerializeField, Min(0f)] private float _defaultFadeDuration = 1.5f;

        [Header("옵션")]
        [Tooltip("켜면 씬이 바뀌어도 유지 (끄면 씬마다 음악이 새로 시작됨)")]
        [SerializeField] private bool _persistAcrossScenes = true;

        /// <summary>현재 살아있는 재생기. 요청자는 IBgmPlayer로만 사용한다.</summary>
        public static IBgmPlayer Instance { get; private set; }

        private readonly AudioSource[] _sources = new AudioSource[2];
        private readonly float[] _weights = new float[2]; // 페이드 진행도 (0~1)
        private int _current;
        private float _currentTarget; // 현재 곡의 목표 진행도 (재생 = 1, 정지 = 0)
        private Coroutine _fade;

        public AudioClip CurrentClip => _sources[_current] != null ? _sources[_current].clip : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && !ReferenceEquals(Instance, this)) { Destroy(gameObject); return; }
            Instance = this;

            if (_persistAcrossScenes)
            {
                transform.SetParent(null); // DontDestroyOnLoad는 최상위 오브젝트에만 적용됨
                DontDestroyOnLoad(gameObject);
            }

            for (int i = 0; i < _sources.Length; i++)
            {
                var src = gameObject.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = true;
                src.spatialBlend = 0f; // 2D 사운드
                src.priority = 0;      // 배경음은 다른 소리에 밀려 끊기지 않게
                src.volume = 0f;
                _sources[i] = src;
            }

            if (_settings == null)
                Debug.LogWarning($"[BgmPlayer] {name}: GameSettings가 연결되지 않아 설정창 음량이 반영되지 않습니다.", this);
        }

        private void OnEnable()
        {
            if (_settings != null) _settings.BgmVolumeChanged += HandleVolumeChanged;
            ApplyVolumes();
        }

        private void OnDisable()
        {
            if (_settings != null) _settings.BgmVolumeChanged -= HandleVolumeChanged;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        public void Play(AudioClip clip, float fadeDuration = -1f)
        {
            if (clip == null) { Stop(fadeDuration); return; }

            var cur = _sources[_current];
            if (cur.clip == clip && cur.isPlaying)
            {
                // 같은 곡: 처음부터 다시 틀지 않음 (꺼지는 중이었다면 다시 키움)
                _currentTarget = 1f;
                StartFade(fadeDuration);
                return;
            }

            _current = 1 - _current;
            var next = _sources[_current];
            next.clip = clip;
            next.time = 0f;
            _weights[_current] = 0f;
            next.Play();

            _currentTarget = 1f;
            StartFade(fadeDuration);
        }

        public void Stop(float fadeDuration = -1f)
        {
            _currentTarget = 0f;
            StartFade(fadeDuration);
        }

        private void StartFade(float fadeDuration)
        {
            if (fadeDuration < 0f) fadeDuration = _defaultFadeDuration;
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeRoutine(fadeDuration));
        }

        private IEnumerator FadeRoutine(float duration)
        {
            float speed = duration > 0f ? 1f / duration : float.PositiveInfinity;

            while (true)
            {
                bool done = true;
                float step = speed * Time.unscaledDeltaTime; // 일시정지(timeScale 0)에도 페이드 진행
                for (int i = 0; i < _sources.Length; i++)
                {
                    float target = i == _current ? _currentTarget : 0f;
                    _weights[i] = Mathf.MoveTowards(_weights[i], target, step);
                    if (!Mathf.Approximately(_weights[i], target)) done = false;
                }
                ApplyVolumes();
                if (done) break;
                yield return null;
            }

            // 완전히 꺼진 소스는 정지 + 클립 해제 (메모리 절약)
            for (int i = 0; i < _sources.Length; i++)
            {
                if (_weights[i] > 0f) continue;
                _sources[i].Stop();
                _sources[i].clip = null;
            }
            _fade = null;
        }

        private void HandleVolumeChanged(float _) => ApplyVolumes();

        private void ApplyVolumes()
        {
            float volume = (_settings != null ? _settings.BgmVolume : 1f) * _masterVolume;
            for (int i = 0; i < _sources.Length; i++)
                if (_sources[i] != null) _sources[i].volume = _weights[i] * volume;
        }

#if UNITY_EDITOR
        // 플레이 중 인스펙터에서 Master Volume을 바꾸면 바로 들리도록
        private void OnValidate()
        {
            if (Application.isPlaying) ApplyVolumes();
        }
#endif
    }
}
