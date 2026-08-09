namespace Mixology.Filtering.Internal;

internal enum FilterTokenKind
{
    End,
    Identifier,
    String,
    Integer,
    Float,
    True,
    False,
    Null,
    LeftParenthesis,
    RightParenthesis,
    LeftBracket,
    RightBracket,
    Comma,
    Dot,
    Not,
    Equal,
    NotEqual,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
    And,
    Or,
    In,
    NotIn,
    Contains,
    StartsWith,
    EndsWith,
    Matches,
}

internal readonly record struct FilterToken(FilterTokenKind Kind, string Text, object? Value, int Position);

