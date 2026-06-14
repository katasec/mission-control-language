namespace ForgeMission.Core.Parser;

public enum TokenType
{
    Mission,
    Expert,
    Pipe,
    Identifier,
    Equals,
    EOF,
    Unknown
}

public record Token(TokenType Type, string Value, int Line, int Column);
