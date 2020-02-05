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
                currentLogin.PasswordSalt,
                p.Roles.Where(r => r.EndOnUtc == null).Select(r => r.Role))
                .From(_emailUsage);
        }

        public SqlUser ToPersisted(User model)
        {
            var m = Memento.Get(model);

            var auditor = new Auditor(_executionContext);

            var email = new SqlUserEmail
            {
                Email = m.CurrentEmail,
                Status = SqlUserEmail.ActiveStatus,
                StatusDate = _executionContext.SystemClock.Now.Date,
            }.Audit(auditor);

            var login = new SqlUserPassword
            {
                BeginOnUtc = _executionContext.SystemClock.UtcNow,
                EndOnUtc = null,
                PasswordHash = m.PasswordHash,
                PasswordSalt = m.PasswordSalt,
            }.Audit(auditor);

            var roles = m.Roles.Select(r => new SqlUserRole
            {
                BeginOnUtc = _executionContext.SystemClock.UtcNow,
                EndOnUtc = null,
                Role = r,
            }.Audit(auditor));

            return new SqlUser
            {
                UserId = m.UserId,
                RegistrationId = m.RegistrationId ?? Guid.Empty,
                FirstName = m.FirstName,
                LastName = m.LastName,
                Login = m.CurrentLogin,
                Passwords = new List<SqlUserPassword>(new[] { login }),
                Emails = new List<SqlUserEmail>(new[] { email }),
                Roles = roles.ToList()
            }.Audit(auditor);
        }

        public void CopyChanges(User from, SqlUser sql)
        {
            var auditor = new Auditor(_executionContext);

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
                oldPassword.EndOnUtc = auditor.UtcNow.AddTicks(-1);
                oldPassword.Audit(auditor);

                var newLogin = new SqlUserPassword
                {
                    BeginOnUtc = auditor.UtcNow,
                    EndOnUtc = null,
                    PasswordHash = user.PasswordHash,
                    PasswordSalt = user.PasswordSalt,
                }.Audit(auditor);

                sql.Passwords.Add(newLogin);
            }

            if (user.CurrentEmail != sql.Emails.Single(e => e.Status == SqlUserEmail.ActiveStatus).Email)
            {
                audit.Force();

                var oldEmail = sql.Emails.Single(e => e.Status == SqlUserEmail.ActiveStatus);

                oldEmail.Status = SqlUserEmail.InactiveStatus;
                oldEmail.StatusDate = auditor.Now.Date;
                oldEmail.Audit(auditor);

                var newEmail = new SqlUserEmail
                {
                    Email = user.CurrentEmail,
                    Status = SqlUserEmail.ActiveStatus,
                    StatusDate = auditor.Now.Date,
                }.Audit(auditor);

                sql.Emails.Add(newEmail);
            }

            var addedRoles = user.Roles.Except(sql.Roles.Select(r => r.Role));
            var removedRoles = sql.Roles.Select(r => r.Role).Except(user.Roles).ToList();

            if (addedRoles.Union(removedRoles).Any())
                audit.Force();

            foreach (var role in addedRoles)
            {
                sql.Roles.Add(new SqlUserRole
                {
                    BeginOnUtc = auditor.UtcNow,
                    EndOnUtc = null,
                    Role = role,
                }.Audit(auditor));
            }

            foreach (var role in removedRoles)
            {
                var sqlRole = sql.Roles.Single(s => s.Role == role);
                sqlRole.EndOnUtc = auditor.UtcNow;
                sqlRole.Audit(auditor);
            }
        }
    }
}
