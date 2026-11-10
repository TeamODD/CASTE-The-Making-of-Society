using System;

namespace Caste.CardSwipe
{
    /// <summary>
    /// "선택 입력"의 약속. 터치 스와이프, 키보드, 화면 버튼 등이 이걸 구현한다.
    /// 컨트롤러는 입력이 무엇으로 들어오는지 모르고 이 약속만 안다. (DIP, ISP)
    /// </summary>
    public interface IChoiceInput
    {
        /// <summary>기울기. 0 = 가운데, ±1 = 확정 기준선. 1을 넘을 수 있다.</summary>
        event Action<float> TiltChanged;

        /// <summary>선택 확정.</summary>
        event Action<SwipeDirection> ChoiceMade;

        /// <summary>기준선을 못 넘기고 놓음 → 카드 복귀.</summary>
        event Action Canceled;
    }
}
