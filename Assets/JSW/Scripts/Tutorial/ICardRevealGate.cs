using System;

namespace Caste.Tutorial
{
    /// <summary>
    /// 카드를 화면에 내보내기 직전에 "잠깐 멈춰!"라고 끼어들 수 있는 관문.
    /// 카드 진행 쪽은 튜토리얼이 무엇인지 몰라도 이 인터페이스만 알면 된다. (DIP / ISP)
    /// </summary>
    public interface ICardRevealGate
    {
        /// <summary>게임을 열 때 호출. 새 게임이면 true, 이어하기면 false.</summary>
        void BeginRun(bool isNewGame);

        /// <summary>
        /// 카드를 보여주기 전에 호출. 끼어들 내용이 있으면 true를 돌려주고,
        /// 끝난 뒤 release를 호출해 카드 표시를 재개시킨다.
        /// </summary>
        /// <param name="choicesMade">이번 판에서 지금까지 고른 선택지 수 (0 = 첫 이벤트 전)</param>
        bool TryHold(int choicesMade, Action release);
    }
}
