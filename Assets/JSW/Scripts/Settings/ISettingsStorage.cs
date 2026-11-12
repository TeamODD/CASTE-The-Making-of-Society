namespace Caste.Settings
{
    /// <summary>
    /// 설정값을 어디에 저장할지 추상화. (DIP)
    /// 지금은 PlayerPrefs, 나중에 파일 / 클라우드 저장으로 바꿔도 GameSettings는 그대로. (OCP)
    /// </summary>
    public interface ISettingsStorage
    {
        float GetFloat(string key, float defaultValue);
        void SetFloat(string key, float value);
        bool GetBool(string key, bool defaultValue);
        void SetBool(string key, bool value);
        void Save();
    }
}
