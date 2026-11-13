// EventData.cs → CardData.cs
// ChoiceData 선언을 이 파일로 옮긴 뒤 기존 ChoiceData.cs를 삭제한다.

using System;

[Serializable]
public class CardData
{
    public string eventId;
    public string variantId;
    public string eventType;

    public string casteCode;
    public string casteName;
    public int casteOrder;

    public string requiredLegacyFlag;

    public string title;
    public string speaker;
    public string dialogue;

    public ChoiceData leftChoice;
    public ChoiceData rightChoice;
}
[Serializable]
public class ChoiceData
{
    public string text;
    public Stat delta;
    public string flagOnSelect;
}