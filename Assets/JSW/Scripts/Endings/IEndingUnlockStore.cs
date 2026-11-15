using UnityEngine;

namespace Caste.Endings
{
    /// <summary>
    /// 엔딩 해금 기록을 읽고 쓰는 방법. 도감 화면은 이 인터페이스만 알고 저장 방식은 모른다. (DIP)
    /// 나중에 팀원 백엔드 저장 시스템으로 바꾸려면 이 인터페이스를 구현한 클래스만 새로 만들면 된다. (OCP)
    /// </summary>
    public interface IEndingUnlockStore
    {
        bool IsUnlocked(string endingId);
        void Unlock(string endingId);
    }

    /// <summary>기본 구현: PlayerPrefs에 저장.</summary>
    public class PlayerPrefsEndingUnlockStore : IEndingUnlockStore
    {
        private const string Prefix = "Caste.Ending.Unlocked.";

        public bool IsUnlocked(string endingId) => PlayerPrefs.GetInt(Prefix + endingId, 0) == 1;

        public void Unlock(string endingId)
        {
            PlayerPrefs.SetInt(Prefix + endingId, 1);
            PlayerPrefs.Save();
        }
    }
}
