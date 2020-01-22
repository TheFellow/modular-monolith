using System;
using System.Collections.Generic;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Modules.UserAccess.Domain.Users;
using static XPL.Modules.UserAccess.Domain.Users.User;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.Users.Converters
{
    public class UserConverter : IModelConverter<User, SqlUser>
    {
        public User ToModel(SqlUser p) => new Memento(
                p.UserId,
                p.RegistrationId,
                p.Login,
                p.FirstName,
                p.LastName)
            .From();

        public SqlUser ToPersisted(User model)
        {
            var m = Memento.Get(model);
            return new SqlUser
            {
                UserId = m.UserId,
                RegistrationId = (m.RegistrationId ?? Guid.Empty),
                FirstName = m.FirstName,
                LastName = m.LastName,
                Login = m.Login,
                Logins = new List<SqlUserLogin>(),
                Emails = new List<SqlUserEmail>(),    // TODO: Deal with the schema
            };
        }

        public void CopyChanges(User from, SqlUser to)
        {
            var m = Memento.Get(from);
            to.UserId = m.UserId;
            to.RegistrationId = m.RegistrationId;
            to.FirstName = m.FirstName;
            to.LastName = m.LastName;
            to.Login = m.Login;
            // TODO: Deal with the schema
        }
    }
}
