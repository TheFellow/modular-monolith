﻿using System;
using XPL.Framework.Application.Ports;

namespace XPL.Framework.Infrastructure.Logging
{
    public class SerilogLoggingAdapter : ILogger
    {
        private readonly Serilog.ILogger _logger;

        public SerilogLoggingAdapter(Serilog.ILogger logger) => _logger = logger;

        public void Debug(string messageTemplate) => _logger.Debug(messageTemplate);
        public void Debug(string messageTemplate, params object[] propertyValues) => _logger.Debug(messageTemplate, propertyValues);
        public void Debug(Exception exception, string messageTemplate) => _logger.Debug(exception, messageTemplate);
        public void Debug(Exception exception, string messageTemplate, params object[] propertyValues) => _logger.Debug(exception, messageTemplate, propertyValues);

        public void Error(string messageTemplate) => _logger.Error(messageTemplate);
        public void Error(string messageTemplate, params object[] propertyValues) => _logger.Error(messageTemplate, propertyValues);
        public void Error(Exception exception, string messageTemplate) => _logger.Error(exception, messageTemplate);
        public void Error(Exception exception, string messageTemplate, params object[] propertyValues) => _logger.Error(exception, messageTemplate, propertyValues);

        public void Fatal(string messageTemplate) => _logger.Fatal(messageTemplate);
        public void Fatal(string messageTemplate, params object[] propertyValues) => _logger.Fatal(messageTemplate, propertyValues);
        public void Fatal(Exception exception, string messageTemplate) => _logger.Fatal(exception, messageTemplate);
        public void Fatal(Exception exception, string messageTemplate, params object[] propertyValues) => _logger.Fatal(exception, messageTemplate, propertyValues);

        public void Info(string messageTemplate) => _logger.Information(messageTemplate);
        public void Info(string messageTemplate, params object[] propertyValues) => _logger.Information(messageTemplate, propertyValues);
        public void Info(Exception exception, string messageTemplate) => _logger.Information(exception, messageTemplate);
        public void Info(Exception exception, string messageTemplate, params object[] propertyValues) => _logger.Information(exception, messageTemplate, propertyValues);

        public void Warning(string messageTemplate) => _logger.Warning(messageTemplate);
        public void Warning(string messageTemplate, params object[] propertyValues) => _logger.Warning(messageTemplate, propertyValues);
        public void Warning(Exception exception, string messageTemplate) => _logger.Warning(exception, messageTemplate);
        public void Warning(Exception exception, string messageTemplate, params object[] propertyValues) => _logger.Warning(exception, messageTemplate, propertyValues);
    }
}