namespace Caste.Integration
{
    // using을 namespace 안에 둔다: 전역 CardData/ChoiceData(JMS)보다 Caste.Cards 쪽을 먼저 찾게 하기 위함.
    using System.Collections.Generic;
    using Caste.Cards;
    using Caste.Stats;
    using UnityEngine;
    using BackendCard = global::CardData;
    using BackendChoice = global::ChoiceData;
    using BackendStat = global::Stat;

    /// <summary>
    /// 백엔드(JMS) 카드 → 화면용(JSW) 카드 변환만 담당하는 순수 C# 클래스. (SRP)
    /// 같은 카드 변형은 한 번만 만들어 재사용한다.
    /// </summary>
    public sealed class JmsCardConverter
    {
        private readonly IReadOnlyList<StatDefinition> _stats;
        private readonly IPortraitProvider _portraits;
        private readonly Dictionary<string, CardData> _cache = new();

        public JmsCardConverter(IReadOnlyList<StatDefinition> stats, IPortraitProvider portraits)
        {
            _stats = stats;
            _portraits = portraits;
        }

        public CardData Convert(BackendCard source)
        {
            if (source == null) return null;

            string key = string.IsNullOrEmpty(source.variantId) ? source.eventId : source.variantId;
            if (key != null && _cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var card = CardData.CreateRuntime(
                source.speaker,
                _portraits?.GetPortrait(source.speaker),
                source.dialogue,
                ConvertChoice(source.leftChoice),
                ConvertChoice(source.rightChoice));
            card.name = key;

            if (key != null) _cache[key] = card;
            return card;
        }

        /// <summary>백엔드 스탯 묶음에서 키에 해당하는 값을 꺼낸다.</summary>
        public static int Read(BackendStat stat, StatKey key) => key switch
        {
            StatKey.Faith => stat.faith,
            StatKey.Order => stat.order,
            StatKey.Economy => stat.economy,
            StatKey.Unrest => stat.unrest,
            _ => 0,
        };

        public void Clear()
        {
            foreach (var card in _cache.Values)
                if (card != null) Object.Destroy(card);
            _cache.Clear();
        }

        private ChoiceData ConvertChoice(BackendChoice choice)
        {
            if (choice == null) return new ChoiceData();

            var effects = new List<StatDelta>();
            foreach (var stat in _stats)
            {
                if (stat == null) continue;
                int amount = Read(choice.delta, stat.Key);
                if (amount != 0) effects.Add(new StatDelta(stat, amount));
            }
            return new ChoiceData(choice.text, effects.ToArray());
        }
    }
}
