﻿using MediatR;

namespace XPL.Framework.Modules.Contracts
{
    public interface ICommand : IRequest { }

    public interface ICommand<TResult> : IRequest<TResult> { }
}
