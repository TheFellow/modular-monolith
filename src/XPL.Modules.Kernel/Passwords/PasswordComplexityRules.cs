using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace XPL.Modules.Kernel.Passwords
{
    public class PasswordComplexityRules
    {
        private IList<(string pattern, string message)> Patterns { get; } = new List<(string, string)>();

        public void EnforceComplexity(string password)
        {
            foreach (var (pattern, message) in Patterns)
                if (!Regex.IsMatch(password, pattern))
                    throw new DomainException(message);

        }

        public static PasswordComplexityRules Default => CharacterDigitAtLeast8;

        public static PasswordComplexityRules CharacterDigitAtLeast8
        {
            get
            {
                var rules = new PasswordComplexityRules();
                rules.Patterns.Add((@".*[a-zA-Z]", "Password must contain a character."));
                rules.Patterns.Add((@".*[0-9]", "Password must contain a digit."));
                rules.Patterns.Add((@".{8}", "Password must be at least 8 characters long."));
                return rules;
            }
        }
    }
}