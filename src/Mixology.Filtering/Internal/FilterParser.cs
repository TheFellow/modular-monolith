using System.Globalization;
using System.Text.RegularExpressions;
using Mixology.Kernel.Errors;

namespace Mixology.Filtering.Internal;

internal sealed class FilterParser(string source)
{
    private readonly FilterLexer lexer = new(source);
    private FilterToken current;

    public FilterNode Parse()
    {
        current = lexer.Next();
        FilterNode expression = ParseExpression(0);
        Require(FilterTokenKind.End);
        return expression;
    }

    private FilterNode ParseExpression(int minimumPrecedence)
    {
        FilterNode left = current.Kind is FilterTokenKind.Not
            ? ParseUnary()
            : ParsePrimary();

        while (TryBinaryOperator(out string? operation, out int precedence) && precedence >= minimumPrecedence)
        {
            Advance();
            if (operation == "not in")
            {
                Require(FilterTokenKind.In);
                Advance();
            }

            FilterNode right = ParseExpression(precedence + 1);
            left = new BinaryNode(operation, left, right);
        }

        return left;
    }

    private UnaryNode ParseUnary()
    {
        Advance();
        return new UnaryNode("!", ParseExpression(4));
    }

    private FilterNode ParsePrimary()
    {
        if (current.Kind == FilterTokenKind.LeftParenthesis)
        {
            Advance();
            FilterNode nested = ParseExpression(0);
            Require(FilterTokenKind.RightParenthesis);
            Advance();
            return nested;
        }

        if (current.Kind == FilterTokenKind.LeftBracket)
        {
            return ParseList();
        }

        if (current.Kind == FilterTokenKind.Identifier)
        {
            return ParseIdentifier();
        }

        if (current.Kind is FilterTokenKind.String or FilterTokenKind.Integer or FilterTokenKind.Float
            or FilterTokenKind.True or FilterTokenKind.False or FilterTokenKind.Null)
        {
            LiteralNode literal = new(current.Value);
            Advance();
            return literal;
        }

        throw Invalid($"expected expression, got {current.Text}");
    }

    private FilterNode ParseIdentifier()
    {
        string name = (string)current.Value!;
        Advance();
        if (current.Kind == FilterTokenKind.LeftParenthesis)
        {
            return ParseCall(name);
        }

        while (current.Kind == FilterTokenKind.Dot)
        {
            Advance();
            if (current.Kind is FilterTokenKind.Contains or FilterTokenKind.StartsWith
                or FilterTokenKind.EndsWith or FilterTokenKind.Matches)
            {
                string operation = Operator(current.Kind);
                Advance();
                Require(FilterTokenKind.LeftParenthesis);
                Advance();
                FilterNode argument = ParseExpression(0);
                Require(FilterTokenKind.RightParenthesis);
                Advance();
                return new BinaryNode(operation, new FieldNode(name), argument);
            }

            Require(FilterTokenKind.Identifier);
            name += $".{current.Value}";
            Advance();
        }

        return new FieldNode(name);
    }

    private CallNode ParseCall(string name)
    {
        if (name is not ("date" or "duration"))
        {
            throw Invalid($"function {name} is not supported");
        }

        Advance();
        Require(FilterTokenKind.String);
        LiteralNode argument = new(current.Value);
        string text = (string)current.Value!;
        Advance();
        Require(FilterTokenKind.RightParenthesis);
        Advance();

        object parsed = name == "date" ? ParseDate(text) : ParseDuration(text);
        return new CallNode(name, parsed, argument);
    }

    private ListNode ParseList()
    {
        List<FilterNode> items = [];
        Advance();
        while (current.Kind != FilterTokenKind.RightBracket)
        {
            items.Add(ParseExpression(0));
            if (current.Kind != FilterTokenKind.Comma)
            {
                break;
            }

            Advance();
        }

        Require(FilterTokenKind.RightBracket);
        Advance();
        return new ListNode(items);
    }

    private bool TryBinaryOperator(out string operation, out int precedence)
    {
        operation = Operator(current.Kind);
        precedence = current.Kind switch
        {
            FilterTokenKind.Or => 1,
            FilterTokenKind.And => 2,
            FilterTokenKind.Equal or FilterTokenKind.NotEqual or FilterTokenKind.Less
                or FilterTokenKind.LessOrEqual or FilterTokenKind.Greater or FilterTokenKind.GreaterOrEqual
                or FilterTokenKind.In or FilterTokenKind.Contains or FilterTokenKind.StartsWith
                or FilterTokenKind.EndsWith or FilterTokenKind.Matches => 3,
            FilterTokenKind.Not => 3,
            _ => -1,
        };

        if (current.Kind == FilterTokenKind.Not)
        {
            operation = "not in";
        }

        return precedence >= 0;
    }

    private static string Operator(FilterTokenKind kind) => kind switch
    {
        FilterTokenKind.Or => "||",
        FilterTokenKind.And => "&&",
        FilterTokenKind.Equal => "==",
        FilterTokenKind.NotEqual => "!=",
        FilterTokenKind.Less => "<",
        FilterTokenKind.LessOrEqual => "<=",
        FilterTokenKind.Greater => ">",
        FilterTokenKind.GreaterOrEqual => ">=",
        FilterTokenKind.In => "in",
        FilterTokenKind.Contains => "contains",
        FilterTokenKind.StartsWith => "startsWith",
        FilterTokenKind.EndsWith => "endsWith",
        FilterTokenKind.Matches => "matches",
        _ => string.Empty,
    };

    private static DateTimeOffset ParseDate(string text)
    {
        if (!DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset value))
        {
            throw AppError.Invalid($"invalid filter: invalid date literal \"{text}\"");
        }

        return value;
    }

    private static TimeSpan ParseDuration(string text)
    {
        MatchCollection matches = Regex.Matches(text, @"(?<number>\d+(?:\.\d+)?)(?<unit>ms|us|µs|ns|h|m|s)", RegexOptions.CultureInvariant);
        if (matches.Count == 0 || string.Concat(matches.Select(match => match.Value)) != text)
        {
            throw AppError.Invalid($"invalid filter: invalid duration literal \"{text}\"");
        }

        double ticks = 0;
        foreach (Match match in matches)
        {
            double number = double.Parse(match.Groups["number"].Value, CultureInfo.InvariantCulture);
            ticks += match.Groups["unit"].Value switch
            {
                "h" => TimeSpan.FromHours(number).Ticks,
                "m" => TimeSpan.FromMinutes(number).Ticks,
                "s" => TimeSpan.FromSeconds(number).Ticks,
                "ms" => TimeSpan.FromMilliseconds(number).Ticks,
                "us" or "µs" => number * 10d,
                "ns" => number / 100d,
                _ => 0d,
            };
        }

        return TimeSpan.FromTicks(checked((long)ticks));
    }

    private void Require(FilterTokenKind kind)
    {
        if (current.Kind != kind)
        {
            throw Invalid($"expected {kind}, got {current.Text}");
        }
    }

    private void Advance() => current = lexer.Next();
    private AppError Invalid(string message) => AppError.Invalid($"invalid filter at {current.Position}: {message}");
}
