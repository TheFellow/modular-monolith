using Microsoft.EntityFrameworkCore;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Registrations;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Users;

namespace XPL.Modules.UserAccess.Infrastructure.Data
{
    public interface IUserAccessQueryContext
    {
        DbSet<SqlRegistration> UserRegistrations { get; set; }
        DbSet<SqlUser> Users { get; set; }
    }
}