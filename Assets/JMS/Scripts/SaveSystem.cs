using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

[Serializable]
public sealed class SaveData
{
    public const int CurrentSchemaVersion = 1;

    public int? schemaVersion;
    public List<CheckpointData> checkpoints = new();
    public ProgressData progress;

    public CheckpointData GetLastCheckpoint()
    {
        return checkpoints.Count == 0 ? null : checkpoints[checkpoints.Count - 1];
    }
}

[Serializable]
public sealed class CheckpointData
{
    public int casteOrder;
    public string casteCode;
    public Stat stats;
    public List<string> flags = new();
    public string firstVariantId;
}

[Serializable]
public sealed class ProgressData
{
    public int casteOrder;
    public int turnsUsedInCaste;
    public int turnsPerCaste;
    public Stat stats;
    public List<string> seenEventIds = new();
    public List<string> flags = new();
    public string currentVariantId;
    public bool isFinished;
}

public interface ISaveStore
{
    bool HasSave { get; }
    SaveData LoadOrNull();
    void Save(SaveData data);
}

public sealed class SaveSystem : ISaveStore
{
    public string SavePath { get; }
    public bool HasSave => File.Exists(SavePath);

    public SaveSystem(string savePath = null)
    {
        SavePath = Path.GetFullPath(savePath
            ?? Path.Combine(Application.persistentDataPath, "save.json"));
    }

    public void Save(SaveData data)
    {
        if (data == null)
            throw new InvalidDataException("The save must contain progress and a checkpoint.");

        JObject document = JObject.FromObject(data);
        ValidateDocument(document, legacy: false);
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
        string tempPath = SavePath + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            File.WriteAllText(tempPath, document.ToString(Formatting.Indented));
            if (File.Exists(SavePath))
                File.Replace(tempPath, SavePath, null);
            else
                File.Move(tempPath, SavePath);
        }
        catch
        {
            TryDeleteTemporaryFile(tempPath);
            throw;
        }
    }

    public SaveData LoadOrNull()
    {
        string json;
        try
        {
            json = File.ReadAllText(SavePath);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }

        try
        {
            var settings = new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.None,
                CheckAdditionalContent = true
            };
            JObject document = JsonConvert.DeserializeObject<JObject>(json, settings);
            if (document == null)
                throw new InvalidDataException("The save must be a JSON object.");
            bool legacy = document.Property("schemaVersion") == null;
            ValidateDocument(document, legacy);

            SaveData data = document.ToObject<SaveData>();
            if (legacy)
            {
                data.progress.currentVariantId = (string)(document["progress"]["currentCard"] as JObject)?["variantId"];
                data.progress.seenEventIds ??= new List<string>();
                data.progress.flags ??= new List<string>();
                foreach (CheckpointData checkpoint in data.checkpoints)
                    checkpoint.flags ??= new List<string>();
            }
            return data;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The save JSON is malformed or missing required data.", exception);
        }
        catch (OverflowException exception)
        {
            throw new InvalidDataException("A number in the save exceeds the supported range.", exception);
        }
    }

    private static void ValidateDocument(JObject document, bool legacy)
    {
        if (document["checkpoints"] is not JArray checkpoints || checkpoints.Count == 0
            || document["progress"] is not JObject progress)
        {
            throw new InvalidDataException("The save must contain progress and a checkpoint.");
        }

        ValidateStats(progress["stats"]);
        if (legacy)
        {
            RequireAbsent(progress, "casteOrder", "turnsUsedInCaste", "turnsPerCaste", "currentVariantId", "isFinished");
            JToken legacyCard = progress["currentCard"];
            if (legacyCard != null && legacyCard.Type != JTokenType.Null && legacyCard is not JObject)
                throw new InvalidDataException("The legacy current card must be an object or null.");

            if (legacyCard is JObject card && card["variantId"] is JToken variantId
                && variantId.Type != JTokenType.String && variantId.Type != JTokenType.Null)
            {
                throw new InvalidDataException("The legacy variant ID must be text.");
            }
        }
        else
        {
            if (ReadInt(document, "schemaVersion") != SaveData.CurrentSchemaVersion)
                throw new InvalidDataException("Unsupported save schema version.");

            int casteOrder = ReadInt(progress, "casteOrder");
            int turnsUsed = ReadInt(progress, "turnsUsedInCaste");
            int turnLimit = ReadInt(progress, "turnsPerCaste");
            if (casteOrder < 1 || turnsUsed < 0 || turnsUsed > turnLimit)
                throw new InvalidDataException("The saved caste order or turn count is invalid.");

            ValidateStrings(progress["seenEventIds"]);
            ValidateStrings(progress["flags"]);
            JToken finished = progress["isFinished"];
            JToken currentCard = progress["currentVariantId"];
            if (finished?.Type != JTokenType.Boolean || currentCard == null
                || ((bool)finished ? currentCard.Type != JTokenType.Null : !HasText(currentCard)))
            {
                throw new InvalidDataException("The current card does not match the saved completion state.");
            }
        }

        foreach (JToken item in checkpoints)
        {
            if (item is not JObject checkpoint || !HasText(checkpoint["casteCode"]))
                throw new InvalidDataException("The save contains an invalid checkpoint.");

            ValidateStats(checkpoint["stats"]);
            if (legacy)
            {
                RequireAbsent(checkpoint, "casteOrder", "firstVariantId");
            }
            else
            {
                if (ReadInt(checkpoint, "casteOrder") < 1 || !HasText(checkpoint["firstVariantId"]))
                    throw new InvalidDataException("The checkpoint is missing its caste or first card.");

                ValidateStrings(checkpoint["flags"]);
            }
        }
    }

    private static void ValidateStats(JToken stats)
    {
        if (stats is not JObject values)
            throw new InvalidDataException("The save is missing stats.");

        foreach (string field in new[] { "faith", "order", "economy", "unrest" })
        {
            int value = ReadInt(values, field);
            if (value < 0 || value > 100)
                throw new InvalidDataException("A saved stat is outside the 0-100 range.");
        }
    }

    private static int ReadInt(JObject value, string field)
    {
        JToken token = value[field];
        if (token?.Type != JTokenType.Integer)
            throw new InvalidDataException($"The save field '{field}' must be an integer.");
        return (int)token;
    }

    private static void ValidateStrings(JToken token)
    {
        if (token is not JArray list)
            throw new InvalidDataException("The save requires a list of strings.");

        foreach (JToken item in list)
        {
            if (item.Type != JTokenType.String)
                throw new InvalidDataException("A save list contains an invalid value.");
        }
    }

    private static bool HasText(JToken token)
    {
        return token?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)token);
    }

    private static void RequireAbsent(JObject value, params string[] fields)
    {
        foreach (string field in fields)
        {
            if (value.Property(field) != null)
                throw new InvalidDataException("The save has versioned fields without a version.");
        }
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
