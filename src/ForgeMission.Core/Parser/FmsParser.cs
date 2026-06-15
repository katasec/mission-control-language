using Antlr4.Runtime;

namespace ForgeMission.Core.Parser;

public record Diagnostic(string Message, int Line, int Column);

public record ParseResult(Program? Ast, IReadOnlyList<Diagnostic> Diagnostics)
{
    public bool Success => Ast is not null && Diagnostics.Count == 0;
}

public static class FmsParser
{
    /// <summary>
    /// Parse FMS source and return the AST, throwing <see cref="ParseException"/> on error.
    /// </summary>
    public static Program Parse(string source)
    {
        var result = TryParse(source);
        if (!result.Success)
        {
            var first = result.Diagnostics[0];
            throw new ParseException(first.Message, first.Line, first.Column);
        }
        return result.Ast!;
    }

    /// <summary>
    /// Parse FMS source and return all diagnostics alongside a best-effort AST.
    /// Use this path for LSP / tooling that needs to handle incomplete input.
    /// </summary>
    public static ParseResult TryParse(string source)
    {
        var diagnostics = new List<Diagnostic>();

        var inputStream  = CharStreams.fromString(source);
        var lexer        = new FmsGrammarLexer(inputStream);
        var tokenStream  = new CommonTokenStream(lexer);
        var parser       = new FmsGrammarParser(tokenStream);

        lexer.RemoveErrorListeners();
        parser.RemoveErrorListeners();

        var errorListener = new DiagnosticErrorListener(diagnostics);
        lexer.AddErrorListener(errorListener);
        parser.AddErrorListener(errorListener);

        var tree = parser.program();

        if (diagnostics.Count > 0)
            return new ParseResult(null, diagnostics);

        var ast = (Program)new FmsAstBuilder().Visit(tree)!;
        return new ParseResult(ast, []);
    }
}

file sealed class DiagnosticErrorListener(List<Diagnostic> diagnostics)
    : IAntlrErrorListener<int>, IAntlrErrorListener<IToken>
{
    // Called by the lexer (offending symbol is an int token type)
    void IAntlrErrorListener<int>.SyntaxError(
        System.IO.TextWriter output, IRecognizer recognizer,
        int offendingSymbol, int line, int col, string msg,
        Antlr4.Runtime.RecognitionException e)
    {
        diagnostics.Add(new Diagnostic(msg, line, col));
    }

    // Called by the parser (offending symbol is an IToken)
    void IAntlrErrorListener<IToken>.SyntaxError(
        System.IO.TextWriter output, IRecognizer recognizer,
        IToken offendingSymbol, int line, int col, string msg,
        Antlr4.Runtime.RecognitionException e)
    {
        var message = offendingSymbol?.Type == FmsGrammarLexer.LOWER_ID
            ? $"'{offendingSymbol.Text}' is not valid here — expert and mission names must be PascalCase"
            : msg;

        diagnostics.Add(new Diagnostic(message, line, col));
    }
}
