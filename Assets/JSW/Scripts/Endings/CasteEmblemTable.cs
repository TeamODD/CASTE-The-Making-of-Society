using System;
using UnityEngine;

namespace Caste.Endings
{
    /// <summary>
    /// 계층 코드(CSV의 caste_code) ↔ 상징물 이미지 표. 인스펙터에서 그림만 바꾸면 도감에 반영된다.
    /// 만들기: Project 창 우클릭 > Create > Caste > Caste Emblem Table
    /// </summary>
    [CreateAssetMenu(menuName = "Caste/Caste Emblem Table", fileName = "CasteEmblemTable")]
    public class CasteEmblemTable : ScriptableObject, ICasteEmblemProvider
    {
        [Serializable]
        public class Row
        {
            [Tooltip("CSV의 caste_code (B, K, V, S, C)")]
            public string casteCode;
            [Tooltip("메모용 이름")]
            public string casteName;
            public Sprite emblem;
        }

        [SerializeField] private Row[] _rows = Array.Empty<Row>();

        public Sprite GetEmblem(string casteCode)
        {
            if (string.IsNullOrEmpty(casteCode) || _rows == null) return null;
            foreach (var row in _rows)
                if (row != null && string.Equals(row.casteCode, casteCode, StringComparison.OrdinalIgnoreCase))
                    return row.emblem;
            return null;
        }
    }
}
