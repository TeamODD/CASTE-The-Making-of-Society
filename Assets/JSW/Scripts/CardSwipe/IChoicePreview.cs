namespace Caste.CardSwipe
{
    /// <summary>
    /// 카드를 기울이는 동안 "선택 결과를 미리 보여주는 것"의 약속.
    /// 선택지 글씨, 스탯 게이지 등 여러 개를 동시에 연결할 수 있다. (OCP, ISP)
    /// </summary>
    public interface IChoicePreview
    {
        /// <summary>기울기. 0 = 가운데, ±1 = 확정 기준선</summary>
        void SetTilt(float tilt);
        void Hide();
    }
}
