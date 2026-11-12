using System.Globalization;
using System.IO;
using CsvHelper;
using System.Collections.Generic;
using System.Linq;

public class CardDatabase
{
    private readonly Dictionary<string, List<CardData>> cardsByEventId;

    public CardDatabase(string csvText)
    {
        cardsByEventId = CardCsvParser.Parse(csvText)
            .GroupBy(card => card.eventId)
            .ToDictionary(group => group.Key, group => group.ToList());
    }

    public List<CardData> GetAllCards()
    {
        return cardsByEventId.Values
            .SelectMany(variants => variants)
            .ToList();
    }

    public List<CardData> GetEventVariants(string eventId)
    {
        if (cardsByEventId.TryGetValue(eventId, out var variants))
        {
            return variants;
        }

        return new List<CardData>();
    }

    public CardData FindByVariantId(string variantId)
    {
        return cardsByEventId.Values
            .SelectMany(variants => variants)
            .FirstOrDefault(card => card.variantId == variantId);
    }
}

internal static class CardCsvParser
{
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

    private static int GetInt(CsvReader csv, string column)
    {
        string value = csv.GetField(column);

        if (string.IsNullOrWhiteSpace(value))
            return 0;

        return int.Parse(value, CultureInfo.InvariantCulture);
    }
}
