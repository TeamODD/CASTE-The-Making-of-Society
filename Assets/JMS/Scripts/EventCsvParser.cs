
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using CsvHelper;

public static class EventCsvParser
{
    public static List<EventData> Parse(string csvText)
    {
        List<EventData> eventList = new();

        using var reader = new StringReader(csvText);
        using var csv = new CsvReader(
            reader, CultureInfo.InvariantCulture);

        csv.Read();
        csv.ReadHeader();

        while (csv.Read())
        {
            EventData eventData = new EventData
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

            eventList.Add(eventData);
        }

        return eventList;
    }

    private static int GetInt(CsvReader csv, string column)
    {
        string value = csv.GetField(column);

        if (string.IsNullOrWhiteSpace(value))
            return 0;

        return int.Parse(value, CultureInfo.InvariantCulture);
    }
}
