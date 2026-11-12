using UnityEngine;

namespace Caste.UI
{
    /// <summary>
    /// "나가기"의 실제 동작을 정하는 기반 클래스.
    /// 타이틀로 가기, 확인창 띄우기 등으로 바꾸려면 이걸 상속한 새 컴포넌트를 만들어
    /// 인스펙터에서 교체만 하면 된다. PauseMenuController는 고칠 필요 없음. (OCP, LSP)
    /// </summary>
    public abstract class ExitHandler : MonoBehaviour
    {
        public abstract void Exit();
    }
}
