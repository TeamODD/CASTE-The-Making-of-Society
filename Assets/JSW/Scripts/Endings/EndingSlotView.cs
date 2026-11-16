using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Caste.Endings
{
    /// <summary>
    /// 도감의 엔딩 칸 하나를 그린다 (표시만 담당, SRP).
    /// 붙이는 곳: 도감 칸 오브젝트
    /// </summary>
    [AddComponentMenu("Caste/Endings/Ending Slot View")]
    public class EndingSlotView : MonoBehaviour
    {
        [Header("구성 요소")]
        [SerializeField] private Image _background;
        [SerializeField] private TMP_Text _numberText;
        [SerializeField] private TMP_Text _titleText;
        [Tooltip("잠겼을 때만 보이는 표시 (자물쇠 아이콘/글자)")]
        [SerializeField] private GameObject _lockedMark;
        [Tooltip("(선택) 해금 시 보일 일러스트")]
        [SerializeField] private Image _illustration;

        [Header("표시")]
        [Tooltip("번호 형식. {0} = 엔딩 번호")]
        [SerializeField] private string _numberFormat = "No.{0:00}";
        [Tooltip("잠긴 엔딩 이름 대신 보일 글자")]
        [SerializeField] private string _lockedTitle = "???";
        [SerializeField] private Color _lockedColor = new Color(0.12f, 0.10f, 0.08f, 0.9f);
        [SerializeField] private Color _unlockedColor = new Color(0.35f, 0.27f, 0.18f, 1f);

        public void ShowLocked(int number)
        {
            gameObject.SetActive(true);
            SetCommon(number, _lockedTitle, _lockedColor, locked: true);
            if (_illustration != null) _illustration.enabled = false;
        }

        public void ShowUnlocked(int number, EndingEntry entry)
        {
            gameObject.SetActive(true);
            string title = entry != null && !string.IsNullOrEmpty(entry.title) ? entry.title : $"엔딩 {number}";
            SetCommon(number, title, _unlockedColor, locked: false);

            if (_illustration != null)
            {
                _illustration.sprite = entry?.illustration;
                _illustration.enabled = _illustration.sprite != null;
            }
        }

        /// <summary>마지막 페이지에서 남는 칸 숨김.</summary>
        public void Hide() => gameObject.SetActive(false);

        private void SetCommon(int number, string title, Color bg, bool locked)
        {
            if (_numberText != null) _numberText.text = string.Format(_numberFormat, number);
            if (_titleText != null) _titleText.text = title;
            if (_background != null) _background.color = bg;
            if (_lockedMark != null) _lockedMark.SetActive(locked);
        }
    }
}
