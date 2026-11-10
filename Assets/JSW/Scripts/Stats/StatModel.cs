using System;
using System.Collections.Generic;
using UnityEngine;

namespace Caste.Stats
{
    /// <summary>
    /// 스탯의 현재 값을 보관하고 계산하는 순수 C# 클래스. (MonoBehaviour 아님)
    /// 화면을 전혀 모르기 때문에 Unity 실행 없이 테스트할 수 있다. (SRP)
    /// </summary>
    public class StatModel
    {
        private readonly Dictionary<StatDefinition, int> _values = new();

        /// <summary>값이 실제로 바뀌었을 때 (스탯, 새 값)</summary>
        public event Action<StatDefinition, int> Changed;

        public StatModel(IEnumerable<StatDefinition> stats)
        {
            foreach (var stat in stats)
                if (stat != null) _values[stat] = stat.StartValue;
        }

        public IEnumerable<StatDefinition> Stats => _values.Keys;

        public int Get(StatDefinition stat) => _values.TryGetValue(stat, out int v) ? v : 0;

        /// <summary>변화량을 적용하면 몇이 되는지 (실제로 바꾸지는 않음)</summary>
        public int Preview(StatDefinition stat, int delta) => stat.Clamp(Get(stat) + delta);

        public void Apply(IReadOnlyList<StatDelta> effects)
        {
            foreach (var effect in effects)
            {
                if (effect.Stat == null) continue;
                if (!_values.ContainsKey(effect.Stat))
                {
                    Debug.LogWarning($"[Stats] 등록되지 않은 스탯: {effect.Stat.name}");
                    continue;
                }

                int newValue = Preview(effect.Stat, effect.Amount);
                if (newValue == _values[effect.Stat]) continue;

                _values[effect.Stat] = newValue;
                Changed?.Invoke(effect.Stat, newValue);
            }
        }
    }
}
