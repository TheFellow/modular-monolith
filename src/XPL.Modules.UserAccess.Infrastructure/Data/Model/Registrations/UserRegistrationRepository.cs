using Functional.Option;
using System;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Modules.UserAccess.Domain.Registrations;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.Registrations
{
    public class UserRegistrationRepository : MappingRepository<UserAccessDbContext, UserRegistration, SqlUserRegistration>
    {
        public UserRegistrationRepository(UserAccessUoW uow, Func<UserRegistrationConverter> converterFactory)
            : base(uow, dbContext => dbContext.UserRegistrations)
        {
            Converter = converterFactory();
        }

        protected override IModelConverter<UserRegistration, SqlUserRegistration> Converter { get; }

        public Option<UserRegistration> TryFind(Guid registrationId) => GetIdByRegistrationId(registrationId)
            .Map(id => TryFind(id));

        public Option<long> GetIdByRegistrationId(Guid registrationId) => GetIdByExpression(s => s.RegistrationId == registrationId);
    }
}
