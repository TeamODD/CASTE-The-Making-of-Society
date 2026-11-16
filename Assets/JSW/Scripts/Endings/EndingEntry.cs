using System;
using UnityEngine;

namespace Caste.Endings
{
    /// <summary>
    /// 엔딩 한 개의 정보. (지금은 비어 있고, 엔딩 데이터가 나오면 EndingCatalog에 채운다)
    /// </summary>
    [Serializable]
    public class EndingEntry
    {
        [Tooltip("저장 키로 쓰는 고유 ID. 한 번 정하면 바꾸지 말 것 (바꾸면 해금 기록이 사라짐)")]
        public string id;
        [Tooltip("도감에 보일 엔딩 이름")]
        public string title;
        [TextArea(2, 5)]
        [Tooltip("도감 상세 설명 (나중에 상세 화면용)")]
        public string description;
        [Tooltip("엔딩 일러스트 (선택)")]
        public Sprite illustration;
    }
}
