using UnityEngine;
using UnityEngine.UI;

namespace Caste.Ending
{
    /// <summary>
    /// 엔딩 화면의 일러스트 한 장을 보여준다. (표시만 담당, SRP)
    /// 일러스트가 있으면 글 영역을 아래쪽으로 내리고, 없으면 글이 화면 전체를 쓴다.
    /// 붙이는 곳: 엔딩 화면의 일러스트 Image 오브젝트
    /// </summary>
    [AddComponentMenu("Caste/Ending/Ending Illustration View")]
    [RequireComponent(typeof(Image))]
    public class EndingIllustrationView : MonoBehaviour
    {
        [Tooltip("(선택) 일러스트 유무에 따라 위치를 바꿀 글 영역")]
        [SerializeField] private RectTransform _textArea;

        [Header("글 영역 위치 (Anchor, 0~1)")]
        [Tooltip("일러스트가 있을 때 글 영역의 Anchor Min / Max")]
        [SerializeField] private Vector2 _textMinWithArt = new Vector2(0f, 0f);
        [SerializeField] private Vector2 _textMaxWithArt = new Vector2(1f, 0.38f);
        [Tooltip("일러스트가 없을 때 글 영역의 Anchor Min / Max")]
        [SerializeField] private Vector2 _textMinNoArt = new Vector2(0f, 0f);
        [SerializeField] private Vector2 _textMaxNoArt = new Vector2(1f, 1f);

        private Image _image;

        private Image Image => _image != null ? _image : (_image = GetComponent<Image>());

        /// <summary>일러스트 교체. null이면 숨기고 글 영역을 넓힌다.</summary>
        public void Show(Sprite sprite)
        {
            bool has = sprite != null;
            Image.sprite = sprite;
            Image.preserveAspect = true;
            Image.enabled = has;

            if (_textArea == null) return;
            _textArea.anchorMin = has ? _textMinWithArt : _textMinNoArt;
            _textArea.anchorMax = has ? _textMaxWithArt : _textMaxNoArt;
        }

        public void Clear() => Show(null);
    }
}
