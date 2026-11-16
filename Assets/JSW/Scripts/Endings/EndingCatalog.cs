using System.Collections.Generic;
using UnityEngine;

namespace Caste.Endings
{
    /// <summary>
    /// 게임에 있는 엔딩 목록 (데이터만 담당, SRP).
    /// 엔딩 데이터가 아직 없으면 Placeholder Count만큼 "잠긴 빈칸"으로 취급한다.
    /// 만드는 법: Project 창 우클릭 > Create > Caste > Ending Catalog
    /// </summary>
    [CreateAssetMenu(menuName = "Caste/Ending Catalog", fileName = "EndingCatalog")]
    public class EndingCatalog : ScriptableObject
    {
        [Tooltip("엔딩 데이터가 비어 있을 때 도감에 보여줄 칸 수")]
        [SerializeField, Min(0)] private int _placeholderCount = 50;

        [Tooltip("실제 엔딩 데이터. 하나라도 들어가면 이 목록 개수가 도감 칸 수가 된다")]
        [SerializeField] private List<EndingEntry> _entries = new List<EndingEntry>();

        public int Count => _entries.Count > 0 ? _entries.Count : _placeholderCount;

        /// <summary>해당 번호의 엔딩 데이터 (아직 데이터가 없으면 false)</summary>
        public bool TryGet(int index, out EndingEntry entry)
        {
            bool ok = index >= 0 && index < _entries.Count && _entries[index] != null;
            entry = ok ? _entries[index] : null;
            return ok;
        }

        /// <summary>저장용 ID. 데이터가 없으면 "ending_01" 같은 기본 ID.</summary>
        public string GetId(int index)
        {
            if (TryGet(index, out var e) && !string.IsNullOrEmpty(e.id)) return e.id;
            return $"ending_{index + 1:00}";
        }
    }
}
