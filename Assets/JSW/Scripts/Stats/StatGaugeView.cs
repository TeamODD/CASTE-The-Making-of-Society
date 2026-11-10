using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Caste.Stats
{
    /// <summary>
    /// 스탯 게이지 1개. 아래에서 위로 차오르는 세로 게이지 + 변화 예상치 표시.
    ///
    /// 이미지 2장을 겹쳐서 표현한다.
    ///   _previewFill (뒤) : 오를 구간(밝은색) / 줄어들 구간(빨간색)
    ///   _fill        (앞) : 현재 값
    /// 두 이미지가 각자 목표값을 SmoothDamp로 따라가서, 미리보기 → 확정 → 다음 카드까지 끊김 없이 이어진다.
    /// 표시만 담당하고 값 계산은 StatModel이 한다. (SRP)
    /// </summary>
    public class StatGaugeView : MonoBehaviour
    {
        [SerializeField] private StatDefinition _stat;

        [Header("이미지 (Image Type = Filled, Vertical, Bottom)")]
        [SerializeField] private Image _track;
        [SerializeField] private Image _previewFill;
        [SerializeField] private Image _fill;

        [Header("텍스트 (선택)")]
        [SerializeField] private TMP_Text _label;
        [SerializeField] private TMP_Text _deltaText;

        [Header("미리보기 색")]
        [SerializeField] private Color _goodColor = new(0.65f, 1f, 0.65f);
        [SerializeField] private Color _badColor = new(1f, 0.3f, 0.3f);

        [Header("부드러움 (작을수록 빠름)")]
        [Tooltip("카드를 기울일 때 미리보기가 따라가는 시간")]
        [SerializeField, Range(0.01f, 0.5f)] private float _previewSmoothTime = 0.08f;
        [Tooltip("선택 확정 후 게이지가 차오르거나 줄어드는 시간")]
        [SerializeField, Range(0.05f, 1.5f)] private float _settleSmoothTime = 0.45f;

        public StatDefinition Stat => _stat;

        private StatModel _model;
        private float _value01;          // 현재 실제 값 (0~1)
        private float _predicted01;      // 미리보기 예상 값 (0~1)
        private float _strength;         // 미리보기 강도 (카드 기울기)
        private bool _settling;          // 확정 후 차오르는 중

        private float _front, _frontVel; // 화면에 보이는 값 (부드럽게 따라감)
        private float _back, _backVel;
        private float _alpha, _alphaVel;
        private float _deltaAlpha, _deltaAlphaVel;

        public void Bind(StatModel model)
        {
            Unbind();
            _model = model;
            _model.Changed += OnStatChanged;

            _value01 = _predicted01 = _front = _back = _stat.Normalize(model.Get(_stat));
            ApplyStaticVisuals();
            Render();
        }

        /// <summary>미리보기 표시. strength 0~1 = 카드를 기울인 정도.</summary>
        public void ShowPreview(int delta, int predictedValue, float strength)
        {
            _predicted01 = _stat.Normalize(predictedValue);
            _strength = strength;

            Color c = _stat.IsGood(delta) ? _goodColor : _badColor;
            _previewFill.color = new Color(c.r, c.g, c.b, _previewFill.color.a);

            if (_deltaText != null)
            {
                _deltaText.text = delta > 0 ? $"+{delta}" : delta.ToString();
                _deltaText.color = new Color(c.r, c.g, c.b, _deltaText.color.a);
            }
        }

        public void ClearPreview() => _strength = 0f;

        private void OnStatChanged(StatDefinition stat, int value)
        {
            if (stat != _stat) return;
            _value01 = _stat.Normalize(value);
            _settling = true;
        }

        private void Update()
        {
            if (_model == null) return;

            bool previewing = _strength > 0.001f;
            float frontTarget = _value01, backTarget = _value01;
            if (previewing)
            {
                frontTarget = Mathf.Min(_value01, _predicted01); // 줄어들면 앞 게이지가 내려가고
                backTarget = Mathf.Max(_value01, _predicted01);  // 오르면 뒤 게이지가 올라간다
            }

            float dt = Time.unscaledDeltaTime;
            float smooth = _settling ? _settleSmoothTime : _previewSmoothTime;
            _front = Mathf.SmoothDamp(_front, frontTarget, ref _frontVel, smooth, Mathf.Infinity, dt);
            _back = Mathf.SmoothDamp(_back, backTarget, ref _backVel, smooth, Mathf.Infinity, dt);

            // 미리보기 중엔 기울기만큼, 확정 후엔 앞뒤 게이지가 만날 때까지 보인다
            float alphaTarget = previewing ? _strength : (_settling ? 1f : 0f);
            _alpha = Mathf.SmoothDamp(_alpha, alphaTarget, ref _alphaVel, _previewSmoothTime, Mathf.Infinity, dt);
            _deltaAlpha = Mathf.SmoothDamp(_deltaAlpha, previewing ? _strength : 0f, ref _deltaAlphaVel,
                _previewSmoothTime, Mathf.Infinity, dt);

            if (_settling && Mathf.Abs(_front - frontTarget) < 0.001f && Mathf.Abs(_back - backTarget) < 0.001f)
                _settling = false;

            Render();
        }

        private void Render()
        {
            _fill.fillAmount = _front;
            _previewFill.fillAmount = _back;
            SetAlpha(_previewFill, _alpha);
            if (_deltaText != null) SetAlpha(_deltaText, _deltaAlpha);
        }

        /// <summary>스탯 에셋의 아이콘·색·이름을 게이지에 반영 → 아트가 나오면 에셋만 바꾸면 된다.</summary>
        private void ApplyStaticVisuals()
        {
            if (_stat.Icon != null)
            {
                foreach (var img in new[] { _track, _previewFill, _fill })
                {
                    if (img == null) continue;
                    img.sprite = _stat.Icon;
                    img.preserveAspect = true;
                }
            }

            _fill.color = _stat.Color;
            if (_label != null) _label.text = _stat.DisplayName;
        }

        private static void SetAlpha(Graphic g, float a)
        {
            Color c = g.color;
            c.a = a;
            g.color = c;
        }

        private void Unbind()
        {
            if (_model != null) _model.Changed -= OnStatChanged;
            _model = null;
        }

        private void OnDestroy() => Unbind();
    }
}
