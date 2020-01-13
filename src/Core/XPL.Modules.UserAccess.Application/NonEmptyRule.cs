using Functional.Option;
using System;
using System.Linq.Expressions;
using XPL.Framework.Domain;
using XPL.Framework.Modules.Contracts;

namespace XPL.Modules.UserAccess.Application
{
    public abstract class NonEmptyRule<T> : ICommandRule<T>
    {
        private readonly string _name;
        private readonly Func<T, string> _selectorFunc;

        protected abstract Expression<Func<T, string>> _selector { get; }

        public NonEmptyRule(string name = "")
        {
            _name = string.IsNullOrWhiteSpace(name)
                ? ((MemberExpression)_selector.Body).Member.Name
                : name;

            _selectorFunc = _selector.Compile();
        }

        public Option<CommandError> Validate(T command)
        {
            string value = _selectorFunc(command);

            if (string.IsNullOrWhiteSpace(value))
                throw new DomainException($"{_name} cannot be empty");

            return None.Value;
        }
    }
}
