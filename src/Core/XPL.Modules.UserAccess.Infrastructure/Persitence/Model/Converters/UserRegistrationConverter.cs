using System;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Modules.UserAccess.Domain.UserRegistrations;

namespace XPL.Modules.UserAccess.Infrastructure.Persitence.Model.Converters
{
    public class UserRegistrationConverter : IModelConverter<UserRegistration, SqlUserRegistration>
    {
        public UserRegistration ToModel(SqlUserRegistration persisted)
        {
            throw new NotImplementedException();
        }

        public SqlUserRegistration ToPersited(UserRegistration model)
        {
            throw new NotImplementedException();
        }

        public void CopyChanges(UserRegistration from, SqlUserRegistration to)
        {
            throw new NotImplementedException();
        }
    }
}
