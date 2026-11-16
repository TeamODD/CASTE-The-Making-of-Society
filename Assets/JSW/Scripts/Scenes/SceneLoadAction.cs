using UnityEngine;
using UnityEngine.SceneManagement;

namespace Caste.Scenes
{
    /// <summary>
    /// 버튼·UnityEvent에서 "이 씬으로 이동"을 연결하기 위한 작은 컴포넌트.
    /// SceneTransitioner는 타이틀 씬에서 만들어져 씬을 넘어 살아있기 때문에
    /// 다른 씬의 인스펙터에서는 직접 연결할 수 없다 → 이 컴포넌트가 대신 찾아서 호출한다. (DIP)
    /// 붙이는 곳: 아무 오브젝트 (UnityEvent에 Load()를 연결)
    /// </summary>
    [AddComponentMenu("Caste/Scenes/Scene Load Action")]
    public class SceneLoadAction : MonoBehaviour
    {
        [Tooltip("이동할 씬 이름 (File > Build Profiles 목록에 있어야 함)")]
        [SerializeField] private string _sceneName = "JSW_Title";

        /// <summary>페이드 전환기가 있으면 페이드로, 없으면(게임 씬을 바로 플레이한 경우) 즉시 이동.</summary>
        public void Load()
        {
            var transitioner = SceneTransitioner.Instance;
            if (transitioner != null) transitioner.LoadScene(_sceneName);
            else SceneManager.LoadScene(_sceneName);
        }
    }
}
