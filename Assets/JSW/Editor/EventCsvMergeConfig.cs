using UnityEngine;

namespace Caste.Integration.EditorTools
{
    /// <summary>
    /// 여러 이벤트 CSV(JMS 메인 + JSW 서브 등)를 하나로 합치는 설정 에셋.
    /// 원본 CSV는 읽기만 하고, 합친 결과는 별도 파일로 만든다 → 팀원 파일을 건드리지 않는다.
    /// 기능을 없애고 싶으면 GameManager의 Csv File을 원래 events.csv로 되돌리기만 하면 된다.
    /// </summary>
    [CreateAssetMenu(menuName = "CASTE/Event CSV Merge Config", fileName = "EventCsvMergeConfig")]
    public class EventCsvMergeConfig : ScriptableObject
    {
        [Tooltip("합칠 CSV 목록. 위에서부터 순서대로 이어 붙이고, 첫 번째 파일의 헤더를 기준으로 쓴다.")]
        public TextAsset[] sources;

        [Tooltip("합친 결과를 저장할 경로 (Assets/로 시작)")]
        public string outputPath = "Assets/JSW/Data/Events/events_merged.csv";

        [Tooltip("켜면 소스 CSV가 바뀔 때마다 자동으로 다시 합친다. 끄면 메뉴 CASTE > 이벤트 CSV 다시 합치기로 수동 실행.")]
        public bool autoRebuild = true;

        [Tooltip("이름만 바뀐 같은 열을 같은 열로 본다. (예: 팀원 CSV는 publicOp, 우리 CSV는 unrest)\n" +
                 "From이 들어간 열 이름을 To로 바꿔서 비교한다. 결과 파일은 첫 번째 CSV의 열 이름을 쓴다.")]
        public HeaderAlias[] headerAliases =
        {
            new HeaderAlias { from = "_unrest_delta", to = "_publicOp_delta" },
        };

        [System.Serializable]
        public struct HeaderAlias
        {
            public string from;
            public string to;
        }
    }
}
