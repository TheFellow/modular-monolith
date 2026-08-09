using Mixology.Kernel.Errors;
using Mixology.Kernel.Tags;
using Xunit;

namespace Mixology.Kernel.Tests.Tags;

public sealed class TagTests
{
    [Theory]
    [InlineData("region=east", "region", "east", "region=east")]
    [InlineData("featured", "featured", "", "featured")]
    [InlineData("equation=a=b", "equation", "a=b", "equation=a=b")]
    [InlineData("  audience = members  ", "audience", "members", "audience=members")]
    public void ParseUsesFirstEqualsAndCanonicalizes(string source, string key, string value, string canonical)
    {
        Tag tag = Tag.Parse(source);

        Assert.Equal(key, tag.Key);
        Assert.Equal(value, tag.Value);
        Assert.Equal(canonical, tag.ToString());
    }

    [Theory]
    [InlineData("=missing")]
    [InlineData("bad=line\nbreak")]
    [InlineData("bad\nkey=value")]
    public void ParseRejectsInvalidTags(string source)
    {
        Assert.Throws<AppError>(() => Tag.Parse(source));
    }

    [Fact]
    public void CollectionsAreCaseSensitiveSortedAndImmutable()
    {
        TagCollection original = new([Tag.Create("region", "west"), Tag.Create("featured")]);
        TagCollection updated = original.Upsert(Tag.Create("region", "east")).Upsert(Tag.Create("Audience", "Members"));

        Assert.Equal(["Audience=Members", "featured", "region=east"], updated.Strings());
        Assert.Equal(["featured", "region=west"], original.Strings());
        Assert.Equal(["Audience=Members", "featured"], updated.Remove(" region ").Strings());
    }

    [Theory]
    [InlineData("region=east, env=dev, terraform=", "env=dev,region=east,terraform")]
    [InlineData("zulu,alpha=first,middle", "alpha=first,middle,zulu")]
    [InlineData("\"place=east, coast\",\"note=said \"\"hello\"\"\",equation=a=b", "equation=a=b,\"note=said \"\"hello\"\"\",\"place=east, coast\"")]
    [InlineData("", "")]
    public void CollectionCsvRoundTripsCanonically(string source, string canonical)
    {
        TagCollection tags = TagCollection.Parse(source);

        Assert.Equal(canonical, tags.Format());
        Assert.Equal(tags.Strings(), TagCollection.Parse(tags.Format()).Strings());
    }

    [Theory]
    [InlineData("\"unterminated")]
    [InlineData("one\ntwo")]
    [InlineData("region=west,region=east")]
    [InlineData("featured,")]
    public void CollectionRejectsInvalidInput(string source)
    {
        Assert.Throws<AppError>(() => TagCollection.Parse(source));
    }

    [Fact]
    public void LimitsCountUnicodeScalars()
    {
        Assert.Throws<AppError>(() => Tag.Create(new string('k', Tag.MaxKeyLength + 1)));
        Assert.Throws<AppError>(() => Tag.Create("key", new string('v', Tag.MaxValueLength + 1)));
        Assert.Equal(Tag.MaxKeyLength, Tag.Create(string.Concat(Enumerable.Repeat("😀", Tag.MaxKeyLength))).Key.EnumerateRunes().Count());
    }
}
