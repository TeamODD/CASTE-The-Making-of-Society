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

    /// <summary>
    /// 가장 최근 체크포인트를 반환합니다. 체크포인트가 없으면 <see langword="null"/>를 반환합니다.
    /// </summary>
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
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public string endingId;
}

public interface ISaveStore
{
    bool HasSave { get; }
    /// <summary>
    /// 저장 데이터를 불러옵니다. 저장이 없으면 <see langword="null"/>를 반환합니다.
    /// </summary>
    SaveData LoadOrNull();
    /// <summary>
    /// 전달받은 진행 데이터를 저장소에 기록합니다.
    /// </summary>
    void Save(SaveData data);
}

public sealed class SaveSystem : ISaveStore
{
    public string SavePath { get; }
    public bool HasSave => File.Exists(SavePath);

    /// <summary>
    /// 지정 경로 또는 Unity의 기본 영구 데이터 경로에서 사용할 저장 파일의 절대 경로를 정합니다.
    /// </summary>
    public SaveSystem(string savePath = null)
    {
        SavePath = Path.GetFullPath(savePath
            ?? Path.Combine(Application.persistentDataPath, "save.json"));
    }

    /// <summary>
    /// 진행 데이터의 유효성을 검증하고 임시 JSON 파일을 써서 기존 저장을 교체하거나 새 저장을 만듭니다.
    /// </summary>
    public void Save(SaveData data)
    {
        if (data == null)
            throw new InvalidDataException("The save must contain progress and a checkpoint.");

        JObject document = JObject.FromObject(data);
        ValidateDocument(document, legacy: false);
        WriteJsonAtomically(SavePath, document);
    }

    /// <summary>
    /// JSON을 같은 디렉터리의 임시 파일에 쓴 뒤 대상 파일을 교체합니다.
    /// </summary>
    internal static void WriteJsonAtomically(string path, JObject document)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";

        try
        {
            File.WriteAllText(tempPath, document.ToString(Formatting.Indented));
            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);
        }
        catch
        {
            TryDeleteTemporaryFile(tempPath);
            throw;
        }
    }

    /// <summary>
    /// 저장 JSON을 읽고 형식을 검증합니다. 파일이 없으면 <see langword="null"/>를 반환하고 구형 저장은 카드 변형 ID를 추출합니다.
    /// </summary>
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

    /// <summary>
    /// 저장 버전에 맞춰 진행과 체크포인트의 필수 항목, 스탯과 종료 상태가 유효한지 검사합니다.
    /// </summary>
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
            RequireAbsent(progress, "casteOrder", "turnsUsedInCaste", "turnsPerCaste", "currentVariantId", "isFinished", "endingId");
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

            JToken ending = progress["endingId"];
            if (ending != null && ending.Type != JTokenType.Null && (!(bool)finished || !HasText(ending)))
                throw new InvalidDataException("A saved ending ID requires finished progress.");
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

    /// <summary>
    /// 네 스탯이 모두 정수이며 0~100 범위에 있는지 검사합니다.
    /// </summary>
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

    /// <summary>
    /// 지정 JSON 필드를 정수로 읽고 필드의 타입이 정수가 아니면 예외를 발생시킵니다.
    /// </summary>
    private static int ReadInt(JObject value, string field)
    {
        JToken token = value[field];
        if (token?.Type != JTokenType.Integer)
            throw new InvalidDataException($"The save field '{field}' must be an integer.");
        return (int)token;
    }

    /// <summary>
    /// JSON 값이 배열이며 모든 항목이 문자열인지 검사합니다.
    /// </summary>
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

    /// <summary>
    /// JSON 값이 비어 있지 않은 문자열인지 확인합니다.
    /// </summary>
    private static bool HasText(JToken token)
    {
        return token?.Type == JTokenType.String && !string.IsNullOrWhiteSpace((string)token);
    }

    /// <summary>
    /// 버전이 없는 구형 저장에 지정한 새 형식 필드가 섞여 있으면 예외를 발생시킵니다.
    /// </summary>
    private static void RequireAbsent(JObject value, params string[] fields)
    {
        foreach (string field in fields)
        {
            if (value.Property(field) != null)
                throw new InvalidDataException("The save has versioned fields without a version.");
        }
    }

    /// <summary>
    /// 저장 실패 후 임시 파일 삭제를 시도합니다. 삭제 중 발생한 입출력 및 접근 오류는 무시합니다.
    /// </summary>
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
