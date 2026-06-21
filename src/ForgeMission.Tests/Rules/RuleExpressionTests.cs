using ForgeMission.Core.Rules;

namespace ForgeMission.Tests.Rules;

public class RuleExpressionTests
{
    // ── ParseClause ───────────────────────────────────────────────────────────

    [Fact]
    public void ParseClause_Nullary_ReturnsSingleToken()
    {
        var (evaluator, op, value) = RuleExpression.ParseClause("json_parseable");
        Assert.Equal("json_parseable", evaluator);
        Assert.Null(op);
        Assert.Null(value);
    }

    [Fact]
    public void ParseClause_NumericComparison_ReturnsThreeTokens()
    {
        var (evaluator, op, value) = RuleExpression.ParseClause("word_count < 200");
        Assert.Equal("word_count", evaluator);
        Assert.Equal("<",          op);
        Assert.Equal("200",        value);
    }

    [Fact]
    public void ParseClause_QuotedStringArg_StripsQuotes()
    {
        var (evaluator, op, value) = RuleExpression.ParseClause("""contains "hello world" """);
        Assert.Equal("contains", evaluator);
        Assert.Null(op);
        Assert.Equal("hello world", value);
    }

    [Fact]
    public void ParseClause_UnclosedQuote_Throws()
    {
        Assert.Throws<RuleEvaluationException>(() =>
            RuleExpression.ParseClause("""contains "unclosed"""));
    }

    // ── word_count ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("one two three", "<",  "4", true)]
    [InlineData("one two three", ">",  "2", true)]
    [InlineData("one two three", "==", "3", true)]
    [InlineData("one two three", "!=", "3", false)]
    [InlineData("one two three", "<=", "3", true)]
    [InlineData("one two three", ">=", "4", false)]
    public void WordCount_Comparison(string text, string op, string threshold, bool expected)
    {
        Assert.Equal(expected, RuleExpression.Evaluate($"word_count {op} {threshold}", text));
    }

    // ── char_count ────────────────────────────────────────────────────────────

    [Fact]
    public void CharCount_EqualsLength()
    {
        Assert.True(RuleExpression.Evaluate("char_count == 5", "hello"));
    }

    // ── line_count ────────────────────────────────────────────────────────────

    [Fact]
    public void LineCount_CountsNewlines()
    {
        Assert.True(RuleExpression.Evaluate("line_count >= 2", "line1\nline2"));
    }

    // ── contains / starts_with / ends_with ───────────────────────────────────

    [Fact]
    public void Contains_True_WhenSubstringPresent()
    {
        Assert.True(RuleExpression.Evaluate("""contains "world" """, "hello world"));
    }

    [Fact]
    public void Contains_False_WhenSubstringAbsent()
    {
        Assert.False(RuleExpression.Evaluate("""contains "missing" """, "hello world"));
    }

    [Fact]
    public void StartsWith_True_WhenPrefixMatches()
    {
        Assert.True(RuleExpression.Evaluate("""starts_with "hello" """, "hello world"));
    }

    [Fact]
    public void EndsWith_True_WhenSuffixMatches()
    {
        Assert.True(RuleExpression.Evaluate("""ends_with "world" """, "hello world"));
    }

    // ── no_match / contains_pattern ──────────────────────────────────────────

    [Fact]
    public void NoMatch_True_WhenPatternAbsent()
    {
        Assert.True(RuleExpression.Evaluate("""no_match "\d{4}" """, "no numbers here"));
    }

    [Fact]
    public void ContainsPattern_True_WhenPatternPresent()
    {
        Assert.True(RuleExpression.Evaluate("""contains_pattern "\d+" """, "answer is 42"));
    }

    // ── json_parseable / xml_parseable ────────────────────────────────────────

    [Fact]
    public void JsonParseable_True_ForValidJson()
    {
        Assert.True(RuleExpression.Evaluate("json_parseable", """{"key":"value"}"""));
    }

    [Fact]
    public void JsonParseable_False_ForInvalidJson()
    {
        Assert.False(RuleExpression.Evaluate("json_parseable", "not json at all"));
    }

    [Fact]
    public void XmlParseable_True_ForValidXml()
    {
        Assert.True(RuleExpression.Evaluate("xml_parseable", "<root><child/></root>"));
    }

    [Fact]
    public void XmlParseable_False_ForInvalidXml()
    {
        Assert.False(RuleExpression.Evaluate("xml_parseable", "not xml"));
    }

    // ── markdown_has_heading ──────────────────────────────────────────────────

    [Fact]
    public void MarkdownHasHeading_True_WhenHashPresent()
    {
        Assert.True(RuleExpression.Evaluate("markdown_has_heading", "# Title\nsome content"));
    }

    [Fact]
    public void MarkdownHasHeading_False_WhenNoHash()
    {
        Assert.False(RuleExpression.Evaluate("markdown_has_heading", "just plain text"));
    }

    // ── compound 'and' expression ─────────────────────────────────────────────

    [Fact]
    public void And_AllClausesMustPass()
    {
        Assert.True(RuleExpression.Evaluate(
            """word_count > 0 and contains "hello" """,
            "hello world"));

        Assert.False(RuleExpression.Evaluate(
            """word_count > 0 and contains "missing" """,
            "hello world"));
    }

    // ── deferred evaluators throw ─────────────────────────────────────────────

    [Fact]
    public void ReadingLevel_Throws_NotImplemented()
    {
        Assert.Throws<RuleEvaluationException>(
            () => RuleExpression.Evaluate("reading_level < 8", "text"));
    }

    [Fact]
    public void SchemaValid_Throws_NotImplemented()
    {
        Assert.Throws<RuleEvaluationException>(
            () => RuleExpression.Evaluate("schema_valid", "{}"));
    }

    // ── unknown evaluator ─────────────────────────────────────────────────────

    [Fact]
    public void UnknownEvaluator_Throws()
    {
        Assert.Throws<RuleEvaluationException>(
            () => RuleExpression.Evaluate("frobulate > 5", "text"));
    }

    // ── empty check throws ────────────────────────────────────────────────────

    [Fact]
    public void EmptyCheck_Throws()
    {
        Assert.Throws<RuleEvaluationException>(
            () => RuleExpression.Evaluate("", "text"));
    }
}
