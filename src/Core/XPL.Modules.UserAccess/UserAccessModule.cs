using MediatR;
using XPL.Framework.Modules;

namespace XPL.Modules.UserAccess
{
    public class UserAccessModule : Module
    {
        public UserAccessModule(IMediator mediator) : base(mediator)
        {
        }
    }
}
