using System;
using CSSMinifier.Logging;
using dotless.Core;
using dotless.Core.Loggers;
using dotless.Core.Parser;

namespace CSSMinifier.FileLoaders
{
	public class DotLessCssCssLoader : ITextFileLoader
	{
		private ITextFileLoader _contentLoader;
		private LessCssMinificationTypeOptions _minificationType;
		private ILogEvents _logger;
		public DotLessCssCssLoader(ITextFileLoader contentLoader, LessCssMinificationTypeOptions minificationType, ILogEvents logger)
		{
			if (contentLoader == null)
				throw new ArgumentNullException("contentLoader");
			if (!Enum.IsDefined(typeof(LessCssMinificationTypeOptions), minificationType))
				throw new ArgumentOutOfRangeException("minificationType");
			if (logger == null)
				throw new ArgumentNullException("logger");

			_contentLoader = contentLoader;
			_minificationType = minificationType;
			_logger = logger;
		}

		public enum LessCssMinificationTypeOptions
		{
			DoNotMinify,
			Minify
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
				new DotLessCssPassThroughLogger(_logger),
				_minificationType == LessCssMinificationTypeOptions.Minify
			);
			return new TextFileContents(
				initialFileContents.Filename,
				initialFileContents.LastModified,
				engine.TransformToCss(initialFileContents.Content, null).Replace("}", "}\n")
			);
		}

		private class DotLessCssPassThroughLogger : ILogger
		{
			private ILogEvents _logger;
			public DotLessCssPassThroughLogger(ILogEvents logger)
			{
				if (logger == null)
					throw new ArgumentNullException("logger");

				_logger = logger;
			}

			public void Debug(string message)
			{
				if (string.IsNullOrWhiteSpace(message))
					return;

				_logger.LogIgnoringAnyError(Logging.LogLevel.Debug, () => message);
			}

			public void Error(string message)
			{
				if (string.IsNullOrWhiteSpace(message))
					return;

				_logger.LogIgnoringAnyError(Logging.LogLevel.Error, () => message);
			}

			public void Info(string message)
			{
				if (string.IsNullOrWhiteSpace(message))
					return;

				_logger.LogIgnoringAnyError(Logging.LogLevel.Info, () => message);
			}

			public void Log(dotless.Core.Loggers.LogLevel level, string message)
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
						_logger.LogIgnoringAnyError(Logging.LogLevel.Warning, () => "DotLess logged message with unsupported LogLeve [" + level.ToString() + "]: " + message);
						return;
				}
			}

			public void Warn(string message)
			{
				if (string.IsNullOrWhiteSpace(message))
					return;

				_logger.LogIgnoringAnyError(Logging.LogLevel.Warning, () => message);
			}
		}
	}
}
