using Functional.Option;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Framework.Infrastructure.UnitOfWork;
using XPL.Modules.UserAccess.Domain.Users;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.Users
{
    public class UserRepository : MappingRepository<UserAccessDbContext, User, SqlUser>
    {
        public UserRepository(UnitOfWorkBase<UserAccessDbContext> uow, Func<UserConverter> converterFactory)
            : base(uow, dbContext => dbContext.Users,
                  dbSet => dbSet.Include(u => u.Passwords).Include(u => u.Emails))
        {
            Converter = converterFactory();
        }

        protected override IModelConverter<User, SqlUser> Converter { get; }

        public Option<User> TryFindByLogin(string login) => DbSet
            .Where(u => u.Login == login)
            .Select(u => u.Id)
            .FirstOrNone()
            .Map(id => TryFind(id));
    }
}
