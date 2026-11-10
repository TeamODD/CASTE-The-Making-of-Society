using System;
using System.Collections;
using UnityEngine;

namespace Caste.CardSwipe
{
    /// <summary>
    /// 카드의 위치·회전·등장 연출만 담당한다. 입력이 무엇인지는 모른다. (SRP)
    /// 외부 트윈 라이브러리 없이 코루틴 + AnimationCurve로 구현 → 클론만 하면 바로 동작.
    /// 붙이는 곳: 카드 오브젝트
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CardMotion : MonoBehaviour
    {
        [SerializeField] private SwipeSettings _settings;
        [Tooltip("비워두면 부모 RectTransform 사용")]
        [SerializeField] private RectTransform _area;

        private RectTransform _rect;
        private Vector2 _homePosition;
        private Coroutine _running;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            if (_area == null) _area = _rect.parent as RectTransform;
            _homePosition = _rect.anchoredPosition;

            if (_settings == null || _area == null)
            {
                Debug.LogError($"[CardSwipe] {name}: SwipeSettings 또는 기준 영역(부모 RectTransform)이 없습니다.", this);
                enabled = false;
            }
        }

        /// <summary>드래그 중: 손가락을 따라가며 기울어진다.</summary>
        public void SetTilt(float tilt)
        {
            StopRunning();
            float x = tilt * _settings.GetThresholdDistance(_area);
            float angle = -Mathf.Clamp(tilt, -1f, 1f) * _settings.MaxRotation; // 오른쪽으로 끌면 시계 방향
            Apply(x, angle);
        }

        /// <summary>선택 확정: 화면 밖으로 날아간다.</summary>
        public void PlayCommit(SwipeDirection dir, Action onComplete)
        {
            float sign = (int)dir;
            float startX = _rect.anchoredPosition.x - _homePosition.x;
            float startAngle = _rect.localEulerAngles.z;
            if (startAngle > 180f) startAngle -= 360f;

            // 카드 너비만큼 더 밀어서 완전히 화면 밖으로
            float endX = sign * (_area.rect.width * 0.5f + _rect.rect.width);
            float endAngle = -sign * _settings.MaxRotation * 2f;

            Run(Animate(_settings.CommitDuration, _settings.CommitCurve, t =>
                Apply(Mathf.LerpUnclamped(startX, endX, t), Mathf.LerpUnclamped(startAngle, endAngle, t)),
                onComplete));
        }

        /// <summary>기준선 미달: 제자리로 돌아온다.</summary>
        public void PlayReturn(Action onComplete)
        {
            float startX = _rect.anchoredPosition.x - _homePosition.x;
            float startAngle = _rect.localEulerAngles.z;
            if (startAngle > 180f) startAngle -= 360f;

            Run(Animate(_settings.ReturnDuration, _settings.ReturnCurve, t =>
                Apply(Mathf.LerpUnclamped(startX, 0f, t), Mathf.LerpUnclamped(startAngle, 0f, t)),
                onComplete));
        }

        /// <summary>새 카드 등장: 가운데서 뒤집히듯 나타난다. (카드 오브젝트는 재사용)</summary>
        public void PlaySpawn(Action onComplete)
        {
            Apply(0f, 0f);
            Run(Animate(_settings.SpawnDuration, _settings.SpawnCurve, t =>
                    _rect.localScale = new Vector3(Mathf.LerpUnclamped(0f, 1f, t), 1f, 1f),
                () =>
                {
                    _rect.localScale = Vector3.one;
                    onComplete?.Invoke();
                }));
        }

        private void Apply(float offsetX, float angle)
        {
            _rect.anchoredPosition = _homePosition + new Vector2(offsetX, 0f);
            _rect.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void Run(IEnumerator routine)
        {
            StopRunning();
            _running = StartCoroutine(routine);
        }

        private void StopRunning()
        {
            if (_running == null) return;
            StopCoroutine(_running);
            _running = null;
        }

        private IEnumerator Animate(float duration, AnimationCurve curve, Action<float> step, Action onComplete)
        {
            for (float time = 0f; time < duration; time += Time.unscaledDeltaTime)
            {
                step(curve.Evaluate(time / duration));
                yield return null;
            }
            step(curve.Evaluate(1f));
            _running = null;
            onComplete?.Invoke();
        }
    }
}
