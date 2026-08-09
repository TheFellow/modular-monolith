using Mixology.Application.Authentication;
using Mixology.Kernel.Errors;
using Xunit;

namespace Mixology.Application.Tests;

public sealed class ActorTests
{
    [Theory]
    [InlineData(null, "owner")]
    [InlineData("", "owner")]
    [InlineData(" OWNER ", "owner")]
    [InlineData("anon", "anonymous")]
    [InlineData("manager", "manager")]
    [InlineData("SOMMELIER", "sommelier")]
    [InlineData("bartender", "bartender")]
    public void ParsePreservesTheClosedActorVocabulary(string? source, string expected)
    {
        Assert.Equal(expected, Actor.Parse(source).Id);
    }

    [Fact]
    public void UnknownActorIsTypedInvalidInput()
    {
        InvalidError error = Assert.Throws<InvalidError>(() => Actor.Parse("visitor"));

        Assert.Equal("unknown actor: \"visitor\"", error.Message);
    }
}
