using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Caste.Endings
{
    /// <summary>
    /// 도감의 엔딩 칸 하나를 그리고, 눌렸다는 사실만 알린다 (표시 + 클릭 알림, SRP).
    /// 눌렀을 때 무엇을 할지(엔딩 다시 보기 등)는 EndingCollectionView가 정한다.
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
        [Tooltip("(선택) 계층 상징물 아이콘")]
        [SerializeField] private Image _emblem;
        [Tooltip("켜면 잠긴 칸에도 상징물을 보여준다 (어느 계층 엔딩인지 힌트)")]
        [SerializeField] private bool _showEmblemWhenLocked = true;
        [Tooltip("(선택) 칸을 눌러 엔딩을 다시 볼 때 쓰는 버튼. 비워두면 클릭 기능 없음")]
        [SerializeField] private Button _button;

        [Header("표시")]
        [Tooltip("번호 형식. {0} = 엔딩 번호. 비우면 번호를 표시하지 않음")]
        [SerializeField] private string _numberFormat = "";
        [Tooltip("잠긴 엔딩 이름 대신 보일 글자")]
        [SerializeField] private string _lockedTitle = "???";
        [Tooltip("켜면 잠긴 칸에도 엔딩 이름(예: [ 크샤트리아 ] 엔딩1)을 보여준다. 끄면 위의 잠금 글자")]
        [SerializeField] private bool _showTitleWhenLocked = true;
        [SerializeField] private Color _lockedColor = new Color(0.12f, 0.10f, 0.08f, 0.9f);
        [SerializeField] private Color _unlockedColor = new Color(0.35f, 0.27f, 0.18f, 1f);

        /// <summary>칸이 눌렸을 때 (해금된 칸만 눌림)</summary>
        public event Action<EndingSlotView> Clicked;

        private void Awake()
        {
            if (_button != null) _button.onClick.AddListener(HandleClick);
        }

        private void OnDestroy()
        {
            if (_button != null) _button.onClick.RemoveListener(HandleClick);
        }

        public void ShowLocked(int number, Sprite emblem = null, string title = null)
        {
            gameObject.SetActive(true);
            string shown = _showTitleWhenLocked && !string.IsNullOrEmpty(title) ? title : _lockedTitle;
            SetCommon(number, shown, _lockedColor, locked: true);
            if (_illustration != null) _illustration.enabled = false;
            SetEmblem(_showEmblemWhenLocked ? emblem : null);
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
            SetEmblem(entry?.casteEmblem);
        }

        private void SetEmblem(Sprite sprite)
        {
            if (_emblem == null) return;
            _emblem.sprite = sprite;
            _emblem.preserveAspect = true;
            _emblem.enabled = sprite != null;
        }

        /// <summary>마지막 페이지에서 남는 칸 숨김.</summary>
        public void Hide() => gameObject.SetActive(false);

        private void SetCommon(int number, string title, Color bg, bool locked)
        {
            if (_numberText != null) _numberText.text = string.Format(_numberFormat, number);
            if (_titleText != null) _titleText.text = title;
            if (_background != null) _background.color = bg;
            if (_lockedMark != null) _lockedMark.SetActive(locked);
            if (_button != null) _button.interactable = !locked; // 잠긴 칸은 누를 수 없음
        }

        private void HandleClick() => Clicked?.Invoke(this);
    }
}
