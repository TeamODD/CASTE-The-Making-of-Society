// 도감 일러스트 자동 로딩
// Assets/JMS/Resources/EndingArt에 CSV의 ending_id와 같은 이름으로 이미지를 넣습니다.
// 예시 B-ZERO-FAITH.png. Unity의 Texture Type을 Sprite (2D and UI), Sprite Mode를 Single로 설정합니다.
// 해금된 엔딩만 EndingArt.Load(ending.Id)로 불러옵니다. (엔딩 화면도 같은 함수를 씁니다)
// 경로에는 확장자를 쓰지 않습니다. 이미지가 없거나 엔딩이 잠겨 있으면 Illustration은 null입니다.

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 엔딩 ID → 일러스트 스프라이트. 이미지 위치 규칙을 한 곳에만 둡니다. (도감·엔딩 화면 공용)
/// </summary>
public static class EndingArt
{
    public const string ResourcesFolder = "EndingArt/";

    /// <summary>Resources/EndingArt/{id} 스프라이트를 불러옵니다. 없으면 null.</summary>
    public static Sprite Load(string endingId)
    {
        return string.IsNullOrEmpty(endingId) ? null : Resources.Load<Sprite>(ResourcesFolder + endingId);
    }
}

public sealed class EndingCollectionGroup
{
    public string CasteCode { get; }
    public string CasteName { get; }
    public int CasteOrder { get; }
    public IReadOnlyList<EndingCollectionEntry> Entries { get; }

    /// <summary>
    /// 계층과 CSV 순서대로 준비한 도감 항목을 보관합니다.
    /// </summary>
    internal EndingCollectionGroup(EndingData first, IReadOnlyList<EndingCollectionEntry> entries)
    {
        CasteCode = first.CasteCode;
        CasteName = first.CasteName;
        CasteOrder = first.CasteOrder;
        Entries = entries;
    }
}

public sealed class EndingCollectionEntry
{
    public bool IsUnlocked { get; }
    public string Id { get; }
    public string Text { get; }
    public Sprite Illustration { get; }

    /// <summary>
    /// 해금된 칸에 엔딩 ID와 본문을 제공하고 EndingArt 폴더에서 같은 이름의 스프라이트를 찾습니다.
    /// </summary>
    internal EndingCollectionEntry(EndingData ending, bool isUnlocked)
    {
        IsUnlocked = isUnlocked;
        if (isUnlocked)
        {
            Id = ending.Id;
            Text = ending.Text;
            Illustration = EndingArt.Load(ending.Id);
        }
    }
}
