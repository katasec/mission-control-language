namespace ForgeMission.Core.Parser;

public static class FmlParser
{
    public static Program Parse(string source)
    {
        var tokens = new Lexer(source).Tokenize();
        var stream = new TokenStream(tokens);
        return ParseProgram(stream);
    }

    private static Program ParseProgram(TokenStream stream)
    {
        var declarations = new List<Declaration>();
        while (stream.Peek().Type != TokenType.EOF)
            declarations.Add(ParseDeclaration(stream));
        return new Program(declarations);
    }

    private static Declaration ParseDeclaration(TokenStream stream)
    {
        var keyword = stream.Peek();

        if (keyword.Type == TokenType.Mission)
        {
            stream.Consume();
            var name = stream.Expect(TokenType.Identifier);
            stream.Expect(TokenType.Equals);
            var pipeline = ParsePipeline(stream);
            return new MissionDeclaration(name.Value, pipeline);
        }

        if (keyword.Type == TokenType.Expert)
        {
            stream.Consume();
            var name = stream.Expect(TokenType.Identifier);
            stream.Expect(TokenType.Equals);
            var pipeline = ParsePipeline(stream);
            return new ExpertDeclaration(name.Value, pipeline);
        }

        throw new ParseException(
            $"Expected 'mission' or 'expert' but got '{keyword.Value}'",
            keyword.Line, keyword.Column);
    }

    private static Pipeline ParsePipeline(TokenStream stream)
    {
        var steps = new List<string>();
        steps.Add(stream.Expect(TokenType.Identifier).Value);

        while (stream.Peek().Type == TokenType.Pipe)
        {
            stream.Consume();
            steps.Add(stream.Expect(TokenType.Identifier).Value);
        }

        return new Pipeline(steps);
    }
}
