using Functional.Option;
using System;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Framework.Infrastructure.UnitOfWork;
using XPL.Modules.UserAccess.Domain.Users;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Users.Converters;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.Users
{
    public class UserRepository : MappingRepository<UserAccessDbContext, User, SqlUser>
    {
        public UserRepository(UnitOfWorkBase<UserAccessDbContext> uow, Func<UserConverter> converterFactory)
            : base(uow, dbContext => dbContext.Users)
        {
            Converter = converterFactory();
        }

        protected override IModelConverter<User, SqlUser> Converter { get; }

        public Option<User> TryFindByLogin(string login)
        {
            var result = DbSet.FirstOrNone(u => u.Login == login);

            if (result is Some<SqlUser> sqlUser)
                return Converter.ToModel(sqlUser.Content);

            return None.Value;
        }
    }
}
