using Caste.Effects;
using UnityEngine;

namespace Caste.Settings
{
    /// <summary>
    /// 설정의 "타자기 효과 ON/OFF"를 TypewriterEffect에 전달한다.
    /// TypewriterEffect는 설정 시스템을 모르고, 이 바인더가 둘을 이어준다. (SRP, DIP)
    /// 붙이는 곳: TypewriterEffect가 있는 오브젝트
    /// </summary>
    [DefaultExecutionOrder(-100)] // TypewriterEffect가 재생을 시작하기 전에 먼저 값을 넣기 위함
    [RequireComponent(typeof(TypewriterEffect))]
    [AddComponentMenu("Caste/Settings/Typewriter Setting Binder")]
    public class TypewriterSettingBinder : MonoBehaviour
    {
        [SerializeField] private GameSettings _settings;

        private TypewriterEffect _effect;

        private void Awake() => _effect = GetComponent<TypewriterEffect>();

        private void OnEnable()
        {
            if (_settings == null)
            {
                Debug.LogError($"[TypewriterSettingBinder] {name}: GameSettings가 연결되지 않았습니다.", this);
                return;
            }
            _settings.TypewriterEnabledChanged += HandleChanged;
            HandleChanged(_settings.TypewriterEnabled);
        }

        private void OnDisable()
        {
            if (_settings != null) _settings.TypewriterEnabledChanged -= HandleChanged;
        }

        private void HandleChanged(bool isOn) => _effect.TypingEnabled = isOn;
    }
}
