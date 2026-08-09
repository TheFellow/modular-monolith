using System.Collections;
using System.Text;
using Mixology.Kernel.Errors;

namespace Mixology.Kernel.Tags;

public sealed class TagCollection : IReadOnlyList<Tag>
{
    private readonly Tag[] tags;

    public TagCollection(IEnumerable<Tag> tags)
    {
        this.tags = tags.OrderBy(tag => tag.Key, StringComparer.Ordinal).ToArray();
        Validate();
    }

    public static TagCollection Empty { get; } = new([]);

    public int Count => tags.Length;

    public Tag this[int index] => tags[index];

    public static TagCollection Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Empty;
        }

        return new TagCollection(ParseCsvRecord(value).Select(Tag.Parse));
    }

    public static TagCollection FromDictionary(IReadOnlyDictionary<string, string> values) =>
        new(values.Select(pair => Tag.Create(pair.Key, pair.Value)));

    public TagCollection Upsert(Tag next)
    {
        next.Validate();
        return new TagCollection(tags.Where(tag => tag.Key != next.Key).Append(next));
    }

    public TagCollection Remove(string key)
    {
        key = key.Trim();
        return new TagCollection(tags.Where(tag => tag.Key != key));
    }

    public IReadOnlyList<string> Strings() => tags.Select(tag => tag.ToString()).ToArray();

    public IReadOnlyDictionary<string, string> ToDictionary() =>
        tags.ToDictionary(tag => tag.Key, tag => tag.Value, StringComparer.Ordinal);

    public void Validate()
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (Tag tag in tags)
        {
            tag.Validate();
            if (!seen.Add(tag.Key))
            {
                throw AppError.Invalid($"duplicate tag key: {tag.Key}");
            }
        }
    }

    public string Format() => FormatCsvRecord(Strings());

    public override string ToString() => Format();

    public IEnumerator<Tag> GetEnumerator() => ((IEnumerable<Tag>)tags).GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => tags.GetEnumerator();

    private static List<string> ParseCsvRecord(string value)
    {
        List<string> fields = [];
        StringBuilder field = new();
        bool quoted = false;
        bool afterQuote = false;

        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (quoted)
            {
                if (current == '"')
                {
                    if (index + 1 < value.Length && value[index + 1] == '"')
                    {
                        field.Append('"');
                        index++;
                    }
                    else
                    {
                        quoted = false;
                        afterQuote = true;
                    }
                }
                else
                {
                    field.Append(current);
                }

                continue;
            }

            if (afterQuote)
            {
                if (current == ',')
                {
                    fields.Add(field.ToString());
                    field.Clear();
                    afterQuote = false;
                    continue;
                }

                throw AppError.Invalid("invalid tag collection: characters after closing quote");
            }

            if (field.Length == 0 && current is ' ' or '\t')
            {
                continue;
            }

            if (current == '"')
            {
                if (field.Length != 0)
                {
                    throw AppError.Invalid("invalid tag collection: quote in unquoted field");
                }

                quoted = true;
            }
            else if (current == ',')
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else if (current is '\r' or '\n')
            {
                throw AppError.Invalid("invalid tag collection: expected one CSV record");
            }
            else
            {
                field.Append(current);
            }
        }

        if (quoted)
        {
            throw AppError.Invalid("invalid tag collection: unterminated quoted field");
        }

        fields.Add(field.ToString());
        return fields;
    }

    private static string FormatCsvRecord(IEnumerable<string> values) =>
        string.Join(',', values.Select(QuoteCsvField));

    private static string QuoteCsvField(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
    }
}
