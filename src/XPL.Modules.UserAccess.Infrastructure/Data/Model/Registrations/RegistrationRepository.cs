using Functional.Option;
using System;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Modules.UserAccess.Domain.Registrations;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.Registrations
{
    public class RegistrationRepository : MappingRepository<UserAccessDbContext, UserRegistration, SqlRegistration>
    {
        public RegistrationRepository(UserAccessUoW uow, Func<RegistrationConverter> converterFactory)
            : base(uow, dbContext => dbContext.UserRegistrations)
        {
            Converter = converterFactory();
        }

        protected override IModelConverter<UserRegistration, SqlRegistration> Converter { get; }

        public Option<UserRegistration> TryFind(Guid registrationId) => GetIdByRegistrationId(registrationId)
            .Map(id => TryFind(id));

        public Option<long> GetIdByRegistrationId(Guid registrationId) => GetIdByExpression(s => s.RegistrationId == registrationId);
    }
}
