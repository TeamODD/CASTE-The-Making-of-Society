using System;

namespace Caste.Turn
{
    /// <summary>
    /// 1 → Max까지 올라가고, Max 다음에는 다시 1로 돌아가는 숫자. (순수 로직, Unity 의존 없음)
    /// 화면 표시와 분리해 두면 나중에 "12장마다 1년 경과" 같은 규칙을 붙이기 쉽다. (SRP)
    /// </summary>
    public class LoopingCounter
    {
        public int Current { get; private set; }
        public int Max { get; private set; }

        /// <summary>숫자가 바뀔 때 (새 값)</summary>
        public event Action<int> Changed;
        /// <summary>Max를 넘어 1로 돌아갈 때 (한 바퀴 완료)</summary>
        public event Action Looped;

        public LoopingCounter(int max, int start = 1)
        {
            Max = Math.Max(1, max);
            Current = Clamp(start);
        }

        public void Increment()
        {
            if (Current >= Max)
            {
                Current = 1;
                Looped?.Invoke();
            }
            else
            {
                Current++;
            }
            Changed?.Invoke(Current);
        }

        public void Reset(int start = 1)
        {
            Current = Clamp(start);
            Changed?.Invoke(Current);
        }

        private int Clamp(int value) => Math.Min(Math.Max(1, value), Max);
    }
}
