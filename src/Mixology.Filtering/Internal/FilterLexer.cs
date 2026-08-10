using System.Globalization;
using System.Text;
using Mixology.Kernel.Errors;

namespace Mixology.Filtering.Internal;

internal sealed class FilterLexer(string source)
{
    private int position;

    public FilterToken Next()
    {
        SkipWhitespace();
        if (position >= source.Length)
        {
            return new FilterToken(FilterTokenKind.End, string.Empty, null, position);
        }

        int start = position;
        char current = source[position++];
        return current switch
        {
            '(' => Token(FilterTokenKind.LeftParenthesis, start),
            ')' => Token(FilterTokenKind.RightParenthesis, start),
            '[' => Token(FilterTokenKind.LeftBracket, start),
            ']' => Token(FilterTokenKind.RightBracket, start),
            ',' => Token(FilterTokenKind.Comma, start),
            '.' when position >= source.Length || !char.IsDigit(source[position]) => Token(FilterTokenKind.Dot, start),
            '!' when Match('=') => Token(FilterTokenKind.NotEqual, start),
            '!' => Token(FilterTokenKind.Not, start),
            '=' when Match('=') => Token(FilterTokenKind.Equal, start),
            '<' when Match('=') => Token(FilterTokenKind.LessOrEqual, start),
            '<' => Token(FilterTokenKind.Less, start),
            '>' when Match('=') => Token(FilterTokenKind.GreaterOrEqual, start),
            '>' => Token(FilterTokenKind.Greater, start),
            '&' when Match('&') => Token(FilterTokenKind.And, start),
            '|' when Match('|') => Token(FilterTokenKind.Or, start),
            '"' => String(start),
            '-' or >= '0' and <= '9' => Number(start),
            _ when IsIdentifierStart(current) => Identifier(start),
            _ => throw Invalid($"unexpected character '{current}'", start),
        };
    }

    private FilterToken Identifier(int start)
    {
        while (position < source.Length && IsIdentifierPart(source[position]))
        {
            position++;
        }

        string text = source[start..position];
        return text switch
        {
            "true" => new(FilterTokenKind.True, text, true, start),
            "false" => new(FilterTokenKind.False, text, false, start),
            "nil" or "null" => new(FilterTokenKind.Null, text, null, start),
            "and" => new(FilterTokenKind.And, text, null, start),
            "or" => new(FilterTokenKind.Or, text, null, start),
            "not" => new(FilterTokenKind.Not, text, null, start),
            "in" => new(FilterTokenKind.In, text, null, start),
            "contains" => new(FilterTokenKind.Contains, text, null, start),
            "startsWith" => new(FilterTokenKind.StartsWith, text, null, start),
            "endsWith" => new(FilterTokenKind.EndsWith, text, null, start),
            "matches" => new(FilterTokenKind.Matches, text, null, start),
            _ => new(FilterTokenKind.Identifier, text, text, start),
        };
    }

    private FilterToken String(int start)
    {
        StringBuilder value = new();
        while (position < source.Length)
        {
            char current = source[position++];
            if (current == '"')
            {
                return new FilterToken(FilterTokenKind.String, source[start..position], value.ToString(), start);
            }

            if (current != '\\')
            {
                value.Append(current);
                continue;
            }

            if (position >= source.Length)
            {
                break;
            }

            value.Append(source[position++] switch
            {
                '"' => '"',
                '\\' => '\\',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                char escaped => escaped,
            });
        }

        throw Invalid("unterminated string", start);
    }

    private FilterToken Number(int start)
    {
        bool floating = source[start] == '.';
        while (position < source.Length && char.IsDigit(source[position]))
        {
            position++;
        }

        if (position < source.Length && source[position] == '.')
        {
            floating = true;
            position++;
            while (position < source.Length && char.IsDigit(source[position]))
            {
                position++;
            }
        }

        string text = source[start..position];
        if (floating && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double real))
        {
            return new FilterToken(FilterTokenKind.Float, text, real, start);
        }

        if (!floating && long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out long integer))
        {
            return new FilterToken(FilterTokenKind.Integer, text, integer, start);
        }

        throw Invalid($"invalid number {text}", start);
    }

    private FilterToken Token(FilterTokenKind kind, int start) =>
        new(kind, source[start..position], null, start);

    private bool Match(char expected)
    {
        if (position >= source.Length || source[position] != expected)
        {
            return false;
        }

        position++;
        return true;
    }

    private void SkipWhitespace()
    {
        while (position < source.Length && char.IsWhiteSpace(source[position]))
        {
            position++;
        }
    }

    private static bool IsIdentifierStart(char value) => char.IsLetter(value) || value == '_';
    private static bool IsIdentifierPart(char value) => char.IsLetterOrDigit(value) || value == '_';
    private static InvalidError Invalid(string message, int at) => AppError.Invalid($"invalid filter at {at}: {message}");
}
