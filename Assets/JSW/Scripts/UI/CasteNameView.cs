using TMPro;
using UnityEngine;

namespace Caste.UI
{
    /// <summary>
    /// 현재 계층 이름을 "[ 크샤트리아 ]" 같은 형식으로 보여준다. 표시만 담당. (SRP)
    /// BackendCardGameController.onCasteNameShown 같은 이벤트에 ShowName을 연결해서 쓴다.
    /// 붙이는 곳: 계층 이름을 보여줄 TMP 텍스트 오브젝트
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [AddComponentMenu("Caste/UI/Caste Name View")]
    public class CasteNameView : MonoBehaviour
    {
        [Tooltip("{0} = 계층 이름. 예: \"[ {0} ]\", \"{0}\", \"{0} 계급\"")]
        [SerializeField] private string _format = "[ {0} ]";

        private TMP_Text _text;

        private void Awake()
        {
            _text = GetComponent<TMP_Text>();
            _text.text = string.Empty; // 첫 카드가 나오기 전에는 비워둔다
        }

        public void ShowName(string casteName)
        {
            _text.text = string.IsNullOrEmpty(casteName) ? string.Empty : string.Format(_format, casteName);
        }

        // 에디터에서 형식을 바꾸면 예시로 미리보기
        private void OnValidate()
        {
            var text = GetComponent<TMP_Text>();
            if (text != null && !Application.isPlaying) text.text = string.Format(_format, "크샤트리아");
        }
    }
}
