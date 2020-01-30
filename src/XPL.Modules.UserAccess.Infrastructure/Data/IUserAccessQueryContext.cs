using Microsoft.EntityFrameworkCore;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.UserRegistrations;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Users;

namespace XPL.Modules.UserAccess.Infrastructure.Data
{
    public interface IUserAccessQueryContext
    {
        DbSet<SqlUserRegistration> UserRegistrations { get; set; }
        DbSet<SqlUser> Users { get; set; }
    }
}