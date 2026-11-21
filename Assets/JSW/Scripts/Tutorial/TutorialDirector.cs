using System;
using System.Collections;
using System.Collections.Generic;
using Caste.Effects;
using TMPro;
using UnityEngine;

namespace Caste.Tutorial
{
    /// <summary>
    /// 튜토리얼 진행 담당. 정해진 시점(선택 횟수)에 카드 표시를 멈추고,
    /// 안내 문장을 페이지별로 보여준 뒤 카드 진행을 재개시킨다. (SRP: "언제·무엇을 보여줄지"만 담당)
    /// 어둡게 하기/밝히기는 TutorialSpotlight에, 글자 출력은 TypewriterEffect에 맡긴다.
    /// </summary>
    [AddComponentMenu("Caste/Tutorial/Tutorial Director")]
    public class TutorialDirector : MonoBehaviour, ICardRevealGate
    {
        [Header("출력 위치")]
        [SerializeField] private TMP_Text _text;
        [Tooltip("(선택) 비워두면 문장이 한 번에 나온다")]
        [SerializeField] private TypewriterEffect _typewriter;
        [SerializeField] private TutorialSpotlight _spotlight;
        [Tooltip("(선택) 한 페이지를 다 읽으면 켜지는 '탭하여 계속' 표시")]
        [SerializeField] private GameObject _continueHint;

        [Header("튜토리얼 중 숨길 UI (카드, 화자 이름 등)")]
        [SerializeField] private RectTransform[] _hideWhilePlaying;

        [Header("단계")]
        [SerializeField] private TutorialStep[] _steps;

        [Header("옵션")]
        [Tooltip("켜면 튜토리얼을 끝까지 한 번 본 뒤로는 다시 나오지 않는다 (기기에 저장)")]
        [SerializeField] private bool _playOnlyOnce;
        [Tooltip("이어하기로 시작해도 튜토리얼을 보여줄지 (보통 끔)")]
        [SerializeField] private bool _playOnResume;
        [Tooltip("페이지가 바뀌거나 글자를 건너뛴 직후, 이 시간(초) 동안은 탭을 무시한다 (연타로 페이지가 넘어가는 것 방지)")]
        [SerializeField, Min(0f)] private float _tapCooldown = 0.3f;

        private const string SeenKey = "Caste.Tutorial.Seen";

        private bool[] _played;
        private bool _armed;
        private bool _playing;
        private bool _advance;
        private float _ignoreTapsUntil;
        private Coroutine _routine;

        public bool IsPlaying => _playing;

        private void OnEnable()
        {
            if (_spotlight != null) _spotlight.Tapped += OnTapped;
        }

        private void OnDisable()
        {
            if (_spotlight != null) _spotlight.Tapped -= OnTapped;
        }

        // ───────── ICardRevealGate ─────────

        public void BeginRun(bool isNewGame)
        {
            Abort(); // 튜토리얼 도중 새 게임을 시작해도 이전 진행이 남지 않게

            _played = new bool[_steps != null ? _steps.Length : 0];
            bool seen = _playOnlyOnce && PlayerPrefs.GetInt(SeenKey, 0) == 1;
            _armed = (isNewGame || _playOnResume) && !seen;
        }

        public bool TryHold(int choicesMade, Action release)
        {
            if (_playing) return true;              // 이미 진행 중이면 계속 붙잡아 둔다 (중복 호출 무시)
            if (!_armed || !isActiveAndEnabled || _played == null) return false;

            for (int i = 0; i < _played.Length; i++)
            {
                if (_played[i] || _steps[i] == null || _steps[i].beforeChoice != choicesMade) continue;
                _routine = StartCoroutine(Play(i, release));
                return true;
            }
            return false;
        }

        // ───────── 진행 ─────────

        private IEnumerator Play(int index, Action release)
        {
            _playing = true;
            var step = _steps[index];

            SetConcealed(true);
            if (_spotlight != null) _spotlight.Show(step.dimBackground, Highlights(step));

            if (step.pages != null)
            {
                foreach (var page in step.pages)
                {
                    ShowPage(page);
                    _advance = false;
                    while (!_advance) yield return null;
                }
            }

            Close();
            _played[index] = true;
            _routine = null;

            if (_playOnlyOnce && Array.TrueForAll(_played, p => p))
            {
                PlayerPrefs.SetInt(SeenKey, 1);
                PlayerPrefs.Save();
            }

            release?.Invoke(); // 멈춰 뒀던 카드를 이제 보여준다
        }

        private void Abort()
        {
            if (!_playing) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            Close();
        }

        private void Close()
        {
            if (_continueHint != null) _continueHint.SetActive(false);
            if (_spotlight != null) _spotlight.Hide();
            SetConcealed(false);
            _playing = false;
        }

        private void ShowPage(string page)
        {
            if (_continueHint != null) _continueHint.SetActive(false);
            _ignoreTapsUntil = Time.unscaledTime + _tapCooldown;
            if (_typewriter != null) _typewriter.Play(page);
            else if (_text != null) _text.text = page;
        }

        // 타자 중에 탭 → 글자 전부 보이기, 다 보인 상태에서 탭 → 다음 페이지
        private void OnTapped()
        {
            if (!_playing || Time.unscaledTime < _ignoreTapsUntil) return;

            if (_typewriter != null && _typewriter.IsPlaying)
            {
                _typewriter.Skip();
                _ignoreTapsUntil = Time.unscaledTime + _tapCooldown;
                return;
            }
            _advance = true;
        }

        private void Update()
        {
            // 한 페이지를 다 출력하면 '탭하여 계속' 표시
            if (_playing && _continueHint != null && !_continueHint.activeSelf
                && (_typewriter == null || !_typewriter.IsPlaying))
                _continueHint.SetActive(true);
        }

        // 어둡게 할 때도 문장은 읽혀야 하므로 문장 칸은 자동으로 밝힌다 (막의 자식이면 Spotlight가 알아서 건너뜀)
        private IEnumerable<RectTransform> Highlights(TutorialStep step)
        {
            if (!step.dimBackground) yield break;
            if (_text != null) yield return _text.rectTransform;
            if (_continueHint != null) yield return _continueHint.transform as RectTransform;
            if (step.highlights == null) yield break;
            foreach (var h in step.highlights) yield return h;
        }

        private void SetConcealed(bool concealed)
        {
            if (_hideWhilePlaying == null) return;
            foreach (var target in _hideWhilePlaying)
            {
                if (target == null) continue;
                var group = target.GetComponent<CanvasGroup>();
                if (group == null) group = target.gameObject.AddComponent<CanvasGroup>();
                group.alpha = concealed ? 0f : 1f;
                group.blocksRaycasts = !concealed;
            }
        }

        [ContextMenu("튜토리얼 '이미 봄' 기록 지우기")]
        private void ResetSeen()
        {
            PlayerPrefs.DeleteKey(SeenKey);
            Debug.Log("[Tutorial] '이미 봄' 기록을 지웠습니다. 다음 새 게임에서 다시 나옵니다.");
        }
    }
}
