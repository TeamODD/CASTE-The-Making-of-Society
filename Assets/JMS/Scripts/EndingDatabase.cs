using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;

public sealed class EndingData
{
    public string Id { get; }
    public string CasteName { get; }
    public string Text { get; }
    public bool IsCompletion { get; }
    internal string CasteCode { get; }
    internal int CasteOrder { get; }

    /// <summary>
    /// CSV에서 읽은 엔딩 표시 데이터를 보관합니다.
    /// </summary>
    internal EndingData(string id, string casteCode, string casteName, int casteOrder, string text, bool isCompletion)
    {
        Id = id;
        CasteCode = casteCode;
        CasteName = casteName;
        CasteOrder = casteOrder;
        Text = text;
        IsCompletion = isCompletion;
    }
}

public sealed class EndingDatabase
{
    private readonly Dictionary<string, EndingData> byId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (EndingData ending, Func<Stat, bool> matches)[]> byCaste;
    private readonly Dictionary<string, (EndingData ending, Func<Stat, bool> matches)[]> completionByCaste;
    private readonly EndingData[][] collectionByCaste;

    /// <summary>
    /// 플레이 씬에서 공통으로 사용하는 엔딩 CSV를 불러옵니다.
    /// </summary>
    internal static EndingDatabase LoadDefault()
    {
        var csv = UnityEngine.Resources.Load<UnityEngine.TextAsset>("GameOverEndings");
        if (csv == null)
            throw new InvalidOperationException("GameOverEndings CSV is missing.");
        return new EndingDatabase(csv.text);
    }

    /// <summary>
    /// 엔딩 CSV를 읽고 ID와 계층별 스탯 조건으로 조회할 수 있도록 등록합니다.
    /// </summary>
    public EndingDatabase(string csvText)
    {
        using var reader = new StringReader(csvText);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();

        var rules = new List<(EndingData ending, Func<Stat, bool> matches, bool isDouble100, int statPriority)>();
        while (csv.Read())
        {
            string id = csv.GetField("ending_id");
            string caste = csv.GetField("caste_code");
            string casteName = csv.GetField("caste_name");
            string text = csv.GetField("ending_text");
            string type = csv.GetField("ending_type");
            string condition = csv.GetField("condition");
            bool isCompletion = type == "completion";
            bool hasCondition = !string.IsNullOrWhiteSpace(condition);
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(caste)
                || string.IsNullOrWhiteSpace(casteName) || string.IsNullOrWhiteSpace(text)
                || !csv.TryGetField("caste_order", out int casteOrder) || casteOrder < 1
                || (!isCompletion && (!hasCondition || (type != "stat_zero" && type != "double_100"))))
            {
                throw new InvalidDataException($"Invalid ending CSV row {csv.Parser.Row}: '{id}'.");
            }

            var ending = new EndingData(id, caste, casteName, casteOrder, text, isCompletion);
            if (!byId.TryAdd(id, ending))
                throw new InvalidDataException($"Duplicate ending ID: '{id}'.");

            try
            {
                var (matches, statPriority) = hasCondition ? EndingCondition.Parse(condition) : (null, 0);
                rules.Add((ending, matches, type == "double_100", statPriority));
            }
            catch (FormatException exception)
            {
                throw new InvalidDataException(
                    $"Invalid condition for ending '{id}' on CSV row {csv.Parser.Row}: {exception.Message}", exception);
            }
        }

        var groups = rules.GroupBy(rule => rule.ending.CasteCode, StringComparer.Ordinal).ToArray();
        byCaste = groups.ToDictionary(
            group => group.Key,
            group => group.Where(rule => !rule.ending.IsCompletion)
                .OrderBy(rule => rule.isDouble100).ThenBy(rule => rule.statPriority)
                .Select(rule => (rule.ending, rule.matches)).ToArray(),
            StringComparer.Ordinal);
        completionByCaste = groups.ToDictionary(
            group => group.Key,
            group => group.Where(rule => rule.ending.IsCompletion)
                .OrderBy(rule => rule.matches == null)
                .Select(rule => (rule.ending, rule.matches)).ToArray(),
            StringComparer.Ordinal);
        collectionByCaste = groups.OrderBy(group => group.First().ending.CasteOrder)
            .Select(group => group.Select(rule => rule.ending).ToArray()).ToArray();
    }

    /// <summary>
    /// 계층 순서와 계층 내 CSV 순서를 유지해 도감에 표시할 항목을 반환합니다.
    /// </summary>
    internal IReadOnlyList<EndingCollectionGroup> GetCollection(ISet<string> seenIds)
    {
        var groups = new List<EndingCollectionGroup>(collectionByCaste.Length);
        foreach (EndingData[] endings in collectionByCaste)
        {
            var entries = new List<EndingCollectionEntry>(endings.Length);
            foreach (EndingData ending in endings)
                entries.Add(new EndingCollectionEntry(ending, seenIds.Contains(ending.Id)));

            groups.Add(new EndingCollectionGroup(endings[0], entries.AsReadOnly()));
        }

        return groups.AsReadOnly();
    }

    /// <summary>
    /// 저장된 ID로 엔딩을 복원합니다. CSV에서 누락된 경우 오류를 보고합니다.
    /// </summary>
    public EndingData GetById(string id)
    {
        return byId.TryGetValue(id, out var ending)
            ? ending
            : throw new InvalidDataException($"Unknown ending ID '{id}'.");
    }

    /// <summary>
    /// 현재 계층에서 우선순위가 가장 높은 일치 엔딩을 찾습니다.
    /// </summary>
    public EndingData Evaluate(string caste, Stat stats)
    {
        if (caste != null && byCaste.TryGetValue(caste, out var rules))
            foreach (var rule in rules)
                if (rule.matches(stats))
                    return rule.ending;

        return null;
    }

    /// <summary>
    /// 완주했을 때 조건이 맞는 엔딩을 CSV 순서로 찾고, 없으면 빈 조건의 기본 엔딩을 반환합니다.
    /// </summary>
    public EndingData EvaluateCompletion(string caste, Stat stats)
    {
        if (caste != null && completionByCaste.TryGetValue(caste, out var rules))
            foreach (var rule in rules)
                if (rule.matches == null || rule.matches(stats))
                    return rule.ending;

        return null;
    }
}
