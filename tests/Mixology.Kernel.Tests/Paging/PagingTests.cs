using Mixology.Kernel.Errors;
using Mixology.Kernel.Paging;
using Xunit;
using PagingOperations = Mixology.Kernel.Paging.Paging;

namespace Mixology.Kernel.Tests.Paging;

public sealed class PagingTests
{
    [Fact]
    public async Task CollectTraversesEveryPageInCursorOrder()
    {
        IReadOnlyList<int> items = await PagingOperations.CollectAsync<int>(List);

        Assert.Equal([1, 2, 3, 4, 5], items);
    }

    [Fact]
    public async Task CountTraversesEveryPage()
    {
        int count = await PagingOperations.CountAsync<int>(List);

        Assert.Equal(5, count);
    }

    [Fact]
    public void RequestRejectsNonPositiveLimit()
    {
        InvalidError error = Assert.Throws<InvalidError>(() => new PageRequest(default, 0).Validate());

        Assert.Equal(ErrorKind.Invalid, error.Kind);
    }

    private static ValueTask<Page<int>> List(Cursor cursor, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        int page = cursor.IsEmpty ? 0 : int.Parse(cursor.Value, System.Globalization.CultureInfo.InvariantCulture);

        return page switch
        {
            0 => ValueTask.FromResult(new Page<int>([1, 2], "1")),
            1 => ValueTask.FromResult(new Page<int>([3, 4], "2")),
            _ => ValueTask.FromResult(new Page<int>([5], default)),
        };
    }
}
