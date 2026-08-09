using Microsoft.EntityFrameworkCore;

namespace Mixology.Persistence.Model;

public interface IModuleModelConfiguration
{
    void Configure(ModelBuilder modelBuilder);
}

