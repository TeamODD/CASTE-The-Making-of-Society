using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

[Serializable]
public sealed class SaveData
{
    [JsonProperty(Required = Required.Always)]
    public List<CheckpointData> checkpoints = new();
    [JsonProperty(Required = Required.Always)]
    public ProgressData progress;

    public CheckpointData GetLastCheckpoint()
    {
        if (checkpoints == null || checkpoints.Count == 0)
        {
            return null;
        }

        return checkpoints[checkpoints.Count - 1];
    }

    public void AddCheckpoint(CheckpointData checkpoint)
    {
        checkpoints.Add(checkpoint);
    }

    public void RemoveCheckpoint(CheckpointData checkpoint)
    {
        checkpoints.Remove(checkpoint);
    }

    public void UpdateProgress(GameState state)
    {
        progress ??= new ProgressData();
        progress.stats = state.GetStats();
        progress.flags = state.CopyFlags();
        progress.seenEventIds = state.CopySeenEventIds();
        progress.currentCard = state.GetCurrentCard();
    }

    public void Clear()
    {
        checkpoints.Clear();
        progress = null;
    }
}

[Serializable]
public sealed class CheckpointData
{
    public string casteCode;
    [JsonProperty(Required = Required.Always)]
    public Stat stats;
    public List<string> flags = new();
}

[Serializable]
public sealed class ProgressData
{
    [JsonProperty(Required = Required.Always)]
    public Stat stats;
    public List<string> seenEventIds = new();
    public List<string> flags = new();
    public CardData currentCard=new();

    public void Clear()
    {
        stats = Stat.Default;
        seenEventIds.Clear();
        flags.Clear();
        currentCard = null;
    }
}

public interface ISaveStore
{
    SaveData LoadOrNull();
    void Save(SaveData data);
    void SaveProgress(SaveData data, GameState state);
}

public sealed class SaveSystem : ISaveStore
{
    public string SavePath { get; }

    public SaveSystem(string savePath = null)
    {
        SavePath = Path.GetFullPath(savePath
            ?? Path.Combine(Application.persistentDataPath, "save.json"));
    }

    public void Save(SaveData data)
    {
        if (data == null || data.checkpoints == null || data.progress == null)
        {
            throw new InvalidDataException("The save requires checkpoints and progress.");
        }

        string tempPath = SavePath + ".tmp";
        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
        File.WriteAllText(tempPath, json);

        if (File.Exists(SavePath))
        {
            File.Replace(tempPath, SavePath, null);
        }
        else
        {
            File.Move(tempPath, SavePath);
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

        SaveData data;
        try
        {
            data = JsonConvert.DeserializeObject<SaveData>(json);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The save JSON is malformed or missing required data.", exception);
        }
        if (data == null || data.checkpoints == null || data.progress == null)
        {
            throw new InvalidDataException("The save requires checkpoints and progress.");
        }

        data.progress.flags ??= new List<string>();
        data.progress.seenEventIds ??= new List<string>();
        data.progress.currentCard ??= new CardData();
        return data;
    }

    public void SaveProgress(SaveData data, GameState state)
    {
        data.UpdateProgress(state);
        Save(data);
    }
}
