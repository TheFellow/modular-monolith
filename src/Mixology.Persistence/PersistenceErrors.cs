using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Mixology.Kernel.Errors;

namespace Mixology.Persistence;

public static class PersistenceErrors
{
    private const int ConstraintError = 19;
    private const int PrimaryKeyConstraint = 1555;
    private const int UniqueConstraint = 2067;

    public static Exception TranslateSave(Exception exception, string operation)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (AppError.Find(exception) is not null || AppError.IsCancellation(exception))
        {
            return exception;
        }

        if (exception is DbUpdateException { InnerException: SqliteException sqlite }
            && sqlite.SqliteErrorCode == ConstraintError
            && sqlite.SqliteExtendedErrorCode is PrimaryKeyConstraint or UniqueConstraint)
        {
            return AppError.Conflict($"{operation}: a unique value already exists", exception);
        }

        if (exception is DbUpdateConcurrencyException)
        {
            return AppError.Conflict($"{operation}: the record changed after it was read", exception);
        }

        return AppError.Internal(operation, exception);
    }
}
