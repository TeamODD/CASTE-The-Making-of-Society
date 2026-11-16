using Caste.Scenes;
using Caste.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Caste.Title
{
    /// <summary>
    /// 타이틀 화면의 버튼(게임 시작 / 엔딩 도감 / 설정 / 나가기)을 기능에 연결만 한다. (SRP)
    /// 씬 전환 연출은 SceneTransitioner, 도감·설정창은 PopupView, 종료는 ExitHandler에 맡긴다. (DIP)
    /// 붙이는 곳: 타이틀 메뉴 루트
    /// </summary>
    [AddComponentMenu("Caste/Title/Title Menu Controller")]
    public class TitleMenuController : MonoBehaviour
    {
        [Header("버튼")]
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _collectionButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _exitButton;

        [Header("게임 시작")]
        [Tooltip("시작 버튼을 누르면 넘어갈 씬 이름 (File > Build Profiles 목록에 있어야 함)")]
        [SerializeField] private string _gameSceneName = "JSW_BackendTest";
        [Tooltip("페이드 연출 담당. 비워두면 씬에 있는 것을 자동으로 찾고, 없으면 페이드 없이 바로 넘어감")]
        [SerializeField] private SceneTransitioner _transitioner;

        [Header("팝업 / 나가기")]
        [Tooltip("엔딩 도감 버튼으로 열 팝업")]
        [SerializeField] private PopupView _collectionPopup;
        [Tooltip("설정 버튼으로 열 팝업 (게임 화면과 같은 설정창)")]
        [SerializeField] private PopupView _settingsPopup;
        [Tooltip("나가기 방식 (기본: QuitGameExitHandler)")]
        [SerializeField] private ExitHandler _exitHandler;

        private void Awake()
        {
            if (_startButton != null) _startButton.onClick.AddListener(HandleStart);
            if (_collectionButton != null) _collectionButton.onClick.AddListener(HandleCollection);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(HandleSettings);
            if (_exitButton != null) _exitButton.onClick.AddListener(HandleExit);
        }

        private void OnDestroy()
        {
            if (_startButton != null) _startButton.onClick.RemoveListener(HandleStart);
            if (_collectionButton != null) _collectionButton.onClick.RemoveListener(HandleCollection);
            if (_settingsButton != null) _settingsButton.onClick.RemoveListener(HandleSettings);
            if (_exitButton != null) _exitButton.onClick.RemoveListener(HandleExit);
        }

        private void HandleStart()
        {
            var transitioner = _transitioner != null ? _transitioner : SceneTransitioner.Instance;
            if (transitioner != null) transitioner.LoadScene(_gameSceneName);
            else SceneManager.LoadScene(_gameSceneName);
        }

        private void HandleCollection() => OpenPopup(_collectionPopup, "엔딩 도감");
        private void HandleSettings() => OpenPopup(_settingsPopup, "설정");

        private void OpenPopup(PopupView popup, string label)
        {
            if (popup != null) popup.Open();
            else Debug.LogWarning($"[TitleMenu] {label} 팝업이 연결되지 않았습니다.", this);
        }

        private void HandleExit()
        {
            if (_exitHandler != null) _exitHandler.Exit();
            else Debug.LogWarning("[TitleMenu] ExitHandler가 연결되지 않았습니다.", this);
        }
    }
}
