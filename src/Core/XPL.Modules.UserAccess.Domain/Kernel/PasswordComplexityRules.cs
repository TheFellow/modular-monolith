using System.Collections.Generic;
using System.Text.RegularExpressions;
using XPL.Framework.Modules.Domain;

namespace XPL.Modules.UserAccess.Domain.Kernel
{
    public class PasswordComplexityRules
    {
        private IList<(string pattern, string message)> _patterns { get; } = new List<(string, string)>();

        public void EnforceComplexity(string password)
        {
            foreach (var (pattern, message) in _patterns)
                if (!Regex.IsMatch(password, pattern))
                    throw new DomainException(message);

        }

        public static PasswordComplexityRules Default => CharacterDigitAtLeast8;

        public static PasswordComplexityRules CharacterDigitAtLeast8
        {
            get
            {
                var rules = new PasswordComplexityRules();
                rules._patterns.Add((@".*[a-zA-Z]", "Password must contain a character."));
                rules._patterns.Add((@".*[0-9]", "Password must contain a digit."));
                rules._patterns.Add((@".{8}", "Password must be at least 8 characters long."));
                return rules;
            }
        }
    }
}