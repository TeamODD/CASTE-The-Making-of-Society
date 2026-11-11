using Caste.CardSwipe;
using Caste.Effects;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Caste.Cards
{
    /// <summary>카드 데이터를 화면(대사, 이름, 초상화, 선택지 문구)에 표시만 한다. (SRP)</summary>
    public class CardView : MonoBehaviour
    {
        [SerializeField] private TMP_Text _question;
        [Tooltip("(선택) 대사를 타자기 효과로 출력. 비워두면 기존처럼 한 번에 표시한다.")]
        [SerializeField] private TypewriterEffect _questionTypewriter;
        [SerializeField] private TMP_Text _characterName;
        [SerializeField] private Image _portrait;
        [SerializeField] private TMP_Text _leftLabel;
        [SerializeField] private TMP_Text _rightLabel;

        public void Show(CardData card)
        {
            if (card == null) return;

            ShowQuestion(card.Question);
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

        // 타자기 컴포넌트가 연결돼 있으면 그쪽에 맡기고, 없으면 기존 방식 그대로 (OCP: 기존 동작을 깨지 않고 확장)
        private void ShowQuestion(string text)
        {
            if (_questionTypewriter != null) _questionTypewriter.Play(text);
            else SetText(_question, text);
        }

        private static void SetText(TMP_Text target, string text)
        {
            if (target != null) target.text = text;
        }
    }
}
