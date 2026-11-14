using System.Globalization;
using System.IO;
using CsvHelper;
using System.Collections.Generic;
using System.Linq;

public class CardDatabase
{
    private readonly List<CardData> cards;
    private readonly Dictionary<string, List<CardData>> cardsByEventId;

    /// <summary>
    /// CSV를 읽고 사건 ID별로 카드 변형을 조회할 수 있도록 묶습니다.
    /// </summary>
    public CardDatabase(string csvText)
    {
        cards = CardCsvParser.Parse(csvText);
        cardsByEventId = cards
            .GroupBy(card => card.eventId)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    /// <summary>
    /// CSV에 나온 순서대로 카드 목록의 복사본을 반환합니다.
    /// </summary>
    public List<CardData> GetAllCards()
    {
        return new List<CardData>(cards);
    }

    /// <summary>
    /// 사건 ID에 속한 카드 변형을 CSV 순서대로 반환합니다. 등록된 변형이 없으면 빈 목록을 반환합니다.
    /// </summary>
    public List<CardData> GetEventVariants(string eventId)
    {
        if (cardsByEventId.TryGetValue(eventId, out var variants))
        {
            return variants;
        }

        return new List<CardData>();
    }

    /// <summary>
    /// 변형 ID와 일치하는 첫 카드를 반환합니다. 일치하는 카드가 없으면 <see langword="null"/>를 반환합니다.
    /// </summary>
    public CardData FindByVariantId(string variantId)
    {
        return cards.FirstOrDefault(card => card.variantId == variantId);
    }
}

internal static class CardCsvParser
{
    /// <summary>
    /// CSV의 각 행을 카드와 좌우 선택지로 변환하고 행 순서를 유지합니다.
    /// </summary>
    public static List<CardData> Parse(string csvText)
    {
        List<CardData> cards = new();

        using var reader = new StringReader(csvText);
        using var csv = new CsvReader(
            reader, CultureInfo.InvariantCulture);

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            CardData card = new CardData
            {
                eventId = csv.GetField("event_id"),
                variantId = csv.GetField("variant_id"),
                eventType = csv.GetField("event_type"),

                casteCode = csv.GetField("caste_code"),
                casteName = csv.GetField("caste_name"),
                casteOrder = GetInt(csv, "caste_order"),

                requiredLegacyFlag =
                    csv.GetField("required_legacy_flag"),

                title = csv.GetField("title"),
                speaker = csv.GetField("speaker"),
                dialogue = csv.GetField("dialogue"),

                leftChoice = new ChoiceData
            {
                    text = csv.GetField("swipe_left_choice"),

                    flagOnSelect = csv.GetField("flag_on_left"),

                    delta = new Stat(
                        GetInt(csv, "left_faith_delta"),
                        GetInt(csv, "left_order_delta"),
                        GetInt(csv, "left_economy_delta"),
                        GetInt(csv, "left_unrest_delta")
                    )
                },

                rightChoice = new ChoiceData
                {
                    text = csv.GetField("swipe_right_choice"),

                    flagOnSelect = csv.GetField("flag_on_right"),

                    delta = new Stat(
                        GetInt(csv, "right_faith_delta"),
                        GetInt(csv, "right_order_delta"),
                        GetInt(csv, "right_economy_delta"),
                        GetInt(csv, "right_unrest_delta")
                    )
                }
            };

            cards.Add(card);
        }

        return cards;
    }

    /// <summary>
    /// 해당 CSV 열의 값을 정수로 읽습니다. 빈 값은 0으로 처리합니다.
    /// </summary>
    private static int GetInt(CsvReader csv, string column)
    {
        string value = csv.GetField(column);

        if (string.IsNullOrWhiteSpace(value))
            return 0;

        return int.Parse(value, CultureInfo.InvariantCulture);
    }
}
