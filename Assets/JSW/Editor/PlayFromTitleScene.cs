using UnityEditor;
using UnityEditor.SceneManagement;

namespace Caste.EditorTools
{
    /// <summary>
    /// 에디터에서 Play를 누르면 지금 열린 씬과 상관없이 타이틀 씬부터 시작하게 한다.
    /// 설정은 이 컴퓨터에만 저장(EditorPrefs)되므로 팀원의 에디터에는 영향이 없다.
    /// 메뉴: Caste > Play From Title Scene (체크로 켜고 끔)
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromTitleScene
    {
        private const string MenuPath = "Caste/Play From Title Scene";
        private const string PrefKey = "Caste.PlayFromTitleScene";
        private const string TitleScenePath = "Assets/JSW/Scenes/JSW_Title.unity";

        static PlayFromTitleScene() => EditorApplication.delayCall += Apply;

        private static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefKey, false);
            set => EditorPrefs.SetBool(PrefKey, value);
        }

        [MenuItem(MenuPath)]
        private static void ToggleMenu()
        {
            Enabled = !Enabled;
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleMenuValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        private static void Apply()
        {
            EditorSceneManager.playModeStartScene = Enabled
                ? AssetDatabase.LoadAssetAtPath<SceneAsset>(TitleScenePath)
                : null;
        }
    }
}
