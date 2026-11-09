using UnityEngine;

public class EventManager : MonoBehaviour
{
    private EventData currentEvent;

    public EventData GetCurrentEvent()
    {
        return currentEvent;
    }

    public void NextEvent()
    {
        // 추후 이벤트 선택 로직 구현
    }
}