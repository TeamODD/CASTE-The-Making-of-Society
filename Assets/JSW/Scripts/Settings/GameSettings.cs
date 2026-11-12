using System;
using UnityEngine;

namespace Caste.Settings
{
    /// <summary>
    /// 게임 설정값(배경음 / 효과음 / 타자기)의 단일 저장소.
    /// UI는 값을 "바꾸기만" 하고, 기능 쪽은 이벤트를 "듣기만" 한다 → 서로 모름. (SRP, DIP)
    /// 소리는 아직 없으므로 값만 저장된다. 사운드가 생기면 BgmVolumeChanged / SfxVolumeChanged를 구독하면 된다.
    /// 인스펙터에는 기본값만 두고, 실제 값은 저장소(PlayerPrefs)에서 읽는다.
    /// </summary>
    [CreateAssetMenu(fileName = "GameSettings", menuName = "Caste/Settings/Game Settings")]
    public class GameSettings : ScriptableObject
    {
        [Header("기본값 (처음 실행했을 때)")]
        [Range(0f, 1f)] [SerializeField] private float _defaultBgmVolume = 0.8f;
        [Range(0f, 1f)] [SerializeField] private float _defaultSfxVolume = 0.8f;
        [SerializeField] private bool _defaultTypewriterEnabled = true;

        [Header("저장 키 앞부분 (보통 건드릴 필요 없음)")]
        [SerializeField] private string _keyPrefix = "Caste.Settings.";

        public event Action<float> BgmVolumeChanged;
        public event Action<float> SfxVolumeChanged;
        public event Action<bool> TypewriterEnabledChanged;

        // 플레이 중 바꾼 값이 에셋 파일에 남지 않도록 런타임 값은 직렬화하지 않음
        [NonSerialized] private bool _loaded;
        [NonSerialized] private float _bgmVolume;
        [NonSerialized] private float _sfxVolume;
        [NonSerialized] private bool _typewriterEnabled;
        [NonSerialized] private ISettingsStorage _storage;

        private ISettingsStorage Storage => _storage ??= new PlayerPrefsSettingsStorage();

        /// <summary>저장 방식 교체 (테스트용 가짜 저장소 등).</summary>
        public void SetStorage(ISettingsStorage storage)
        {
            _storage = storage;
            _loaded = false;
        }

        public float BgmVolume
        {
            get { EnsureLoaded(); return _bgmVolume; }
            set
            {
                EnsureLoaded();
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(_bgmVolume, value)) return;
                _bgmVolume = value;
                Storage.SetFloat(Key("BgmVolume"), value);
                BgmVolumeChanged?.Invoke(value);
            }
        }

        public float SfxVolume
        {
            get { EnsureLoaded(); return _sfxVolume; }
            set
            {
                EnsureLoaded();
                value = Mathf.Clamp01(value);
                if (Mathf.Approximately(_sfxVolume, value)) return;
                _sfxVolume = value;
                Storage.SetFloat(Key("SfxVolume"), value);
                SfxVolumeChanged?.Invoke(value);
            }
        }

        public bool TypewriterEnabled
        {
            get { EnsureLoaded(); return _typewriterEnabled; }
            set
            {
                EnsureLoaded();
                if (_typewriterEnabled == value) return;
                _typewriterEnabled = value;
                Storage.SetBool(Key("TypewriterEnabled"), value);
                TypewriterEnabledChanged?.Invoke(value);
            }
        }

        /// <summary>디스크에 확정 저장. 설정창을 닫을 때 / 앱이 백그라운드로 갈 때 호출.</summary>
        public void Save() => Storage.Save();

        [ContextMenu("기본값으로 초기화")]
        public void ResetToDefaults()
        {
            BgmVolume = _defaultBgmVolume;
            SfxVolume = _defaultSfxVolume;
            TypewriterEnabled = _defaultTypewriterEnabled;
            Save();
        }

        private void OnEnable() => _loaded = false;

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            _bgmVolume = Storage.GetFloat(Key("BgmVolume"), _defaultBgmVolume);
            _sfxVolume = Storage.GetFloat(Key("SfxVolume"), _defaultSfxVolume);
            _typewriterEnabled = Storage.GetBool(Key("TypewriterEnabled"), _defaultTypewriterEnabled);
        }

        private string Key(string name) => _keyPrefix + name;
    }
}
