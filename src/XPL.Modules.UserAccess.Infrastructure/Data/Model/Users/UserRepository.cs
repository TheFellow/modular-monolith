using Functional.Option;
using Microsoft.EntityFrameworkCore;
using System;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Framework.Infrastructure.UnitOfWork;
using XPL.Modules.UserAccess.Domain.Users;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.Users
{
    public class UserRepository : MappingRepository<UserAccessDbContext, User, SqlUser>
    {
        public UserRepository(UnitOfWorkBase<UserAccessDbContext> uow, Func<UserConverter> converterFactory)
            : base(uow, dbContext => dbContext.Users,
                  dbSet => dbSet.Include(u => u.Passwords).Include(u => u.Emails).Include(u => u.Roles))
        {
            Converter = converterFactory();
        }

        protected override IModelConverter<User, SqlUser> Converter { get; }

        public Option<User> TryFindByLogin(string login) => GetIdByLogin(login).Map(id => TryFind(id));

        public Option<long> GetIdByLogin(string login) => GetIdByExpression(u => u.Login == login);
    }
}
