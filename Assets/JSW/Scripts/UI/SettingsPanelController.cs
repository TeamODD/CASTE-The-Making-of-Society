using Caste.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Caste.UI
{
    /// <summary>
    /// 설정창의 UI(슬라이더 / 스위치)와 GameSettings를 연결만 한다. (SRP)
    /// 값이 실제로 어디에 쓰이는지(소리, 타자기)는 모른다 → 각 바인더 담당. (DIP)
    /// 붙이는 곳: 설정 팝업 루트
    /// </summary>
    public class SettingsPanelController : MonoBehaviour
    {
        [SerializeField] private GameSettings _settings;
        [SerializeField] private PopupView _popup;

        [Header("소리")]
        [SerializeField] private Slider _bgmSlider;
        [SerializeField] private Slider _sfxSlider;
        [Tooltip("(선택) 슬라이더 옆 숫자 표시")]
        [SerializeField] private TMP_Text _bgmValueText;
        [SerializeField] private TMP_Text _sfxValueText;

        [Header("타자기 효과")]
        [SerializeField] private ToggleSwitch _typewriterSwitch;

        [Header("닫기")]
        [SerializeField] private Button _closeButton;

        private void Awake()
        {
            if (_settings == null || _popup == null)
            {
                Debug.LogError($"[SettingsPanel] {name}: GameSettings 또는 PopupView가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            _bgmSlider.onValueChanged.AddListener(HandleBgmChanged);
            _sfxSlider.onValueChanged.AddListener(HandleSfxChanged);
            _typewriterSwitch.Toggle.onValueChanged.AddListener(HandleTypewriterChanged);
            if (_closeButton != null) _closeButton.onClick.AddListener(_popup.Close);

            _popup.Opening += RefreshFromSettings;
            _popup.Closing += _settings.Save;
            RefreshFromSettings();
        }

        private void OnDestroy()
        {
            if (_popup == null || _settings == null) return;
            _popup.Opening -= RefreshFromSettings;
            _popup.Closing -= _settings.Save;
        }

        // 모바일은 앱이 백그라운드로 가면 언제든 종료될 수 있어 그때도 저장
        private void OnApplicationPause(bool paused)
        {
            if (paused && _settings != null) _settings.Save();
        }

        /// <summary>창을 열 때 저장된 값을 UI에 표시 (이벤트를 발생시키지 않음)</summary>
        private void RefreshFromSettings()
        {
            _bgmSlider.SetValueWithoutNotify(_settings.BgmVolume);
            _sfxSlider.SetValueWithoutNotify(_settings.SfxVolume);
            _typewriterSwitch.SetIsOnWithoutNotify(_settings.TypewriterEnabled);
            UpdateValueText(_bgmValueText, _settings.BgmVolume);
            UpdateValueText(_sfxValueText, _settings.SfxVolume);
        }

        private void HandleBgmChanged(float value)
        {
            _settings.BgmVolume = value;
            UpdateValueText(_bgmValueText, value);
        }

        private void HandleSfxChanged(float value)
        {
            _settings.SfxVolume = value;
            UpdateValueText(_sfxValueText, value);
        }

        private void HandleTypewriterChanged(bool isOn) => _settings.TypewriterEnabled = isOn;

        private static void UpdateValueText(TMP_Text text, float value)
        {
            if (text != null) text.text = Mathf.RoundToInt(value * 100f).ToString();
        }
    }
}
