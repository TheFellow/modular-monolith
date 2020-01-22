using System;
using System.Collections.Generic;
using ValueTypes;

namespace XPL.Modules.UserAccess.Domain.Users
{
    public class UserId : Value
    {
        public Guid Value { get; }
        public UserId(Guid userId) => Value = userId;
        public static UserId New() => new UserId(Guid.NewGuid());

        protected override IEnumerable<ValueBase> GetValues() => Yield(Value);
    }
}