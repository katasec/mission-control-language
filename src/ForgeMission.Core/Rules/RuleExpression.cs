using System.Text.Json;
using System.Text.RegularExpressions;

namespace ForgeMission.Core.Rules;

/// <summary>
/// Parses and evaluates rule check expressions from kind:rule expert frontmatter.
/// Grammar: clause (' and ' clause)*
/// Clause: evaluator_name [op number | "string"]
/// </summary>
public static class RuleExpression
{
    public static bool Evaluate(string check, string text)
    {
        if (string.IsNullOrWhiteSpace(check))
            throw new RuleEvaluationException("Rule expert has an empty 'check' expression.");

        // Split on ' and ' (case-sensitive, as per spec).
        var clauses = check.Split(" and ", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return clauses.All(c => EvaluateClause(c, text));
    }

    private static bool EvaluateClause(string clause, string text)
    {
        var (evaluator, op, value) = ParseClause(clause);

        return evaluator switch
        {
            // ── numeric comparisons ───────────────────────────────────────────
            "word_count"     => CompareInt(CountWords(text),     op!, value!),
            "char_count"     => CompareInt(text.Length,          op!, value!),
            "line_count"     => CompareInt(CountLines(text),     op!, value!),
            "sentence_count" => CompareInt(CountSentences(text), op!, value!),

            // ── string argument checks ────────────────────────────────────────
            "contains"          => text.Contains(value!,     StringComparison.Ordinal),
            "starts_with"       => text.StartsWith(value!,   StringComparison.Ordinal),
            "ends_with"         => text.EndsWith(value!,     StringComparison.Ordinal),
            "no_match"          => !Regex.IsMatch(text, value!),
            "contains_pattern"  =>  Regex.IsMatch(text, value!),

            // ── nullary (no-argument) checks ──────────────────────────────────
            "json_parseable"       => IsJsonParseable(text),
            "xml_parseable"        => IsXmlParseable(text),
            "markdown_has_heading" => HasMarkdownHeading(text),

            // ── deferred ─────────────────────────────────────────────────────
            "reading_level" => throw new RuleEvaluationException(
                "'reading_level' is not yet implemented. Use word_count or char_count instead."),
            "schema_valid"  => throw new RuleEvaluationException(
                "'schema_valid' is not yet implemented. Validate JSON structure with json_parseable instead."),

            _ => throw new RuleEvaluationException($"Unknown rule evaluator: '{evaluator}'")
        };
    }

    // ── parser ────────────────────────────────────────────────────────────────

    public static (string evaluator, string? op, string? value) ParseClause(string clause)
    {
        clause = clause.Trim();
        var tokens = Tokenize(clause);

        return tokens.Count switch
        {
            1 => (tokens[0], null, null),                    // nullary: json_parseable
            2 => (tokens[0], null, tokens[1]),               // string arg: contains "text"
            3 => (tokens[0], tokens[1], tokens[2]),          // comparison: word_count < 200
            _ => throw new RuleEvaluationException($"Cannot parse rule clause: '{clause}'")
        };
    }

    // Splits a clause into tokens, keeping quoted strings as single tokens (quotes stripped).
    private static List<string> Tokenize(string clause)
    {
        var tokens = new List<string>();
        var i = 0;
        while (i < clause.Length)
        {
            if (clause[i] == ' ') { i++; continue; }

            if (clause[i] == '"')
            {
                var end = clause.IndexOf('"', i + 1);
                if (end < 0) throw new RuleEvaluationException($"Unclosed quote in rule clause: '{clause}'");
                tokens.Add(clause[(i + 1)..end]);
                i = end + 1;
            }
            else
            {
                var end = clause.IndexOf(' ', i);
                if (end < 0) end = clause.Length;
                tokens.Add(clause[i..end]);
                i = end;
            }
        }
        return tokens;
    }

    // ── numeric helpers ───────────────────────────────────────────────────────

    private static bool CompareInt(int actual, string op, string thresholdStr)
    {
        if (!int.TryParse(thresholdStr, out var threshold))
            throw new RuleEvaluationException($"Expected a number but got '{thresholdStr}'");

        return op switch
        {
            "<"  => actual <  threshold,
            ">"  => actual >  threshold,
            "<=" => actual <= threshold,
            ">=" => actual >= threshold,
            "==" => actual == threshold,
            "!=" => actual != threshold,
            _    => throw new RuleEvaluationException($"Unknown operator: '{op}'")
        };
    }

    private static int CountWords(string text)
        => text.Split((char[])[' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries).Length;

    private static int CountLines(string text)
        => text.Split('\n').Length;

    private static int CountSentences(string text)
    {
        // Heuristic: count sentence-ending punctuation followed by whitespace or end of string.
        var matches = Regex.Matches(text, @"[.!?](\s|$)");
        return matches.Count == 0 ? 1 : matches.Count; // at least one sentence
    }

    // ── structure evaluators ──────────────────────────────────────────────────

    private static bool IsJsonParseable(string text)
    {
        try
        {
            using var _ = JsonDocument.Parse(text.Trim());
            return true;
        }
        catch (JsonException) { return false; }
    }

    private static bool IsXmlParseable(string text)
    {
        try
        {
            _ = System.Xml.Linq.XDocument.Parse(text.Trim());
            return true;
        }
        catch (System.Xml.XmlException) { return false; }
    }

    private static bool HasMarkdownHeading(string text)
        => text.Split('\n').Any(line => line.TrimStart().StartsWith('#'));
}

public class RuleEvaluationException(string message) : Exception(message);
