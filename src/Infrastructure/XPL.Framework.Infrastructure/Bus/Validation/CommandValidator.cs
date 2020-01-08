using Functional.Option;
using Lamar;
using System;
using System.Collections.Generic;
using XPL.Framework.Application.Modules.Contracts;

namespace XPL.Framework.Infrastructure.Bus.Validation
{
    public sealed class CommandValidator : ICommandValidator
    {
        private readonly IContainer _container;

        public CommandValidator(IContainer container) => _container = container;

        public Option<CommandErrorList> Validate<TResult>(ICommand<TResult> command)
        {
            Type validatorType = typeof(ICommandRule<>).MakeGenericType(command.GetType());
            var rules = _container.GetAllInstances(validatorType);

            var errors = new List<CommandError>();

            foreach (var rule in rules)
            {
                Option<CommandError> option = ((dynamic)rule).Validate((dynamic)command);
                if (option is Some<CommandError> error)
                    errors.Add(error.Content);
            }

            if (errors.Count == 0)
                return None.Value;

            return new CommandErrorList(errors);
        }
    }
}
