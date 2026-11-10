using UnityEngine;

namespace Caste.CardSwipe
{
    /// <summary>
    /// 스와이프 "손맛" 수치 모음. 코드 수정 없이 에셋 값만 바꿔서 조절한다. (OCP)
    /// 생성: Project 창 우클릭 → Create → CASTE → Swipe Settings
    /// </summary>
    [CreateAssetMenu(fileName = "SwipeSettings", menuName = "CASTE/Swipe Settings")]
    public class SwipeSettings : ScriptableObject
    {
        [Header("판정")]
        [Tooltip("부모 영역 너비 대비, 이만큼 끌면 선택 확정 (0.25 = 25%)")]
        [SerializeField, Range(0.05f, 0.6f)] private float _commitThresholdRatio = 0.25f;

        [Tooltip("이 속도(초당 부모 너비 배수) 이상으로 튕기면 기준선을 못 넘겨도 확정")]
        [SerializeField, Min(0f)] private float _flickSpeedRatio = 1.5f;

        [Header("드래그 연출")]
        [Tooltip("기준선까지 끌었을 때의 회전 각도")]
        [SerializeField, Range(0f, 45f)] private float _maxRotation = 15f;

        [Header("애니메이션")]
        [SerializeField, Min(0.01f)] private float _commitDuration = 0.25f;
        [SerializeField] private AnimationCurve _commitCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [SerializeField, Min(0.01f)] private float _returnDuration = 0.2f;
        [SerializeField] private AnimationCurve _returnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [SerializeField, Min(0.01f)] private float _spawnDuration = 0.25f;
        [SerializeField] private AnimationCurve _spawnCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public float CommitThresholdRatio => _commitThresholdRatio;
        public float FlickSpeedRatio => _flickSpeedRatio;
        public float MaxRotation => _maxRotation;
        public float CommitDuration => _commitDuration;
        public AnimationCurve CommitCurve => _commitCurve;
        public float ReturnDuration => _returnDuration;
        public AnimationCurve ReturnCurve => _returnCurve;
        public float SpawnDuration => _spawnDuration;
        public AnimationCurve SpawnCurve => _spawnCurve;

        /// <summary>확정 기준 거리(Canvas 좌표). 해상도가 달라도 같은 비율로 동작한다.</summary>
        public float GetThresholdDistance(RectTransform area)
        {
            return area.rect.width * _commitThresholdRatio;
        }
    }
}
