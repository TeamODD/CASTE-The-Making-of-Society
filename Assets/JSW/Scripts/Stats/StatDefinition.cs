using UnityEngine;

namespace Caste.Stats
{
    /// <summary>
    /// 스탯 하나의 정의 (신앙, 치안, 재정, 불만 ...). 새 스탯은 에셋만 추가하면 된다. (OCP)
    /// 생성: Create → CASTE → Stat Definition
    /// </summary>
    [CreateAssetMenu(fileName = "Stat_", menuName = "CASTE/Stat Definition")]
    public class StatDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName = "Stat";
        [Tooltip("게이지 아이콘. 지정하면 게이지가 이 그림 모양으로 아래에서 위로 차오른다")]
        [SerializeField] private Sprite _icon;
        [SerializeField] private Color _color = Color.white;

        [Header("값")]
        [SerializeField] private int _min = 0;
        [SerializeField] private int _max = 100;
        [SerializeField] private int _startValue = 50;

        [Tooltip("높을수록 좋은 스탯인가? (불만처럼 높을수록 나쁘면 끄기) → 미리보기 색 결정")]
        [SerializeField] private bool _higherIsBetter = true;

        [Header("백엔드 연결")]
        [Tooltip("백엔드(JMS) 스탯 중 어느 값과 연결할지. 신앙=Faith, 치안=Order, 재정=Economy, 불만=Unrest")]
        [SerializeField] private StatKey _key = StatKey.None;

        public string DisplayName => _displayName;
        public Sprite Icon => _icon;
        public Color Color => _color;
        public int Min => _min;
        public int Max => _max;
        public int StartValue => Clamp(_startValue);
        public bool HigherIsBetter => _higherIsBetter;
        public StatKey Key => _key;

        public int Clamp(int value) => Mathf.Clamp(value, _min, _max);

        /// <summary>0~1 비율 (게이지 fillAmount용)</summary>
        public float Normalize(int value) => Mathf.InverseLerp(_min, _max, value);

        /// <summary>이 변화량이 플레이어에게 좋은 일인가?</summary>
        public bool IsGood(int delta) => (delta > 0) == _higherIsBetter;
    }
}
