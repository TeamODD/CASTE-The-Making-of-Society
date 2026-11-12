using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Caste.Tools
{
    /// <summary>
    /// 흩어진 설정을 한 화면에서 보고 고치기 위한 "모아보기" 오브젝트.
    /// 값을 직접 갖지 않고 다른 컴포넌트 / 에셋을 참조만 한다.
    /// → 각 컴포넌트의 책임은 그대로 두고 보는 창구만 하나로 모은다. (SRP 유지)
    /// 실제 화면은 Editor/ControlCenterEditor.cs가 그린다. 게임 실행에는 영향 없음.
    /// </summary>
    [AddComponentMenu("Caste/Tools/Control Center")]
    public class ControlCenter : MonoBehaviour
    {
        [Serializable]
        public class Section
        {
            public string title;
            public List<Object> targets = new List<Object>();
        }

        [Tooltip("분류별 대상 목록. 보통은 '자동으로 채우기' 버튼으로 만든다.")]
        public List<Section> sections = new List<Section>();
    }
}
