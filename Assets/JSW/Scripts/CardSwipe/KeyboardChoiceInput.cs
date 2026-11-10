using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Caste.CardSwipe
{
    /// <summary>
    /// PC·웹용 키보드 선택 (← / →, A / D).
    /// IChoiceInput만 구현하면 되므로 카드 코드는 전혀 고치지 않았다. (OCP, DIP)
    /// </summary>
    public class KeyboardChoiceInput : MonoBehaviour, IChoiceInput
    {
        // 키보드는 기울기·취소가 없으므로 이벤트를 발생시키지 않는다.
        public event Action<float> TiltChanged { add { } remove { } }
        public event Action Canceled { add { } remove { } }
        public event Action<SwipeDirection> ChoiceMade;

        private void Update()
        {
            if (PressedLeft()) ChoiceMade?.Invoke(SwipeDirection.Left);
            else if (PressedRight()) ChoiceMade?.Invoke(SwipeDirection.Right);
        }

#if ENABLE_INPUT_SYSTEM
        private static bool PressedLeft()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame);
        }

        private static bool PressedRight()
        {
            var kb = Keyboard.current;
            return kb != null && (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame);
        }
#else
        private static bool PressedLeft() =>
            Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A);

        private static bool PressedRight() =>
            Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D);
#endif
    }
}
