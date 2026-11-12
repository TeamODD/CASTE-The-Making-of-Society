using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Caste.UI
{
    /// <summary>
    /// 톱니바퀴 버튼과 메뉴의 3개 버튼(계속하기 / 설정 / 나가기)을 기능에 연결만 한다. (SRP)
    /// 팝업 표시 방법은 PopupView, 종료 방법은 ExitHandler에 맡긴다. (DIP)
    /// 붙이는 곳: 메뉴 팝업 루트
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [Header("팝업")]
        [SerializeField] private PopupView _popup;

        [Header("버튼")]
        [Tooltip("화면 오른쪽 위 톱니바퀴 버튼")]
        [SerializeField] private Button _openButton;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _exitButton;

        [Header("동작")]
        [Tooltip("나가기 방식. ExitHandler를 상속한 컴포넌트를 넣는다 (기본: QuitGameExitHandler)")]
        [SerializeField] private ExitHandler _exitHandler;

        [Tooltip("설정 버튼을 눌렀을 때 실행 (소리 / 타자기 설정창은 다음 작업에서 연결)")]
        public UnityEvent onSettingsClicked;

        private void Awake()
        {
            if (_popup == null)
            {
                Debug.LogError($"[PauseMenu] {name}: PopupView가 연결되지 않았습니다.", this);
                enabled = false;
                return;
            }

            if (_openButton != null) _openButton.onClick.AddListener(_popup.Open);
            if (_continueButton != null) _continueButton.onClick.AddListener(_popup.Close);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(HandleSettings);
            if (_exitButton != null) _exitButton.onClick.AddListener(HandleExit);
        }

        private void OnDestroy()
        {
            if (_popup == null) return;
            if (_openButton != null) _openButton.onClick.RemoveListener(_popup.Open);
            if (_continueButton != null) _continueButton.onClick.RemoveListener(_popup.Close);
            if (_settingsButton != null) _settingsButton.onClick.RemoveListener(HandleSettings);
            if (_exitButton != null) _exitButton.onClick.RemoveListener(HandleExit);
        }

        private void HandleSettings()
        {
            if (onSettingsClicked.GetPersistentEventCount() == 0)
                Debug.Log("[PauseMenu] 설정창은 아직 연결되지 않았습니다.");
            onSettingsClicked.Invoke();
        }

        private void HandleExit()
        {
            if (_exitHandler == null)
            {
                Debug.LogWarning("[PauseMenu] ExitHandler가 지정되지 않았습니다.", this);
                return;
            }
            _exitHandler.Exit();
        }
    }
}
