using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Caste.Scenes
{
    /// <summary>
    /// 씬 전환 연출: 검게 페이드 아웃 → 씬 로딩 → 페이드 인.
    /// "어떤 씬으로 갈지"는 모르고 "어떻게 넘어갈지"만 담당한다. (SRP)
    /// 씬이 바뀌어도 사라지지 않아서 다음 씬에서 페이드 인까지 이어서 처리한다.
    /// 붙이는 곳: 전용 Canvas 루트 (최상위 오브젝트여야 함, 자식에 검은 Image + CanvasGroup)
    /// </summary>
    [AddComponentMenu("Caste/Scenes/Scene Transitioner")]
    public class SceneTransitioner : MonoBehaviour
    {
        [Header("구성 요소")]
        [Tooltip("화면 전체를 덮는 검은 Image의 CanvasGroup")]
        [SerializeField] private CanvasGroup _fadeGroup;

        [Header("시간(초)")]
        [Tooltip("검게 어두워지는 시간")]
        [SerializeField, Min(0f)] private float _fadeOutDuration = 0.5f;
        [Tooltip("로딩이 끝난 뒤 검은 화면을 유지하는 시간")]
        [SerializeField, Min(0f)] private float _holdBlackTime = 0.2f;
        [Tooltip("다음 씬이 밝아지는 시간")]
        [SerializeField, Min(0f)] private float _fadeInDuration = 0.6f;

        [Header("옵션")]
        [Tooltip("켜면 씬이 바뀌어도 유지 (페이드 인을 하려면 켜 둬야 함)")]
        [SerializeField] private bool _persistAcrossScenes = true;

        /// <summary>현재 살아있는 전환기. 다른 씬에서 다시 생겨도 하나만 남긴다.</summary>
        public static SceneTransitioner Instance { get; private set; }

        public bool IsTransitioning { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (_persistAcrossScenes)
            {
                transform.SetParent(null); // DontDestroyOnLoad는 최상위 오브젝트에만 적용됨
                DontDestroyOnLoad(gameObject);
            }

            if (_fadeGroup == null)
            {
                Debug.LogError($"[SceneTransitioner] {name}: Fade CanvasGroup이 연결되지 않았습니다.", this);
                return;
            }
            SetAlpha(0f);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>페이드 아웃 → 씬 로드 → 페이드 인. (Build Profiles에 등록된 씬 이름)</summary>
        public void LoadScene(string sceneName)
        {
            if (IsTransitioning) return;
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"[SceneTransitioner] '{sceneName}' 씬이 빌드 목록(File > Build Profiles)에 없습니다.", this);
                return;
            }
            StartCoroutine(Run(sceneName));
        }

        private IEnumerator Run(string sceneName)
        {
            IsTransitioning = true;
            _fadeGroup.blocksRaycasts = true; // 전환 중 터치 차단 (버튼 연타 방지)

            yield return Fade(0f, 1f, _fadeOutDuration);

            var op = SceneManager.LoadSceneAsync(sceneName);
            while (!op.isDone) yield return null;

            for (float t = 0f; t < _holdBlackTime; t += Time.unscaledDeltaTime) yield return null;

            yield return Fade(1f, 0f, _fadeInDuration);

            _fadeGroup.blocksRaycasts = false;
            IsTransitioning = false;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime) // 일시정지(timeScale 0)에도 동작
            {
                SetAlpha(Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            SetAlpha(to);
        }

        private void SetAlpha(float a)
        {
            _fadeGroup.alpha = a;
            _fadeGroup.blocksRaycasts = a > 0f;
        }
    }
}
