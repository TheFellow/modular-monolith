using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Mixology.Persistence.Model;

namespace Mixology.Persistence;

public sealed class MixologyDesignTimeDbContextFactory : IDesignTimeDbContextFactory<MixologyDbContext>
{
    public MixologyDbContext CreateDbContext(string[] args)
    {
        DbContextOptionsBuilder<MixologyDbContext> options = new();
        options.UseSqlite("Data Source=mixology-design.db");
        return new MixologyDbContext(options.Options, [new StoreModelConfiguration()]);
    }
}

