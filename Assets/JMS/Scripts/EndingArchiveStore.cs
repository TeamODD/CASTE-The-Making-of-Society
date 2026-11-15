using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed class EndingArchiveStore
{
    private static readonly object WriteLock = new();
    private readonly string path;

    /// <summary>
    /// 별도 경로를 지정하지 않으면 기기의 archive.json을 사용합니다.
    /// </summary>
    public EndingArchiveStore(string path = null)
    {
        this.path = Path.GetFullPath(path ?? Path.Combine(Application.persistentDataPath, "archive.json"));
    }

    /// <summary>
    /// 해금된 엔딩 ID를 읽습니다. 파일이 없으면 빈 집합을 반환합니다.
    /// </summary>
    internal HashSet<string> LoadSeenIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var settings = new JsonSerializerSettings
            {
                DateParseHandling = DateParseHandling.None,
                CheckAdditionalContent = true
            };
            var document = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(path), settings);
            if (document?["seenEndingIds"] is not JArray entries)
                throw new InvalidDataException("The ending archive requires a seenEndingIds array.");

            foreach (JToken entry in entries)
            {
                if (entry.Type != JTokenType.String || string.IsNullOrWhiteSpace((string)entry))
                    throw new InvalidDataException("The ending archive contains an invalid ending ID.");
                ids.Add((string)entry);
            }
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The ending archive JSON is malformed.", exception);
        }

        return ids;
    }

    /// <summary>
    /// 아직 기록되지 않은 엔딩 ID를 기존 도감에 추가합니다.
    /// </summary>
    internal void RecordSeen(string endingId)
    {
        lock (WriteLock)
        {
            HashSet<string> ids = LoadSeenIds();
            if (ids.Add(endingId))
                SaveSystem.WriteJsonAtomically(path, new JObject { ["seenEndingIds"] = JArray.FromObject(ids) });
        }
    }
}
