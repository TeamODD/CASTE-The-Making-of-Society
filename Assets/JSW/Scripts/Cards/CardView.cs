using Caste.CardSwipe;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Caste.Cards
{
    /// <summary>카드 데이터를 화면(대사, 이름, 초상화, 선택지 문구)에 표시만 한다. (SRP)</summary>
    public class CardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _question;
        [SerializeField] private TMP_Text _characterName;
        [SerializeField] private Image _portrait;
        [SerializeField] private TMP_Text _leftLabel;
        [SerializeField] private TMP_Text _rightLabel;

        public void Show(CardData card)
        {
            if (card == null) return;

            SetText(_question, card.Question);
            SetText(_characterName, card.CharacterName);
            SetText(_leftLabel, card.GetChoice(SwipeDirection.Left).Label);
            SetText(_rightLabel, card.GetChoice(SwipeDirection.Right).Label);

            // 초상화가 아직 없으면 임시 사각형 유지 → 아트가 나오면 에셋에 넣기만 하면 된다
            if (_portrait != null && card.Portrait != null)
            {
                _portrait.sprite = card.Portrait;
                _portrait.color = Color.white;
                _portrait.preserveAspect = true;
            }
        }

        private static void SetText(TMP_Text target, string text)
        {
            if (target != null) target.text = text;
        }
    }
}
