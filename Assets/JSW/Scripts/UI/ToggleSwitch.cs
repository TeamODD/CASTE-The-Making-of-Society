using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Caste.UI
{
    /// <summary>
    /// 모바일식 ON/OFF 스위치. 입력 처리는 Unity 기본 Toggle에 맡기고,
    /// 이 컴포넌트는 "손잡이가 미끄러지고 배경색이 바뀌는 모양"만 담당한다. (SRP)
    /// 값 읽기 / 변경 감지는 Toggle.isOn / Toggle.onValueChanged 그대로 사용.
    /// </summary>
    [RequireComponent(typeof(Toggle))]
    [AddComponentMenu("Caste/UI/Toggle Switch")]
    public class ToggleSwitch : MonoBehaviour
    {
        [Header("구성 요소")]
        [Tooltip("스위치 배경(트랙) Image")]
        [SerializeField] private Image _background;
        [Tooltip("좌우로 움직이는 손잡이")]
        [SerializeField] private RectTransform _knob;

        [Header("색")]
        [SerializeField] private Color _onColor = new Color32(0x2A, 0x1E, 0x14, 0xFF);
        [SerializeField] private Color _offColor = new Color32(0xBF, 0xA7, 0x7A, 0xFF);

        [Header("움직임")]
        [Tooltip("손잡이와 배경 테두리 사이 여백")]
        [Min(0f)] [SerializeField] private float _knobPadding = 8f;
        [Tooltip("켜고 끌 때 애니메이션 시간(초). 0이면 즉시")]
        [Min(0f)] [SerializeField] private float _animDuration = 0.12f;

        private Toggle _toggle;
        private Coroutine _routine;

        public Toggle Toggle => _toggle != null ? _toggle : (_toggle = GetComponent<Toggle>());
        public Image Background => _background;
        public Image KnobImage => _knob != null ? _knob.GetComponent<Image>() : null;

        private void Awake()
        {
            Toggle.onValueChanged.AddListener(HandleValueChanged);
            Snap();
        }

        private void OnDestroy()
        {
            if (_toggle != null) _toggle.onValueChanged.RemoveListener(HandleValueChanged);
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            // OnValidate 안에서 RectTransform을 바로 바꾸면 경고가 나므로 한 프레임 미룸
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null && _background != null && _knob != null) Snap();
            };
#endif
        }

        /// <summary>이벤트 없이 값만 바꾼다 (설정창을 열 때 저장된 값 표시용).</summary>
        public void SetIsOnWithoutNotify(bool isOn)
        {
            Toggle.SetIsOnWithoutNotify(isOn);
            Snap();
        }

        /// <summary>스킨(MenuSkin)에서 색을 바꿀 때 사용.</summary>
        public void SetColors(Color onColor, Color offColor)
        {
            _onColor = onColor;
            _offColor = offColor;
            Snap();
        }

        private void HandleValueChanged(bool isOn)
        {
            if (_routine != null) StopCoroutine(_routine);
            if (_animDuration <= 0f || !isActiveAndEnabled) { Snap(); return; }
            _routine = StartCoroutine(AnimateRoutine(isOn ? 1f : 0f));
        }

        private IEnumerator AnimateRoutine(float target)
        {
            float start = CurrentT();
            for (float t = 0f; t < _animDuration; t += Time.unscaledDeltaTime)
            {
                SetVisual(Mathf.Lerp(start, target, t / _animDuration));
                yield return null;
            }
            SetVisual(target);
            _routine = null;
        }

        private void Snap() => SetVisual(Toggle.isOn ? 1f : 0f);

        // 0 = OFF(왼쪽), 1 = ON(오른쪽)
        private void SetVisual(float t)
        {
            if (_background != null) _background.color = Color.Lerp(_offColor, _onColor, t);
            if (_knob == null || _background == null) return;

            float width = _background.rectTransform.rect.width;
            float knobWidth = _knob.rect.width;
            float x = Mathf.Lerp(_knobPadding, width - knobWidth - _knobPadding, t);
            _knob.anchorMin = _knob.anchorMax = _knob.pivot = new Vector2(0f, 0.5f);
            _knob.anchoredPosition = new Vector2(x, 0f);
        }

        private float CurrentT()
        {
            if (_knob == null || _background == null) return Toggle.isOn ? 1f : 0f;
            float range = _background.rectTransform.rect.width - _knob.rect.width - _knobPadding * 2f;
            return range <= 0f ? 0f : Mathf.Clamp01((_knob.anchoredPosition.x - _knobPadding) / range);
        }
    }
}
