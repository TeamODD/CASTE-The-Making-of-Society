using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Caste.CardSwipe
{
    /// <summary>
    /// 터치·마우스 드래그를 "기울기 / 확정 / 취소" 이벤트로 바꾸는 역할만 한다. (SRP)
    /// 카드가 어떻게 움직이는지는 모른다 → CardMotion 담당.
    /// 붙이는 곳: 카드 오브젝트 (Image 등 Raycast Target이 켜진 그래픽 필요)
    /// </summary>
    public class CardSwipeInput : MonoBehaviour, IChoiceInput,
        IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private const int NoPointer = int.MinValue;

        [SerializeField] private SwipeSettings _settings;
        [Tooltip("기준 영역. 비워두면 카드의 부모 RectTransform을 사용")]
        [SerializeField] private RectTransform _area;

        public event Action<float> TiltChanged;
        public event Action<SwipeDirection> ChoiceMade;
        public event Action Canceled;

        private int _pointerId = NoPointer;   // 첫 번째 손가락만 인식 (멀티터치 방지)
        private float _startX;
        private float _lastOffsetX;
        private float _velocityX;             // Canvas 좌표 기준 초당 이동량

        private void Awake()
        {
            if (_area == null) _area = transform.parent as RectTransform;

            // 연결 누락 시 매 프레임 NullReference 대신 원인을 한 번만 알리고 꺼진다 (꺼진 컴포넌트는 드래그 이벤트를 받지 않음)
            if (_settings == null || _area == null)
            {
                Debug.LogError($"[CardSwipe] {name}: SwipeSettings 또는 기준 영역(부모 RectTransform)이 없습니다.", this);
                enabled = false;
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_pointerId != NoPointer) return;
            if (!TryGetLocalX(eventData, out _startX)) return;

            _pointerId = eventData.pointerId;
            _lastOffsetX = 0f;
            _velocityX = 0f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            if (!TryGetLocalX(eventData, out float x)) return;

            float offsetX = x - _startX;
            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            // 순간 속도는 튀기 쉬워서 부드럽게 섞는다
            _velocityX = Mathf.Lerp(_velocityX, (offsetX - _lastOffsetX) / dt, 0.5f);
            _lastOffsetX = offsetX;

            TiltChanged?.Invoke(offsetX / _settings.GetThresholdDistance(_area));
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId) return;
            _pointerId = NoPointer;

            float tilt = _lastOffsetX / _settings.GetThresholdDistance(_area);
            float flickSpeed = _settings.FlickSpeedRatio * _area.rect.width;
            bool passedLine = Mathf.Abs(tilt) >= 1f;
            // 같은 방향으로 빠르게 튕겼을 때만 인정 (반대로 튕기며 놓는 경우 제외)
            bool flicked = Mathf.Abs(_velocityX) >= flickSpeed
                           && Mathf.Sign(_velocityX) == Mathf.Sign(_lastOffsetX);

            if (passedLine || flicked)
                ChoiceMade?.Invoke(_lastOffsetX > 0f ? SwipeDirection.Right : SwipeDirection.Left);
            else
                Canceled?.Invoke();
        }

        /// <summary>화면 좌표 → 기준 영역의 로컬 좌표. 해상도가 달라도 손맛이 같게 하기 위함.</summary>
        private bool TryGetLocalX(PointerEventData eventData, out float x)
        {
            bool ok = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _area, eventData.position, eventData.pressEventCamera, out Vector2 local);
            x = local.x;
            return ok;
        }
    }
}
