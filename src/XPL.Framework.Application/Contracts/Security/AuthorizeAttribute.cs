using System;

namespace XPL.Framework.Application.Contracts.Security
{
    [AttributeUsage(AttributeTargets.Class)]
    public class AuthorizeAttribute : Attribute
    {
        public string Login { get; set; } = "";
        public string Roles { get; set; } = "";
    }
}
