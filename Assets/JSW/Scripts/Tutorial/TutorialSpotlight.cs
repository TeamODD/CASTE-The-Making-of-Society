using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Caste.Tutorial
{
    /// <summary>
    /// 화면 전체를 덮는 어두운 막. 막의 자식(가운데 글자 등)도 막과 함께 나타나고 사라진다.
    /// 필요하면 지정한 UI만 막 위로 끌어올려 밝게 보이게 할 수 있고, 화면 아무 곳이나 탭하면 Tapped를 알린다.
    /// (SRP: "가리고 비추기 + 탭 감지"만 담당)
    /// 밝히기는 마스크로 구멍을 뚫는 대신 대상에 Canvas(정렬 순서 덮어쓰기)를 잠깐 붙이는 방식이라 가볍다.
    /// </summary>
    [RequireComponent(typeof(Image), typeof(CanvasGroup))]
    [AddComponentMenu("Caste/Tutorial/Tutorial Spotlight")]
    public class TutorialSpotlight : MonoBehaviour, IPointerClickHandler
    {
        [Tooltip("어둡게 할 때의 진하기 (0 = 투명, 1 = 완전 검정)")]
        [SerializeField, Range(0f, 1f)] private float _dimAlpha = 0.85f;
        [Tooltip("나타나고 사라지는 데 걸리는 시간(초)")]
        [SerializeField, Min(0f)] private float _fadeDuration = 0.35f;
        [Tooltip("다른 UI보다 얼마나 위에 그릴지 (팝업보다 위에 오도록 넉넉하게)")]
        [SerializeField] private int _sortingOffset = 100;

        public event Action Tapped;

        private struct Lit
        {
            public Canvas Canvas;
            public bool Added;
            public bool PrevOverride;
            public int PrevOrder;
        }

        private readonly List<Lit> _lit = new List<Lit>();
        private Image _image;
        private CanvasGroup _group;
        private Canvas _canvas;
        private Coroutine _fade;

        private void Awake()
        {
            _image = GetComponent<Image>();
            _group = GetComponent<CanvasGroup>();

            _canvas = GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();

            var parentCanvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : null;
            int baseOrder = parentCanvas != null ? parentCanvas.rootCanvas.sortingOrder : 0;
            _canvas.overrideSorting = true;
            _canvas.sortingOrder = baseOrder + _sortingOffset;

            _group.alpha = 0f;
            _group.blocksRaycasts = false; // 평소에는 터치를 막지 않는다
        }

        /// <summary>막을 켠다. dim이 false면 투명한 막(자식 글자와 탭 감지만)으로 켠다.</summary>
        public void Show(bool dim, IEnumerable<RectTransform> highlights)
        {
            RestoreLit();
            if (highlights != null)
                foreach (var target in highlights) Light(target);

            var color = _image.color;
            color.a = dim ? _dimAlpha : 0f;
            _image.color = color;

            _group.blocksRaycasts = true; // 튜토리얼 중에는 뒤의 카드·버튼이 눌리지 않게
            FadeTo(1f, null);
        }

        /// <summary>막을 끈다. 밝혔던 UI는 페이드가 끝난 뒤 원래대로 돌린다.</summary>
        public void Hide()
        {
            _group.blocksRaycasts = false;
            FadeTo(0f, RestoreLit);
        }

        public void OnPointerClick(PointerEventData eventData) => Tapped?.Invoke();

        private void Light(RectTransform target)
        {
            if (target == null || target.IsChildOf(transform)) return; // 막의 자식은 이미 막 위에 있다

            var c = target.GetComponent<Canvas>();
            bool added = c == null;
            if (added) c = target.gameObject.AddComponent<Canvas>();

            _lit.Add(new Lit { Canvas = c, Added = added, PrevOverride = c.overrideSorting, PrevOrder = c.sortingOrder });
            c.overrideSorting = true;
            c.sortingOrder = _canvas.sortingOrder + 1;
        }

        private void RestoreLit()
        {
            foreach (var lit in _lit)
            {
                if (lit.Canvas == null) continue;
                if (lit.Added) Destroy(lit.Canvas);
                else
                {
                    lit.Canvas.overrideSorting = lit.PrevOverride;
                    lit.Canvas.sortingOrder = lit.PrevOrder;
                }
            }
            _lit.Clear();
        }

        private void FadeTo(float alpha, Action onDone)
        {
            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(FadeRoutine(alpha, onDone));
        }

        private IEnumerator FadeRoutine(float to, Action onDone)
        {
            float from = _group.alpha;
            for (float t = 0f; t < _fadeDuration; t += Time.unscaledDeltaTime)
            {
                _group.alpha = Mathf.Lerp(from, to, t / _fadeDuration);
                yield return null;
            }
            _group.alpha = to;
            _fade = null;
            onDone?.Invoke();
        }
    }
}
