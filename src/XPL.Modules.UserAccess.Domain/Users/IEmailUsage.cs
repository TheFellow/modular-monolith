using Functional.Option;
using XPL.Modules.Kernel.Email;
using XPL.Modules.UserAccess.Domain.Kernel;

namespace XPL.Modules.UserAccess.Domain.Users
{
    public interface IEmailUsage
    {
        Option<Login> TryGetLoginForEmail(EmailAddress newEmail);
    }
}