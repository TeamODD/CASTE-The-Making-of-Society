
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class EventDatabase : MonoBehaviour
{
    [SerializeField] private TextAsset csvFile;

    private List<EventData> eventList = new();

    private Dictionary<string, List<EventData>> eventDictionary = new();

    private void Awake()
    {
        // CSV 파싱
        eventList = EventCsvParser.Parse(csvFile.text);

        // 이벤트 ID를 키로 하는 Dictionary 생성
        eventDictionary = eventList
            .GroupBy(e => e.eventId)
            .ToDictionary(
                group => group.Key,
                group => group.ToList()
            );

        Debug.Log($"이벤트 {eventList.Count}개 로드 완료");
    }

    public List<EventData> GetAllEvents()
    {
        return eventList;
    }

    public List<EventData> GetEventVariants(string eventId)
    {
        if (eventDictionary.TryGetValue(eventId, out var variants))
            return variants;

        return new List<EventData>();
    }
}
