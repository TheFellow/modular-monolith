using XPL.Modules.UserAccess.Domain.Kernel;

namespace XPL.Modules.UserAccess.Domain.Registrations.Rules
{
    public interface ILoginExists
    {
        bool LoginExists(Login login);
    }
}
