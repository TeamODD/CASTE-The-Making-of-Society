using System;
using UnityEngine;

namespace Caste.CardSwipe
{
    /// <summary>
    /// 입력과 연출을 연결하고 카드 상태를 관리한다.
    /// Idle → Dragging → (Committing → Spawning) 또는 (Returning) → Idle
    /// 애니메이션 중에는 입력을 무시해서 "연타로 선택이 두 번 들어가는" 버그를 막는다.
    ///
    /// 다른 시스템(스탯, 날짜)은 이벤트만 구독하면 된다.
    /// → 이 클래스는 스탯을 몰라도 되고, 스탯 쪽도 스와이프를 몰라도 된다. (DIP)
    /// </summary>
    public class CardSwipeController : MonoBehaviour
    {
        private enum State { Idle, Dragging, Committing, Returning, Spawning }

        [Tooltip("IChoiceInput을 구현한 컴포넌트들 (CardSwipeInput, KeyboardChoiceInput 등)")]
        [SerializeField] private MonoBehaviour[] _inputSources;
        [SerializeField] private CardMotion _motion;
        [Tooltip("IChoicePreview를 구현한 컴포넌트들 (ChoicePreviewView, StatPanelView 등)")]
        [SerializeField] private MonoBehaviour[] _previewTargets;
        [SerializeField] private bool _logChoices = true;

        /// <summary>선택이 확정된 순간 (카드가 날아가기 시작할 때) 발생.</summary>
        public event Action<SwipeDirection> ChoiceCommitted;

        /// <summary>카드가 화면 밖으로 나간 직후 발생. 다음 카드 내용 교체용.</summary>
        public event Action<SwipeDirection> ChoiceSelected;

        private IChoiceInput[] _inputs = Array.Empty<IChoiceInput>();
        private IChoicePreview[] _previews = Array.Empty<IChoicePreview>();
        private State _state = State.Idle;

        private void Awake()
        {
            // Unity는 인터페이스를 인스펙터에 직렬화하지 못해서 MonoBehaviour로 받고 변환한다.
            _inputs = CastAll<IChoiceInput>(_inputSources);
            _previews = CastAll<IChoicePreview>(_previewTargets);
        }

        private T[] CastAll<T>(MonoBehaviour[] sources) where T : class
        {
            if (sources == null) return Array.Empty<T>();
            var result = new T[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                result[i] = sources[i] as T;
                if (result[i] == null)
                    Debug.LogError($"{sources[i]?.name} 은(는) {typeof(T).Name}이 아닙니다.", this);
            }
            return result;
        }

        private void OnEnable()
        {
            foreach (var input in _inputs)
            {
                if (input == null) continue;
                input.TiltChanged += HandleTilt;
                input.ChoiceMade += HandleChoice;
                input.Canceled += HandleCancel;
            }
        }

        private void OnDisable()
        {
            foreach (var input in _inputs)
            {
                if (input == null) continue;
                input.TiltChanged -= HandleTilt;
                input.ChoiceMade -= HandleChoice;
                input.Canceled -= HandleCancel;
            }
        }

        private void HandleTilt(float tilt)
        {
            if (_state != State.Idle && _state != State.Dragging) return;
            _state = State.Dragging;
            _motion.SetTilt(tilt);
            foreach (var p in _previews) p?.SetTilt(tilt);
        }

        private void HandleChoice(SwipeDirection dir)
        {
            if (_state != State.Idle && _state != State.Dragging) return;
            _state = State.Committing;
            HidePreviews();
            ChoiceCommitted?.Invoke(dir);

            _motion.PlayCommit(dir, () =>
            {
                if (_logChoices) Debug.Log($"[CardSwipe] {(dir == SwipeDirection.Left ? "왼쪽" : "오른쪽")} 선택");
                ChoiceSelected?.Invoke(dir);

                _state = State.Spawning;
                _motion.PlaySpawn(() => _state = State.Idle);
            });
        }

        private void HandleCancel()
        {
            if (_state != State.Dragging) return;
            _state = State.Returning;
            HidePreviews();
            _motion.PlayReturn(() => _state = State.Idle);
        }

        private void HidePreviews()
        {
            foreach (var p in _previews) p?.Hide();
        }
    }
}
