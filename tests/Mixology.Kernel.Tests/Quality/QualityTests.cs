using Mixology.Kernel.Errors;
using Xunit;
using QualityValue = Mixology.Kernel.Quality.Quality;

namespace Mixology.Kernel.Tests.Quality;

public sealed class QualityTests
{
    [Fact]
    public void ValuesHaveStableRanks()
    {
        Assert.Equal(3, QualityValue.Equivalent.Rank);
        Assert.Equal(2, QualityValue.Similar.Rank);
        Assert.Equal(1, QualityValue.Different.Rank);
    }

    [Fact]
    public void UnknownValueIsInvalid()
    {
        AppError error = Assert.Throws<AppError>(() => QualityValue.Parse("bad"));
        Assert.Equal(ErrorKind.Invalid, error.Kind);
    }
}
