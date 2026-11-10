using System;
using System.Collections.Generic;
using Caste.CardSwipe;
using Caste.Stats;
using UnityEngine;

namespace Caste.Cards
{
    /// <summary>
    /// 카드 한 장의 데이터. 대사·인물·선택지·스탯 변화를 코드 수정 없이 에셋으로 추가한다.
    /// 생성: Create → CASTE → Card Data
    /// </summary>
    [CreateAssetMenu(fileName = "Card_", menuName = "CASTE/Card Data")]
    public class CardData : ScriptableObject
    {
        [SerializeField] private string _characterName;
        [SerializeField] private Sprite _portrait;
        [SerializeField, TextArea(2, 5)] private string _question;

        [Header("왼쪽으로 넘기면")]
        [SerializeField] private ChoiceData _left = new();
        [Header("오른쪽으로 넘기면")]
        [SerializeField] private ChoiceData _right = new();

        public string CharacterName => _characterName;
        public Sprite Portrait => _portrait;
        public string Question => _question;

        public ChoiceData GetChoice(SwipeDirection dir) => dir == SwipeDirection.Left ? _left : _right;
    }

    [Serializable]
    public class ChoiceData
    {
        [SerializeField] private string _label;
        [SerializeField] private StatDelta[] _effects = Array.Empty<StatDelta>();

        public string Label => _label;
        public IReadOnlyList<StatDelta> Effects => _effects;

        /// <summary>이 선택지가 해당 스탯을 얼마나 바꾸는지 (같은 스탯이 여러 줄이면 합산)</summary>
        public int GetDelta(StatDefinition stat)
        {
            int sum = 0;
            foreach (var e in _effects)
                if (e.Stat == stat) sum += e.Amount;
            return sum;
        }
    }
}
