using System;

namespace XPL.Framework.Ports
{
    public interface ILogger
    {
        void Debug(string messageTemplate);
        void Debug(string messageTemplate, params object[] propertyValues);
        void Debug(Exception exception, string messageTemplate);
        void Debug(Exception exception, string messageTemplate, params object[] propertyValues);

        void Error(string messageTemplate);
        void Error(string messageTemplate, params object[] propertyValues);
        void Error(Exception exception, string messageTemplate);
        void Error(Exception exception, string messageTemplate, params object[] propertyValues);

        void Fatal(string messageTemplate);
        void Fatal(string messageTemplate, params object[] propertyValues);
        void Fatal(Exception exception, string messageTemplate);
        void Fatal(Exception exception, string messageTemplate, params object[] propertyValues);

        void Info(string messageTemplate);
        void Info(string messageTemplate, params object[] propertyValues);
        void Info(Exception exception, string messageTemplate);
        void Info(Exception exception, string messageTemplate, params object[] propertyValues);

        void Warning(string messageTemplate);
        void Warning(string messageTemplate, params object[] propertyValues);
        void Warning(Exception exception, string messageTemplate);
        void Warning(Exception exception, string messageTemplate, params object[] propertyValues);
    }
}
