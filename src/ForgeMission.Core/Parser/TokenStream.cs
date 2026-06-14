namespace ForgeMission.Core.Parser;

public class TokenStream(IReadOnlyList<Token> tokens)
{
    private int _pos = 0;

    public Token Peek() => _pos < tokens.Count ? tokens[_pos] : tokens[^1];

    public Token Consume()
    {
        var token = Peek();
        _pos++;
        return token;
    }

    public Token Expect(TokenType type)
    {
        var token = Peek();
        if (token.Type != type)
        {
            var message = (type == TokenType.Identifier && token.Type == TokenType.Unknown && token.Value.Length > 0 && char.IsLower(token.Value[0]))
                ? $"Expert and mission names must be PascalCase (start with an uppercase letter): '{token.Value}'"
                : $"Expected {type} but got '{token.Value}'";

            throw new ParseException(message, token.Line, token.Column);
        }
        return Consume();
    }
}
