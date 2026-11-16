namespace Caste.Integration
{
    // using을 namespace 안에 둔다: 전역 타입(JMS)과 이름이 겹쳐도 Caste 쪽을 먼저 찾게 하기 위함.
    using System;
    using Caste.Endings;
    using Caste.UI;
    using UnityEngine;
    using BackendManager = global::GameManager;

    /// <summary>
    /// 엔딩 도감 화면(JSW)에 백엔드(JMS)의 엔딩 목록과 해금 기록을 넣어준다.
    /// 도감을 열 때마다 백엔드에서 새로 받아오므로, 방금 본 엔딩이 바로 반영된다.
    /// 도감 화면 코드는 바꾸지 않고 데이터 공급처만 바꿔 끼운다. (OCP, DIP)
    /// 붙이는 곳: EndingCollectionView와 같은 오브젝트
    /// </summary>
    [AddComponentMenu("Caste/Integration/Backend Ending Collection Binder")]
    public class BackendEndingCollectionBinder : MonoBehaviour
    {
        [SerializeField] private BackendManager _gameManager;
        [SerializeField] private EndingCollectionView _view;
        [Tooltip("이 팝업이 열릴 때마다 백엔드 목록을 다시 받아온다")]
        [SerializeField] private PopupView _popup;

        [Header("칸 제목")]
        [Tooltip("켜면 제목 앞에 계층 이름을 붙인다. 예: [브라만] ...")]
        [SerializeField] private bool _showCasteName = true;
        [Tooltip("엔딩 문장 첫 줄에서 몇 글자까지 제목으로 쓸지")]
        [SerializeField, Min(4)] private int _titleLength = 14;

        // Start가 아니라 Awake: 팝업이 처음 열리는 순간(Opening)부터 확실히 구독하기 위함
        private void Awake()
        {
            if (_popup != null) _popup.Opening += Reload;
            Reload();
        }

        private void OnDestroy()
        {
            if (_popup != null) _popup.Opening -= Reload;
        }

        public void Reload()
        {
            if (_gameManager == null || _view == null) return;
            try
            {
                var list = new BackendEndingList(_gameManager.GetEndingCollection(), _showCasteName, _titleLength);
                _view.SetSource(list, list);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }
    }
}
