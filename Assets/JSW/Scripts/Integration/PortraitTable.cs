using System;
using UnityEngine;

namespace Caste.Integration
{
    /// <summary>
    /// 화자 이름 → 초상화 표. 아트가 나오면 인스펙터에서 한 줄씩 추가만 하면 된다. (코드 수정 없음)
    /// 생성: Create → CASTE → Portrait Table
    /// </summary>
    [CreateAssetMenu(fileName = "PortraitTable", menuName = "CASTE/Portrait Table")]
    public class PortraitTable : ScriptableObject, IPortraitProvider
    {
        [Serializable]
        private struct Entry
        {
            [Tooltip("CSV의 speaker 열과 똑같이 입력 (예: 수도원 기록관)")]
            public string speaker;
            public Sprite portrait;
        }

        [SerializeField] private Entry[] _entries = Array.Empty<Entry>();
        [Tooltip("표에 없는 화자에게 쓸 기본 초상화 (비워두면 카드의 임시 사각형 유지)")]
        [SerializeField] private Sprite _fallback;

        public Sprite GetPortrait(string speaker)
        {
            foreach (var e in _entries)
                if (e.speaker == speaker) return e.portrait;
            return _fallback;
        }
    }
}
