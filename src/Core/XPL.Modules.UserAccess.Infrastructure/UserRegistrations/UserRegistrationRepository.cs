﻿using Functional.Option;
using System;
using System.Linq;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Modules.UserAccess.Domain.UserRegistrations;
using XPL.Modules.UserAccess.Infrastructure.Data;
using XPL.Modules.UserAccess.Infrastructure.Data.Model;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Converters;

namespace XPL.Modules.UserAccess.Infrastructure.UserRegistrations
{
    public class UserRegistrationRepository : MappingRepository<UserAccessDbContext, UserRegistration, SqlUserRegistration>
    {
        public UserRegistrationRepository(UserAccessUoW uow, Func<UserRegistrationConverter> converterFactory)
            : base(uow, dbContext => dbContext.UserRegistrations)
        {
            Converter = converterFactory();
        }

        protected override IModelConverter<UserRegistration, SqlUserRegistration> Converter { get; }

        public Option<UserRegistration> TryFind(Guid registrationId) => DbSet
            .Where(s => s.RegistrationId == registrationId)
            .Select(s => s.Id)
            .FirstOrNone()
            .Map(id => TryFind(id));
    }
}
