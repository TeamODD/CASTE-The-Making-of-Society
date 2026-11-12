using System;
using TMPro;
using UnityEngine;

namespace Caste.UI
{
    /// <summary>
    /// 메뉴 UI의 "겉모습" 데이터만 모아둔 에셋. (SRP: 데이터와 적용 로직 분리)
    /// 아트 에셋이 오면 이 파일 하나만 고치면 연결된 모든 메뉴 / 설정창에 반영된다.
    /// 스프라이트를 비워두면 지금 모양(단색 사각형)이 유지된다.
    /// 만들기: Project 창 우클릭 → Create → Caste → UI → Menu Skin
    /// </summary>
    [CreateAssetMenu(fileName = "MenuSkin", menuName = "Caste/UI/Menu Skin")]
    public class MenuSkin : ScriptableObject
    {
        [Header("패널 (메뉴 배경판)")]
        public Sprite panelSprite;
        public Color panelColor = new Color32(0xEF, 0xDD, 0xB5, 0xFF);

        [Header("버튼 (계속하기 / 설정 / 나가기 / 닫기)")]
        [Tooltip("기본 상태 이미지")]
        public Sprite buttonSprite;
        [Tooltip("눌렀을 때 이미지. 넣으면 Sprite Swap 방식, 비우면 색 변화(Color Tint) 방식")]
        public Sprite buttonPressedSprite;
        [Tooltip("스프라이트를 넣었다면 흰색으로 두면 원본 색 그대로 나옴")]
        public Color buttonColor = new Color32(0x2A, 0x1E, 0x14, 0xFF);

        [Header("글자")]
        [Tooltip("비우면 현재 폰트 유지")]
        public TMP_FontAsset font;
        [Tooltip("버튼 위 글자 색")]
        public Color textColor = new Color32(0xEF, 0xDD, 0xB5, 0xFF);
        [Tooltip("패널 위 글자 색 (제목, '배경음' 같은 항목 이름)")]
        public Color labelColor = new Color32(0x2A, 0x1E, 0x14, 0xFF);

        [Header("설정(톱니바퀴) 버튼")]
        [Tooltip("아이콘 이미지. 넣으면 '설정' 글자는 자동으로 숨겨짐")]
        public Sprite settingsIconSprite;
        public Color settingsIconColor = new Color32(0x2A, 0x1E, 0x14, 0xFF);

        [Header("슬라이더 (배경음 / 효과음)")]
        public Sprite sliderTrackSprite;
        public Color sliderTrackColor = new Color32(0xBF, 0xA7, 0x7A, 0xFF);
        public Sprite sliderFillSprite;
        public Color sliderFillColor = new Color32(0x6B, 0x4F, 0x33, 0xFF);
        public Sprite sliderHandleSprite;
        public Color sliderHandleColor = new Color32(0x2A, 0x1E, 0x14, 0xFF);

        [Header("스위치 (타자기 효과)")]
        public Sprite switchBackgroundSprite;
        public Color switchOnColor = new Color32(0x2A, 0x1E, 0x14, 0xFF);
        public Color switchOffColor = new Color32(0xBF, 0xA7, 0x7A, 0xFF);
        public Sprite switchKnobSprite;
        public Color switchKnobColor = new Color32(0xF7, 0xEB, 0xD0, 0xFF);

        /// <summary>인스펙터에서 값이 바뀌면 적용기들에게 알림 (에디터 미리보기용)</summary>
        public static event Action<MenuSkin> Changed;

        private void OnValidate() => Changed?.Invoke(this);
    }
}
