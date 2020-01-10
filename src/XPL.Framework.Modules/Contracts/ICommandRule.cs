﻿using Functional.Option;

namespace XPL.Framework.Modules.Contracts
{
    public interface ICommandRule<in TCommand>
    {
        Option<CommandError> Validate(TCommand command);
    }
}