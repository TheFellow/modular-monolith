using Functional.Option;
using System;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Modules.UserAccess.Domain.Registrations;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.Registrations
{
    public class RegistrationRepository : MappingRepository<UserAccessDbContext, Registration, SqlRegistration>
    {
        public RegistrationRepository(UserAccessUoW uow, Func<RegistrationConverter> converterFactory)
            : base(uow, dbContext => dbContext.Registrations)
        {
            Converter = converterFactory();
        }

        protected override IModelConverter<Registration, SqlRegistration> Converter { get; }

        public Option<Registration> TryFind(string login) => GetIdByGetIdByLogin(login)
            .Map(id => TryFind(id));

        public Option<long> GetIdByGetIdByLogin(string login) => GetIdByExpression(s => s.Login == login);
    }
}
