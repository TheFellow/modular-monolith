using Mixology.Kernel.Errors;

namespace Mixology.Kernel.Paging;

public sealed record PageRequest(Cursor Cursor, int Limit = PageRequest.DefaultLimit)
{
    public const int DefaultLimit = 100;

    public void Validate()
    {
        if (Limit <= 0)
        {
            throw AppError.Invalid("page limit must be greater than zero");
        }
    }
}

public sealed record Page<T>(IReadOnlyList<T> Items, Cursor Next);

public static class Paging
{
    public static async ValueTask<IReadOnlyList<T>> CollectAsync<T>(
        Func<Cursor, CancellationToken, ValueTask<Page<T>>> list,
        CancellationToken cancellationToken = default)
    {
        List<T> items = [];
        Cursor cursor = default;

        do
        {
            Page<T> page = await list(cursor, cancellationToken).ConfigureAwait(false);
            items.AddRange(page.Items);
            cursor = page.Next;
        }
        while (!cursor.IsEmpty);

        return items;
    }

    public static async ValueTask<int> CountAsync<T>(
        Func<Cursor, CancellationToken, ValueTask<Page<T>>> list,
        CancellationToken cancellationToken = default)
    {
        int count = 0;
        Cursor cursor = default;

        do
        {
            Page<T> page = await list(cursor, cancellationToken).ConfigureAwait(false);
            count += page.Items.Count;
            cursor = page.Next;
        }
        while (!cursor.IsEmpty);

        return count;
    }
}
