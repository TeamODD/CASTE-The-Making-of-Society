using UnityEngine;

namespace Caste.UI
{
    /// <summary>앱을 종료한다. 에디터에서는 플레이 모드를 멈춘다.</summary>
    public class QuitGameExitHandler : ExitHandler
    {
        public override void Exit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
