using Caste.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Caste.Endings
{
    /// <summary>
    /// 엔딩 도감 화면: 한 페이지에 칸 N개(기본 10개)를 보여주고 &lt; 1 / 5 &gt; 로 넘긴다.
    /// 엔딩 목록은 EndingCatalog, 해금 여부는 IEndingUnlockStore, 칸 그리기는 EndingSlotView에 맡기고
    /// 이 클래스는 "페이지 계산과 연결"만 한다. (SRP, DIP)
    /// 한 페이지 칸 수 = Slots 배열 크기 → 칸을 늘리거나 줄이면 페이지 수가 자동으로 바뀐다. (OCP)
    /// 붙이는 곳: 도감 팝업 루트 (PopupView와 같은 오브젝트)
    /// </summary>
    [AddComponentMenu("Caste/Endings/Ending Collection View")]
    public class EndingCollectionView : MonoBehaviour
    {
        [Header("데이터")]
        [Tooltip("기본 엔딩 목록. 백엔드 연결(BackendEndingCollectionBinder)을 쓰면 실행 중에 백엔드 목록으로 바뀐다")]
        [SerializeField] private EndingCatalog _catalog;

        [Header("구성 요소")]
        [SerializeField] private PopupView _popup;
        [Tooltip("한 페이지에 보일 칸들 (순서대로)")]
        [SerializeField] private EndingSlotView[] _slots;
        [SerializeField] private Button _prevButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _pageText;
        [Tooltip("(선택) 수집 현황 글자")]
        [SerializeField] private TMP_Text _progressText;

        [Header("표시")]
        [Tooltip("{0} = 현재 페이지, {1} = 전체 페이지")]
        [SerializeField] private string _pageFormat = "{0} / {1}";
        [Tooltip("{0} = 해금한 수, {1} = 전체 수")]
        [SerializeField] private string _progressFormat = "수집 {0} / {1}";

        [Header("동작")]
        [Tooltip("켜면 마지막 페이지에서 > 를 누르면 첫 페이지로")]
        [SerializeField] private bool _wrapAround = false;
        [Tooltip("켜면 열 때마다 1페이지부터")]
        [SerializeField] private bool _resetPageOnOpen = true;

        private IEndingUnlockStore _store = new PlayerPrefsEndingUnlockStore();
        private IEndingList _list;
        private int _page;

        private int PerPage => Mathf.Max(1, _slots?.Length ?? 0);
        private int PageCount => Mathf.Max(1, Mathf.CeilToInt((_list != null ? _list.Count : 0) / (float)PerPage));

        /// <summary>저장 방식을 바꾸고 싶을 때 (예: 팀원 백엔드 저장소).</summary>
        public void SetUnlockStore(IEndingUnlockStore store)
        {
            _store = store ?? new PlayerPrefsEndingUnlockStore();
            Refresh();
        }

        /// <summary>엔딩 목록과 해금 기록을 함께 바꾼다. (예: 백엔드 도감 데이터)</summary>
        public void SetSource(IEndingList list, IEndingUnlockStore store)
        {
            _list = list ?? _catalog;
            _store = store ?? new PlayerPrefsEndingUnlockStore();
            _page = Mathf.Clamp(_page, 0, PageCount - 1);
            Refresh();
        }

        private void Awake()
        {
            if (_list == null) _list = _catalog;
            if (_popup == null)
            {
                Debug.LogError($"[EndingCollection] {name}: PopupView가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            if (_prevButton != null) _prevButton.onClick.AddListener(PrevPage);
            if (_nextButton != null) _nextButton.onClick.AddListener(NextPage);
            if (_closeButton != null) _closeButton.onClick.AddListener(_popup.Close);
            _popup.Opening += HandleOpening;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_popup != null) _popup.Opening -= HandleOpening;
        }

        public void Open() => _popup.Open();

        public void NextPage() => GoTo(_page + 1);
        public void PrevPage() => GoTo(_page - 1);

        private void HandleOpening()
        {
            if (_resetPageOnOpen) _page = 0;
            Refresh();
        }

        private void GoTo(int page)
        {
            int count = PageCount;
            _page = _wrapAround ? (page % count + count) % count : Mathf.Clamp(page, 0, count - 1);
            Refresh();
        }

        public void Refresh()
        {
            if (_list == null || _slots == null) return;

            int total = _list.Count;
            int start = _page * PerPage;

            for (int i = 0; i < _slots.Length; i++)
            {
                var slot = _slots[i];
                if (slot == null) continue;

                int index = start + i;
                if (index >= total) { slot.Hide(); continue; }

                bool unlocked = _store.IsUnlocked(_list.GetId(index));
                if (unlocked && _list.TryGet(index, out var entry)) slot.ShowUnlocked(index + 1, entry);
                else if (unlocked) slot.ShowUnlocked(index + 1, null);
                else slot.ShowLocked(index + 1);
            }

            if (_pageText != null) _pageText.text = string.Format(_pageFormat, _page + 1, PageCount);

            if (_progressText != null)
            {
                int got = 0;
                for (int i = 0; i < total; i++) if (_store.IsUnlocked(_list.GetId(i))) got++;
                _progressText.text = string.Format(_progressFormat, got, total);
            }

            if (!_wrapAround)
            {
                if (_prevButton != null) _prevButton.interactable = _page > 0;
                if (_nextButton != null) _nextButton.interactable = _page < PageCount - 1;
            }
        }
    }
}
