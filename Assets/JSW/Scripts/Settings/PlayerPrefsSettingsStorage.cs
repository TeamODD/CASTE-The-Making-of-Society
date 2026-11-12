using UnityEngine;

namespace Caste.Settings
{
    /// <summary>PlayerPrefs(기기 내부 저장소)에 설정을 저장한다.</summary>
    public class PlayerPrefsSettingsStorage : ISettingsStorage
    {
        public float GetFloat(string key, float defaultValue) => PlayerPrefs.GetFloat(key, defaultValue);
        public void SetFloat(string key, float value) => PlayerPrefs.SetFloat(key, value);
        public bool GetBool(string key, bool defaultValue) => PlayerPrefs.GetInt(key, defaultValue ? 1 : 0) != 0;
        public void SetBool(string key, bool value) => PlayerPrefs.SetInt(key, value ? 1 : 0);
        public void Save() => PlayerPrefs.Save();
    }
}
