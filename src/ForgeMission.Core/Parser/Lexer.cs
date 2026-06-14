namespace ForgeMission.Core.Parser;

public class Lexer(string source)
{
    private int _pos = 0;
    private int _line = 1;
    private int _col = 1;

    public IReadOnlyList<Token> Tokenize()
    {
        var tokens = new List<Token>();

        while (_pos < source.Length)
        {
            SkipWhitespace();
            if (_pos >= source.Length) break;

            int startLine = _line, startCol = _col;

            if (source[_pos] == '|' && _pos + 1 < source.Length && source[_pos + 1] == '>')
            {
                tokens.Add(new Token(TokenType.Pipe, "|>", startLine, startCol));
                Advance(2);
                continue;
            }

            if (source[_pos] == '=')
            {
                tokens.Add(new Token(TokenType.Equals, "=", startLine, startCol));
                Advance(1);
                continue;
            }

            if (char.IsLetter(source[_pos]))
            {
                var word = ReadWord();
                var type = word switch
                {
                    "mission" => TokenType.Mission,
                    "expert"  => TokenType.Expert,
                    _ when char.IsUpper(word[0]) => TokenType.Identifier,
                    _ => TokenType.Unknown
                };
                tokens.Add(new Token(type, word, startLine, startCol));
                continue;
            }

            tokens.Add(new Token(TokenType.Unknown, source[_pos].ToString(), startLine, startCol));
            Advance(1);
        }

        tokens.Add(new Token(TokenType.EOF, "", _line, _col));
        return tokens;
    }

    private void SkipWhitespace()
    {
        while (_pos < source.Length && char.IsWhiteSpace(source[_pos]))
        {
            if (source[_pos] == '\n') { _line++; _col = 1; }
            else { _col++; }
            _pos++;
        }
    }

    private string ReadWord()
    {
        int start = _pos;
        while (_pos < source.Length && char.IsLetterOrDigit(source[_pos]))
        {
            _pos++;
            _col++;
        }
        return source[start.._pos];
    }

    private void Advance(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (_pos < source.Length && source[_pos] == '\n') { _line++; _col = 1; }
            else { _col++; }
            _pos++;
        }
    }
}
