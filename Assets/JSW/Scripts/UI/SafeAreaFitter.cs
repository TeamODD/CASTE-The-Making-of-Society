using UnityEngine;

namespace Caste.UI
{
    /// <summary>
    /// 이 RectTransform을 기기의 안전 영역(노치·펀치홀·제스처 바를 뺀 영역) 안으로 맞춘다.
    /// 전체 화면을 덮는 배경·팝업 딤은 이 오브젝트 밖에 두고, 눌러야 하거나 읽어야 하는 UI만 자식으로 넣는다.
    /// 화면 회전이나 해상도가 바뀌어도 자동으로 다시 맞춘다.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [ExecuteAlways]
    public class SafeAreaFitter : MonoBehaviour
    {
        [Header("안전 영역을 적용할 방향 (끄면 그쪽은 화면 끝까지 사용)")]
        [SerializeField] private bool _applyTop = true;
        [SerializeField] private bool _applyBottom = true;
        [SerializeField] private bool _applyLeft = true;
        [SerializeField] private bool _applyRight = true;

        private RectTransform _rect;
        private Rect _lastSafeArea;
        private Vector2Int _lastScreen;

        private void OnEnable()
        {
            _rect = (RectTransform)transform;
            Apply();
        }

        private void Update()
        {
            if (Screen.safeArea != _lastSafeArea || Screen.width != _lastScreen.x || Screen.height != _lastScreen.y)
                Apply();
        }

        private void OnValidate()
        {
            if (_rect != null) Apply();
        }

        private void Apply()
        {
            _lastSafeArea = Screen.safeArea;
            _lastScreen = new Vector2Int(Screen.width, Screen.height);
            if (Screen.width <= 0 || Screen.height <= 0) return;

            Rect safe = Screen.safeArea;
            Vector2 min = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            Vector2 max = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);

            if (!_applyLeft) min.x = 0f;
            if (!_applyBottom) min.y = 0f;
            if (!_applyRight) max.x = 1f;
            if (!_applyTop) max.y = 1f;

            _rect.anchorMin = min;
            _rect.anchorMax = max;
            _rect.offsetMin = Vector2.zero;
            _rect.offsetMax = Vector2.zero;
        }
    }
}
