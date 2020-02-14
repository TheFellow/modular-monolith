using System.Collections.Generic;
using System.Linq;

namespace XPL.Modules.Kernel.Security
{
    public static class Roles
    {
        public const string AdminRoleValue = "Application Admin";
        public const string MemberValue = "Member";
        public static Role AdminRole => new Role(AdminRoleValue);
        public static Role Member => new Role(MemberValue);

        private static IEnumerable<Role> ValidRoles { get; } = new List<Role> { AdminRole, Member };

        public static bool IsValidRole(Role role) => ValidRoles.Contains(role);
        public static bool IsValidRole(string role) => IsValidRole(new Role(role));
    }
}
