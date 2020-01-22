﻿using System;
using XPL.Framework.Domain.Contracts;

namespace XPL.Modules.UserAccess.Application.UseCases.UserRegistrations.ConfirmRegistration
{
    public class ConfirmRegistrationCommand : ICommand<ConfirmRegistrationResponse>
    {
        public Guid CorrelationId { get; } = Guid.NewGuid();
        public Guid RegistrationId { get; }
        public string ConfirmationCode { get; }

        public ConfirmRegistrationCommand(Guid registrationId, string confirmationCode)
        {
            RegistrationId = registrationId;
            ConfirmationCode = confirmationCode;
        }
    }
}
