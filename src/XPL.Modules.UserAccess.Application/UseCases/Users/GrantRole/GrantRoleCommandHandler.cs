using Functional.Option;
using System.Threading;
using System.Threading.Tasks;
using XPL.Framework.Application.Contracts;
using XPL.Modules.Kernel.Security;
using XPL.Modules.UserAccess.Domain.Users;
using XPL.Modules.UserAccess.Infrastructure.Data.Model.Users;

namespace XPL.Modules.UserAccess.Application.UseCases.Users.GrantRole
{
    public class GrantRoleCommandHandler : ICommandHandler<GrantRoleCommand, string>
    {
        private readonly UserRepository _userRepository;

        public GrantRoleCommandHandler(UserRepository userRepository) => _userRepository = userRepository;

        public Task<Result<string>> Handle(GrantRoleCommand request, CancellationToken cancellationToken)
        {
            var option = _userRepository.TryFindByLogin(request.Login);

            if (!(option is Some<User> some))
                return request.Fail("Invalid login");

            var user = some.Content;

            user.GrantRole(new Role(request.Role));

            return request.Ok("Role granted");
        }
    }
}
