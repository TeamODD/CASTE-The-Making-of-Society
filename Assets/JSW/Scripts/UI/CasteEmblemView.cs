using System.Collections;
using Caste.Endings;
using UnityEngine;
using UnityEngine.UI;

namespace Caste.UI
{
    /// <summary>
    /// 현재 계층의 상징물을 보여준다. 계층이 바뀔 때만 부드럽게 교체(페이드)한다.
    /// 계층 코드가 어디서 오는지는 모른다 → BackendCardGameController.onCasteCodeShown 같은 이벤트에 ShowCaste를 연결해서 쓴다. (DIP)
    /// 상징물 그림은 CasteEmblemTable(엔딩 도감과 같은 표)에서 가져온다 → 표만 고치면 도감과 게임 화면이 같이 바뀐다.
    /// 붙이는 곳: 상징물을 보여줄 Image 오브젝트
    /// </summary>
    [RequireComponent(typeof(Image))]
    [AddComponentMenu("Caste/UI/Caste Emblem View")]
    public class CasteEmblemView : MonoBehaviour
    {
        [Tooltip("계층 코드 → 상징물 표 (ICasteEmblemProvider를 구현한 에셋, 예: Data/CasteEmblemTable)")]
        [SerializeField] private ScriptableObject _emblemSource;

        [Tooltip("계층이 바뀔 때 사라졌다 나타나는 시간(초). 0이면 즉시 교체")]
        [SerializeField, Min(0f)] private float _fadeDuration = 0.3f;

        [Tooltip("상징물이 없는 계층이면 숨긴다")]
        [SerializeField] private bool _hideWhenMissing = true;

        private ICasteEmblemProvider _emblems;
        private Image _image;
        private string _currentCode;
        private Coroutine _fade;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _image.preserveAspect = true;
            _emblems = _emblemSource as ICasteEmblemProvider;
            if (_emblems == null)
                Debug.LogError($"[CasteEmblem] {name}: Emblem Source가 ICasteEmblemProvider가 아닙니다.", this);

            // 첫 카드가 나오기 전에는 빈 상징물이 보이지 않게 숨겨둔다
            if (string.IsNullOrEmpty(_currentCode)) SetAlpha(0f);
        }

        /// <summary>계층 코드(B, K, V, S, C)를 받아 상징물을 표시. 같은 계층이면 아무것도 하지 않는다.</summary>
        public void ShowCaste(string casteCode)
        {
            if (_emblems == null || casteCode == _currentCode) return;

            bool first = string.IsNullOrEmpty(_currentCode);
            _currentCode = casteCode;
            Sprite emblem = _emblems.GetEmblem(casteCode);

            if (_fade != null) StopCoroutine(_fade);
            if (first || _fadeDuration <= 0f || !isActiveAndEnabled)
            {
                Apply(emblem);
                SetAlpha(Visible(emblem) ? 1f : 0f);
            }
            else
            {
                _fade = StartCoroutine(CrossFade(emblem));
            }
        }

        private IEnumerator CrossFade(Sprite next)
        {
            float half = _fadeDuration * 0.5f;
            yield return FadeTo(0f, half);
            Apply(next);
            if (Visible(next)) yield return FadeTo(1f, half);
            _fade = null;
        }

        private IEnumerator FadeTo(float target, float duration)
        {
            float start = _image.color.a;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                SetAlpha(Mathf.Lerp(start, target, t / duration));
                yield return null;
            }
            SetAlpha(target);
        }

        private bool Visible(Sprite emblem) => emblem != null || !_hideWhenMissing;

        private void Apply(Sprite emblem)
        {
            _image.sprite = emblem;
            _image.enabled = Visible(emblem);
        }

        private void SetAlpha(float a)
        {
            Color c = _image.color;
            c.a = a;
            _image.color = c;
        }
    }
}
