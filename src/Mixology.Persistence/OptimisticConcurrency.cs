using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Mixology.Kernel.Errors;

namespace Mixology.Persistence;

public interface IRevisionedRow
{
    long Revision { get; set; }
}

public static class OptimisticConcurrency
{
    public static void UseOptimisticConcurrency<TRow>(this EntityTypeBuilder<TRow> entity)
        where TRow : class, IRevisionedRow =>
        entity.Property(row => row.Revision)
            .HasColumnName("revision")
            .HasDefaultValue(1L)
            .IsConcurrencyToken();

    public static void ExpectRevision<TRow>(this MixologyDbContext context, TRow row, long revision)
        where TRow : class, IRevisionedRow
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(row);
        if (revision <= 0)
        {
            throw AppError.Invalid("revision must be greater than zero");
        }

        context.Entry(row).Property(candidate => candidate.Revision).OriginalValue = revision;
    }

    public static void ExpectUpsertRevision<TRow>(
        this MixologyDbContext context,
        TRow? row,
        long revision,
        string subject)
        where TRow : class, IRevisionedRow
    {
        ArgumentNullException.ThrowIfNull(context);
        if (revision < 0)
        {
            throw AppError.Invalid("revision must be greater than or equal to zero");
        }

        if (row is null)
        {
            if (revision != 0)
            {
                throw AppError.Conflict($"{subject} changed after it was read");
            }

            return;
        }

        context.Entry(row).Property(candidate => candidate.Revision).OriginalValue = revision;
    }
}
