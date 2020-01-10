using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XPL.Framework.Modules.Contracts;

namespace XPL.Modules.UserAccess.Application.UserRegistrations
{
    public class RegisterNewUserCommand : ICommand<RegisterNewUserResponse>
    {
        public RegisterNewUserCommand()
        {
        }
    }
}
