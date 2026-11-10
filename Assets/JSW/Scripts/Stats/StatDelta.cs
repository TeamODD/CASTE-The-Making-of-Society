using System;
using UnityEngine;

namespace Caste.Stats
{
    /// <summary>"어떤 스탯이 얼마나 변하는가" 한 줄. 카드 선택지에서 사용.</summary>
    [Serializable]
    public struct StatDelta
    {
        [SerializeField] private StatDefinition _stat;
        [SerializeField] private int _amount;

        public StatDefinition Stat => _stat;
        public int Amount => _amount;
    }
}
