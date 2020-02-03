using System;
using System.Collections.Generic;
using System.Linq;
using XPL.Framework.Domain;
using XPL.Framework.Infrastructure.Data;
using XPL.Framework.Infrastructure.Persistence;
using XPL.Modules.UserAccess.Domain.Users;
using static XPL.Modules.UserAccess.Domain.Users.User;

namespace XPL.Modules.UserAccess.Infrastructure.Data.Model.Users
{
    public class UserConverter : IModelConverter<User, SqlUser>
    {
        private readonly IExecutionContext _executionContext;
        private readonly IEmailUsage _emailUsage;

        public UserConverter(IExecutionContext executionContext, IEmailUsage emailUsage)
        {
            _executionContext = executionContext;
            _emailUsage = emailUsage;
        }

        public User ToModel(SqlUser p)
        {
            var currentLogin = p.Passwords.Single(l => l.EndOnUtc == null);

            return new Memento(
                p.UserId,
                p.RegistrationId,
                p.Login,
                p.Emails.Single(e => e.Status == SqlUserEmail.ActiveStatus).Email,
                p.FirstName,
                p.LastName,
                currentLogin.PasswordHash,
                currentLogin.PasswordSalt)
                .From(_emailUsage);
        }

        public SqlUser ToPersisted(User model)
        {
            var m = Memento.Get(model);

            var email = new SqlUserEmail
            {
                Email = m.CurrentEmail,
                Status = SqlUserEmail.ActiveStatus,
                StatusDate = _executionContext.SystemClock.Now.Date,
                UpdatedBy = _executionContext.UserInfo.UserFullName,
                UpdatedOn = _executionContext.SystemClock.Now,
            };

            var login = new SqlUserPassword
            {
                BeginOnUtc = _executionContext.SystemClock.UtcNow,
                EndOnUtc = null,
                PasswordHash = m.PasswordHash,
                PasswordSalt = m.PasswordSalt,
                UpdatedBy = _executionContext.UserInfo.UserFullName,
                UpdatedOn = _executionContext.SystemClock.Now,
            };

            return new SqlUser
            {
                UserId = m.UserId,
                RegistrationId = m.RegistrationId ?? Guid.Empty,
                FirstName = m.FirstName,
                LastName = m.LastName,
                Login = m.CurrentLogin,
                UpdatedBy = _executionContext.UserInfo.UserFullName,
                UpdatedOn = _executionContext.SystemClock.Now,
                Passwords = new List<SqlUserPassword>(new[] { login }),
                Emails = new List<SqlUserEmail>(new[] { email }),
            };
        }

        public void CopyChanges(User from, SqlUser sql)
        {
            var utcNow = _executionContext.SystemClock.UtcNow;

            var user = Memento.Get(from);

            var audit = new AuditFieldUpdater<Memento, SqlUser>(_executionContext, user, sql, u => u.UpdatedBy, u => u.UpdatedOn);
            audit.Map(m => m.FirstName, u => u.FirstName)
                .Map(m => m.LastName, u => u.LastName)
                .Map(m => m.CurrentLogin, u => u.Login)
                .Map(m => m.RegistrationId, u => u.RegistrationId)
                .Map(m => m.UserId, u => u.UserId)
                .Audit();

            if (user.PasswordHash != sql.Passwords.Single(p => p.EndOnUtc == null).PasswordHash)
            {
                audit.Force();

                var oldPassword = sql.Passwords.Single(l => l.EndOnUtc == null);
                oldPassword.EndOnUtc = utcNow.AddTicks(-1);
                oldPassword.UpdatedBy = _executionContext.UserInfo.UserFullName;
                oldPassword.UpdatedOn = utcNow;

                var newLogin = new SqlUserPassword
                {
                    BeginOnUtc = utcNow,
                    EndOnUtc = null,
                    PasswordHash = user.PasswordHash,
                    PasswordSalt = user.PasswordSalt,
                    UpdatedBy = _executionContext.UserInfo.UserFullName,
                    UpdatedOn = utcNow,
                };

                sql.Passwords.Add(newLogin);
            }

            if (user.CurrentEmail != sql.Emails.Single(e => e.Status == SqlUserEmail.ActiveStatus).Email)
            {
                audit.Force();

                var oldEmail = sql.Emails.Single(e => e.Status == SqlUserEmail.ActiveStatus);

                oldEmail.Status = SqlUserEmail.InactiveStatus;
                oldEmail.StatusDate = _executionContext.SystemClock.Now.Date;
                oldEmail.UpdatedBy = _executionContext.UserInfo.UserFullName;
                oldEmail.UpdatedOn = utcNow;

                var newEmail = new SqlUserEmail
                {
                    Email = user.CurrentEmail,
                    Status = SqlUserEmail.ActiveStatus,
                    StatusDate = _executionContext.SystemClock.Now.Date,
                    UpdatedBy = _executionContext.UserInfo.UserFullName,
                    UpdatedOn = utcNow
                };

                sql.Emails.Add(newEmail);
            }
        }
    }
}
