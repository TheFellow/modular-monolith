using System;
using System.Linq.Expressions;
using System.Reflection;
using XPL.Framework.Domain;

namespace XPL.Framework.Infrastructure.Data
{
    public sealed class AuditFieldUpdater<TSource, TPersistence>
    {
        private readonly IExecutionContext _executionContext;
        private readonly TSource _source;
        private readonly TPersistence _persistence;
        private readonly Expression<Func<TPersistence, string>> _updatedBy;
        private readonly Expression<Func<TPersistence, DateTime>> _updatedOn;
        private bool _execAudit = false;

        public AuditFieldUpdater(
            IExecutionContext executionContext,
            TSource source,
            TPersistence persistence,
            Expression<Func<TPersistence, string>> updatedBy,
            Expression<Func<TPersistence, DateTime>> updatedOn)
        {
            _executionContext = executionContext;
            _source = source;
            _persistence = persistence;
            _updatedBy = updatedBy;
            _updatedOn = updatedOn;
        }

        public AuditFieldUpdater<TSource, TPersistence> Map<T>(Expression<Func<TSource, T>> from, Expression<Func<TPersistence, T>> to)
        {
            var toValue = to.Compile()(_persistence);
            var fromValue = from.Compile()(_source);

            if (toValue == null && fromValue == null)
                return this;

            if (toValue == null || !toValue.Equals(fromValue))
            {
                if (!_execAudit)
                    _execAudit = true;

                GetInfo(to).SetValue(_persistence, fromValue, null);
            }

            return this;
        }

        private PropertyInfo GetInfo<TFrom, T>(Expression<Func<TFrom, T>> e)
        {
            var member = (MemberExpression)e.Body;
            return (PropertyInfo)member.Member;
        }

        public TPersistence Audit()
        {
            if (_execAudit)
            {
                GetInfo(_updatedBy).SetValue(_persistence, _executionContext.UserInfo.UserFullName, null);
                GetInfo(_updatedOn).SetValue(_persistence, _executionContext.SystemClock.Now, null);
            }

            return _persistence;
        }

        public void Force()
        {
            _execAudit = true;
            Audit();
        }
    }
}
