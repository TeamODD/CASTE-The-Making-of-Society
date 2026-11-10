using UnityEngine;

namespace Caste.CardSwipe
{
    /// <summary>
    /// 기울기에 따라 왼쪽/오른쪽 선택지 텍스트를 서서히 보여준다. 표시만 담당. (SRP)
    /// CanvasGroup을 쓰기 때문에 텍스트, 아이콘 등 무엇을 넣어도 동작한다.
    /// (능력치 점 미리보기는 능력치 시스템 단계에서 추가 예정)
    /// </summary>
    public class ChoicePreviewView : MonoBehaviour, IChoicePreview
    {
        [SerializeField] private CanvasGroup _leftChoice;
        [SerializeField] private CanvasGroup _rightChoice;

        [Tooltip("이 기울기에서 완전히 보임 (1 = 확정 기준선)")]
        [SerializeField, Range(0.1f, 1f)] private float _fullyVisibleAt = 0.6f;

        private void Awake() => Hide();

        public void SetTilt(float tilt)
        {
            SetAlpha(_leftChoice, Mathf.Clamp01(-tilt / _fullyVisibleAt));
            SetAlpha(_rightChoice, Mathf.Clamp01(tilt / _fullyVisibleAt));
        }

        public void Hide()
        {
            SetAlpha(_leftChoice, 0f);
            SetAlpha(_rightChoice, 0f);
        }

        private static void SetAlpha(CanvasGroup group, float alpha)
        {
            if (group != null) group.alpha = alpha;
        }
    }
}
