using UnityEngine;

namespace Caste.Endings
{
    /// <summary>
    /// 계층 코드 → 상징물 스프라이트. 도감은 이 인터페이스만 알고 표가 어디 있는지는 모른다. (DIP)
    /// </summary>
    public interface ICasteEmblemProvider
    {
        Sprite GetEmblem(string casteCode);
    }
}
