using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Caste.Ending
{
    /// <summary>
    /// 테스트용: 단축키(기본 F10)로 엔딩 화면을 켜고 끈다.
    /// 실제 엔딩 연결(스탯 0/100 등)이 생기면 이 컴포넌트만 지우면 된다. (SRP — 엔딩 코드와 분리)
    /// 붙이는 곳: 아무 오브젝트 (보통 엔딩 화면 루트)
    /// </summary>
    [AddComponentMenu("Caste/Ending/Ending Debug Trigger")]
    public class EndingDebugTrigger : MonoBehaviour
    {
        [SerializeField] private EndingScreenController _ending;

#if ENABLE_INPUT_SYSTEM
        [Tooltip("엔딩을 띄울 키")]
        [SerializeField] private Key _key = Key.F10;
#else
        [Tooltip("엔딩을 띄울 키")]
        [SerializeField] private KeyCode _key = KeyCode.F10;
#endif

        [Tooltip("켜면 에디터·Development Build에서만 동작 (정식 빌드에서 실수로 엔딩이 뜨는 것 방지)")]
        [SerializeField] private bool _debugBuildOnly = true;

        [Tooltip("한 번 더 누르면 엔딩을 닫는다 (반복 테스트용)")]
        [SerializeField] private bool _toggle = true;

        private void Awake()
        {
            if (_debugBuildOnly && !Debug.isDebugBuild) enabled = false;
            if (_ending == null) _ending = GetComponent<EndingScreenController>();
        }

        private void Update()
        {
            if (_ending == null || !Pressed()) return;

            if (_ending.IsShowing) { if (_toggle) _ending.Hide(); }
            else _ending.Show();
        }

#if ENABLE_INPUT_SYSTEM
        private bool Pressed()
        {
            var kb = Keyboard.current;
            return kb != null && kb[_key].wasPressedThisFrame;
        }
#else
        private bool Pressed() => Input.GetKeyDown(_key);
#endif
    }
}
