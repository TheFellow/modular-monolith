using System;
using System.Collections.Generic;
using System.Linq;

namespace XPL.Framework.Domain.Contracts
{
    public class CommandErrorList : CommandError
    {
        public IReadOnlyList<CommandError> Errors { get; }

        public CommandErrorList(IEnumerable<CommandError> failures) : base()
        {
            var errorList = failures.ToList();

            if (errorList.Count == 0)
                throw new ArgumentException("There must be at least one command error.", nameof(failures));

            Errors = errorList.AsReadOnly();
            Error = string.Join(Environment.NewLine, Errors.Select(e => e.Error));
        }
    }
}