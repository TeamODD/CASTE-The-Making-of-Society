using System;
using UnityEngine;

namespace Caste.Tutorial
{
    /// <summary>튜토리얼 한 단계의 데이터. 인스펙터에서 문장·시점·연출을 고친다. (코드 수정 불필요)</summary>
    [Serializable]
    public class TutorialStep
    {
        [Tooltip("메모용 이름 (게임에는 안 나옴)")]
        public string memo;

        [Tooltip("몇 번 선택한 뒤에 나올지. 0 = 첫 이벤트 전, 1 = 첫 선택 후(두 번째 이벤트 전) ...")]
        [Min(0)] public int beforeChoice;

        [Tooltip("이벤트 문장 칸에 차례로 나올 문장. 한 칸 = 한 페이지 (탭하면 다음 페이지)")]
        [TextArea(3, 8)] public string[] pages;

        [Tooltip("켜면 화면을 어둡게 하고 Highlights만 밝게 보여준다")]
        public bool dimBackground;

        [Tooltip("어두워질 때 밝게 남길 UI (문장 칸은 자동으로 포함)")]
        public RectTransform[] highlights;
    }
}
