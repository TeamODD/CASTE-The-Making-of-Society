using System;
using System.Globalization;
using System.Text.RegularExpressions;

internal sealed class EndingCondition
{
    private static readonly Regex Comparison = new(
        @"\G\s*([a-zA-Z_]+)\s*(<=|>=|==|!=|<|>)\s*([+-]?[0-9]+)",
        RegexOptions.CultureInvariant);

    private readonly string text;
    private int position;
    private int statMask;

    /// <summary>
    /// 파싱할 원본 조건식을 보관합니다.
    /// </summary>
    private EndingCondition(string text)
    {
        this.text = text;
    }

    /// <summary>
    /// 조건을 스탯 판정 함수로 만들고 조건에 등장한 스탯의 우선순위를 반환합니다.
    /// </summary>
    public static (Func<Stat, bool> matches, int priority) Parse(string text)
    {
        var parser = new EndingCondition(text);
        Func<Stat, bool> matches = parser.ParseOr();
        parser.SkipWhitespace();
        if (parser.position != text.Length)
            throw new FormatException($"Unexpected token at column {parser.position + 1}.");

        // 첫 두 스탯을 신앙, 치안, 경제, 민심 순으로 비교합니다.
        int first = 4;
        int second = 4;
        for (int index = 0; index < 4; index++)
        {
            if ((parser.statMask & (1 << index)) == 0)
                continue;

            if (first == 4)
                first = index;
            else
            {
                second = index;
                break;
            }
        }

        return (matches, first * 5 + second);
    }

    /// <summary>
    /// 각 OR 항을 AND 단위로 읽어 AND가 먼저 결합되도록 합니다.
    /// </summary>
    private Func<Stat, bool> ParseOr()
    {
        Func<Stat, bool> result = ParseAnd();
        while (Consume("||"))
        {
            Func<Stat, bool> left = result;
            Func<Stat, bool> right = ParseAnd();
            result = stats => left(stats) || right(stats);
        }
        return result;
    }

    /// <summary>
    /// 비교식과 괄호식을 AND로 연결합니다.
    /// </summary>
    private Func<Stat, bool> ParseAnd()
    {
        Func<Stat, bool> result = ParseTerm();
        while (Consume("&&"))
        {
            Func<Stat, bool> left = result;
            Func<Stat, bool> right = ParseTerm();
            result = stats => left(stats) && right(stats);
        }
        return result;
    }

    /// <summary>
    /// 괄호로 묶은 식 또는 스탯과 정수의 비교를 읽습니다.
    /// </summary>
    private Func<Stat, bool> ParseTerm()
    {
        if (Consume("("))
        {
            Func<Stat, bool> matches = ParseOr();
            if (!Consume(")"))
                throw new FormatException($"Expected ')' at column {position + 1}.");
            return matches;
        }

        Match comparison = Comparison.Match(text, position);
        if (!comparison.Success || !int.TryParse(comparison.Groups[3].Value,
            NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int value))
        {
            throw new FormatException($"Expected a stat comparison at column {position + 1}.");
        }

        string key = comparison.Groups[1].Value;
        int index = key switch
        {
            "faith" => 0,
            "order" => 1,
            "economy" => 2,
            "public" => 3,
            _ => throw new FormatException($"Unknown stat '{key}'.")
        };
        statMask |= 1 << index;
        position += comparison.Length;

        return comparison.Groups[2].Value switch
        {
            "<=" => stats => ReadStat(stats, index) <= value,
            ">=" => stats => ReadStat(stats, index) >= value,
            "==" => stats => ReadStat(stats, index) == value,
            "!=" => stats => ReadStat(stats, index) != value,
            "<" => stats => ReadStat(stats, index) < value,
            ">" => stats => ReadStat(stats, index) > value,
            _ => throw new FormatException("Unknown comparison operator.")
        };
    }

    /// <summary>
    /// 공백 다음에 지정한 연산자나 괄호가 있으면 읽습니다.
    /// </summary>
    private bool Consume(string token)
    {
        SkipWhitespace();
        if (position + token.Length > text.Length
            || string.CompareOrdinal(text, position, token, 0, token.Length) != 0)
            return false;

        position += token.Length;
        return true;
    }

    /// <summary>
    /// 현재 위치부터 연속된 공백을 건너뜁니다.
    /// </summary>
    private void SkipWhitespace()
    {
        while (position < text.Length && char.IsWhiteSpace(text[position]))
            position++;
    }

    /// <summary>
    /// 조건식의 스탯 이름에 대응하는 값을 읽습니다.
    /// </summary>
    private static int ReadStat(Stat stats, int index) => index switch
    {
        0 => stats.faith,
        1 => stats.order,
        2 => stats.economy,
        3 => stats.publicOp,
        _ => throw new ArgumentOutOfRangeException(nameof(index))
    };
}
