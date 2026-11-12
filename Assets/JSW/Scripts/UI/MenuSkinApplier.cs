using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Caste.UI
{
    /// <summary>
    /// MenuSkin 에셋의 내용을 실제 UI(Image / Button / Slider / Switch / 글자)에 입히는 역할만 한다. (SRP)
    /// 에디터에서도 바로 보이도록 ExecuteAlways. 스킨 에셋을 교체하면 테마 전체가 바뀐다. (OCP)
    /// 비워둔 칸은 건너뛴다.
    /// 붙이는 곳: 팝업 루트
    /// </summary>
    [ExecuteAlways]
    public class MenuSkinApplier : MonoBehaviour
    {
        [SerializeField] private MenuSkin _skin;

        [Header("적용 대상")]
        [SerializeField] private Image _panel;
        [Tooltip("스킨을 입힐 버튼들")]
        [SerializeField] private Button[] _menuButtons;
        [Tooltip("톱니바퀴 설정 버튼 (없으면 비워둠)")]
        [SerializeField] private Button _settingsButton;
        [Tooltip("패널 위 글자들 (제목, 항목 이름)")]
        [SerializeField] private TMP_Text[] _labels;
        [SerializeField] private Slider[] _sliders;
        [SerializeField] private ToggleSwitch[] _switches;

        private void OnEnable()
        {
            MenuSkin.Changed += HandleSkinChanged;
            Apply();
        }

        private void OnDisable() => MenuSkin.Changed -= HandleSkinChanged;

        private void OnValidate()
        {
#if UNITY_EDITOR
            // OnValidate 안에서 다른 컴포넌트를 바로 바꾸면 경고가 날 수 있어 한 프레임 미룸
            UnityEditor.EditorApplication.delayCall += () => { if (this != null) Apply(); };
#endif
        }

        private void HandleSkinChanged(MenuSkin changed)
        {
            if (changed == _skin) Apply();
        }

        [ContextMenu("스킨 다시 적용")]
        public void Apply()
        {
            if (_skin == null) return;

            if (_panel != null)
                SetImage(_panel, _skin.panelSprite, _skin.panelColor);

            if (_menuButtons != null)
            {
                foreach (var button in _menuButtons)
                {
                    if (button == null) continue;
                    SetImage(button.image, _skin.buttonSprite, _skin.buttonColor);
                    SetPressedSprite(button, _skin.buttonPressedSprite);
                    SetButtonText(button, true);
                }
            }

            if (_settingsButton != null)
            {
                bool hasIcon = _skin.settingsIconSprite != null;
                SetImage(_settingsButton.image, _skin.settingsIconSprite, _skin.settingsIconColor);
                _settingsButton.image.preserveAspect = hasIcon;   // 아이콘 비율 유지
                SetButtonText(_settingsButton, !hasIcon);         // 아이콘이 있으면 '설정' 글자 숨김
            }

            if (_labels != null)
            {
                foreach (var label in _labels)
                {
                    if (label == null) continue;
                    if (_skin.font != null) label.font = _skin.font;
                    label.color = _skin.labelColor;
                }
            }

            if (_sliders != null)
            {
                foreach (var slider in _sliders)
                {
                    if (slider == null) continue;
                    var track = slider.transform.Find("Background");
                    if (track != null) SetImage(track.GetComponent<Image>(), _skin.sliderTrackSprite, _skin.sliderTrackColor);
                    if (slider.fillRect != null) SetImage(slider.fillRect.GetComponent<Image>(), _skin.sliderFillSprite, _skin.sliderFillColor);
                    if (slider.handleRect != null)
                    {
                        var handle = slider.handleRect.GetComponent<Image>();
                        SetImage(handle, _skin.sliderHandleSprite, _skin.sliderHandleColor);
                        if (handle != null) handle.preserveAspect = _skin.sliderHandleSprite != null;
                    }
                }
            }

            if (_switches != null)
            {
                foreach (var sw in _switches)
                {
                    if (sw == null) continue;
                    SetImage(sw.Background, _skin.switchBackgroundSprite, Color.white);
                    SetImage(sw.KnobImage, _skin.switchKnobSprite, _skin.switchKnobColor);
                    sw.SetColors(_skin.switchOnColor, _skin.switchOffColor); // 배경색은 스위치가 상태에 따라 칠함
                }
            }
        }

        private static void SetImage(Image image, Sprite sprite, Color color)
        {
            if (image == null) return;
            image.sprite = sprite;
            image.color = color;
            // 테두리(9-slice)가 지정된 스프라이트면 늘려도 모서리가 안 깨지게 Sliced
            image.type = sprite != null && sprite.border != Vector4.zero
                ? Image.Type.Sliced
                : Image.Type.Simple;
        }

        private static void SetPressedSprite(Button button, Sprite pressed)
        {
            if (pressed != null)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                var state = button.spriteState;
                state.pressedSprite = pressed;
                button.spriteState = state;
            }
            else
            {
                button.transition = Selectable.Transition.ColorTint;
            }
        }

        private void SetButtonText(Button button, bool visible)
        {
            var text = button.GetComponentInChildren<TextMeshProUGUI>(true);
            if (text == null) return;
            text.gameObject.SetActive(visible);
            if (_skin.font != null) text.font = _skin.font;
            text.color = _skin.textColor;
        }
    }
}
