using System;
using CSSMinifier.Logging;
using dotless.Core;
using dotless.Core.Loggers;
using dotless.Core.Parser;

namespace CSSMinifier.FileLoaders
{
	public class DotLessCssCssLoader : ITextFileLoader
	{
		private readonly ITextFileLoader _contentLoader;
		private readonly LessCssMinificationTypeOptions _minificationType;
		private readonly ReportedErrorBehaviourOptions _reportedErrorBehaviour;
		private readonly ILogEvents _logger;
		public DotLessCssCssLoader(
			ITextFileLoader contentLoader,
			LessCssMinificationTypeOptions minificationType,
			ReportedErrorBehaviourOptions reportedErrorBehaviour,
			ILogEvents logger)
		{
			if (contentLoader == null)
				throw new ArgumentNullException("contentLoader");
			if (!Enum.IsDefined(typeof(LessCssMinificationTypeOptions), minificationType))
				throw new ArgumentOutOfRangeException("minificationType");
			if (!Enum.IsDefined(typeof(ReportedErrorBehaviourOptions), reportedErrorBehaviour))
				throw new ArgumentOutOfRangeException("reportedErrorBehaviour");
			if (logger == null)
				throw new ArgumentNullException("logger");

			_contentLoader = contentLoader;
			_minificationType = minificationType;
			_reportedErrorBehaviour = reportedErrorBehaviour;
			_logger = logger;
		}

		public enum LessCssMinificationTypeOptions
		{
			DoNotMinify,
			Minify
		}

		public enum ReportedErrorBehaviourOptions
		{
			LogAndContinue,
			LogAndRaiseException
		}

		/// <summary>
		/// This will never return null. It will throw an exception for a null or blank relativePath.
		/// </summary>
		public TextFileContents Load(string relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				throw new ArgumentException("Null/blank relativePath specified");

			var initialFileContents = _contentLoader.Load(relativePath);
			var engine = new LessEngine(
				new Parser(),
				new DotLessCssPassThroughLogger(_logger, _reportedErrorBehaviour),
				_minificationType == LessCssMinificationTypeOptions.Minify,
				false // Debug
			);
			return new TextFileContents(
				initialFileContents.RelativePath,
				initialFileContents.LastModified,
				engine.TransformToCss(initialFileContents.Content, null)
			);
		}

		private class DotLessCssPassThroughLogger : ILogger
		{
			private readonly ILogEvents _logger;
			private readonly ReportedErrorBehaviourOptions _reportedErrorBehaviour;
			public DotLessCssPassThroughLogger(ILogEvents logger, ReportedErrorBehaviourOptions reportedErrorBehaviour)
			{
				if (logger == null)
					throw new ArgumentNullException("logger");
				if (!Enum.IsDefined(typeof(ReportedErrorBehaviourOptions), reportedErrorBehaviour))
					throw new ArgumentOutOfRangeException("reportedErrorBehaviour");

				_logger = logger;
				_reportedErrorBehaviour = reportedErrorBehaviour;
			}

			public void Debug(string message, params object[] args)
			{
				if (string.IsNullOrWhiteSpace(message))
					return;

				_logger.Log(Logging.LogLevel.Debug, DateTime.Now, () => message, null);
			}

			public void Error(string message, params object[] args)
			{
				if (string.IsNullOrWhiteSpace(message))
					return;

				_logger.Log(Logging.LogLevel.Error, DateTime.Now, () => message, null);
				
				if (_reportedErrorBehaviour == ReportedErrorBehaviourOptions.LogAndRaiseException)
					throw new Exception("dotLess parsing error: " + message);
			}

			public void Info(string message, params object[] args)
			{
				if (string.IsNullOrWhiteSpace(message))
					return;

				_logger.Log(Logging.LogLevel.Info, DateTime.Now, () => message, null);
			}

			public void Log(dotless.Core.Loggers.LogLevel level, string message, params object[] args)
			{
				if (string.IsNullOrWhiteSpace(message))
					return;

				switch (level)
				{
					case dotless.Core.Loggers.LogLevel.Debug:
						Debug(message);
						return;

					case dotless.Core.Loggers.LogLevel.Error:
						Error(message);
						return;

					case dotless.Core.Loggers.LogLevel.Info:
						Info(message);
						return;

					case dotless.Core.Loggers.LogLevel.Warn:
						Warn(message);
						return;

					default:
						_logger.Log(Logging.LogLevel.Warning, DateTime.Now, () => "DotLess logged message with unsupported LogLeve [" + level.ToString() + "]: " + message, null);
						return;
				}
			}

			public void Warn(string message, params object[] args)
			{
				if (string.IsNullOrWhiteSpace(message))
					return;

				_logger.Log(Logging.LogLevel.Warning, DateTime.Now, () => message, null);
			}

			public void Log(dotless.Core.Loggers.LogLevel level, string message) { Log(level, message, new object[0]); }
			public void Debug(string message) { Debug(message, new object[0]); }
			public void Error(string message) { Error(message, new object[0]); }
			public void Info(string message) { Info(message, new object[0]); }
			public void Warn(string message) { Warn(message, new object[0]); }
		}
	}
}
