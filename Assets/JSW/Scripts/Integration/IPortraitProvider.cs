using UnityEngine;

namespace Caste.Integration
{
    /// <summary>화자 이름으로 초상화를 찾아준다. (CSV에는 초상화 정보가 없기 때문)</summary>
    public interface IPortraitProvider
    {
        Sprite GetPortrait(string speaker);
    }
}
