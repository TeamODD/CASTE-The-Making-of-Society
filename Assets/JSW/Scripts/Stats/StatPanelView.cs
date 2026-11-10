using Caste.Cards;
using Caste.CardSwipe;
using UnityEngine;

namespace Caste.Stats
{
    /// <summary>
    /// 상단 스탯 패널. 카드 기울기를 받아 "그 방향 선택지를 고르면 어떻게 되는지"를 게이지들에 보여준다.
    /// IChoicePreview를 구현하므로 CardSwipeController는 이 클래스를 몰라도 된다. (DIP, OCP)
    /// </summary>
    public class StatPanelView : MonoBehaviour, IChoicePreview
    {
        [SerializeField] private StatGaugeView[] _gauges;
        [Tooltip("이 기울기에서 미리보기가 완전히 보임 (1 = 확정 기준선)")]
        [SerializeField, Range(0.1f, 1f)] private float _fullyVisibleAt = 0.6f;

        private StatModel _model;
        private CardData _card;

        public void Bind(StatModel model)
        {
            _model = model;
            foreach (var gauge in _gauges) gauge.Bind(model);
        }

        public void SetCard(CardData card)
        {
            _card = card;
            Hide();
        }

        public void SetTilt(float tilt)
        {
            if (_model == null || _card == null) return;
            if (Mathf.Approximately(tilt, 0f)) { Hide(); return; }

            ChoiceData choice = _card.GetChoice(tilt > 0f ? SwipeDirection.Right : SwipeDirection.Left);
            float strength = Mathf.Clamp01(Mathf.Abs(tilt) / _fullyVisibleAt);

            foreach (var gauge in _gauges)
            {
                int delta = choice.GetDelta(gauge.Stat);
                if (delta == 0) gauge.ClearPreview();
                else gauge.ShowPreview(delta, _model.Preview(gauge.Stat, delta), strength);
            }
        }

        public void Hide()
        {
            foreach (var gauge in _gauges) gauge.ClearPreview();
        }
    }
}
