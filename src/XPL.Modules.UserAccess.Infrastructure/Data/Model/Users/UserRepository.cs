using XPL.Framework.Infrastructure.Persistence;
using XPL.Framework.Infrastructure.UnitOfWork;
using XPL.Modules.UserAccess.Domain.Users;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Users.Converters;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.Users
{
    public class UserRepository : MappingRepository<UserAccessDbContext, User, SqlUser>
    {
        public UserRepository(UnitOfWorkBase<UserAccessDbContext> uow)
            : base(uow, dbContext => dbContext.Users)
        {
        }

        protected override IModelConverter<User, SqlUser> Converter { get; } = new UserConverter();
    }
}
