using System;
using dotless.Core;
using dotless.Core.Loggers;
using dotless.Core.Parser;

namespace CSSMinifier.FileLoaders
{
	public class DotLessCssCssLoader : ITextFileLoader
	{
		private ITextFileLoader _contentLoader;
		private LessCssMinificationTypeOptions _minificationType;
		public DotLessCssCssLoader(ITextFileLoader contentLoader, LessCssMinificationTypeOptions minificationType)
		{
			if (contentLoader == null)
				throw new ArgumentNullException("contentLoader");
			if (!Enum.IsDefined(typeof(LessCssMinificationTypeOptions), minificationType))
				throw new ArgumentOutOfRangeException("minificationType");

			_contentLoader = contentLoader;
			_minificationType = minificationType;
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
				new NullDotLessCssLogger(),
				_minificationType == LessCssMinificationTypeOptions.Minify
			);
			return new TextFileContents(
				initialFileContents.Filename,
				initialFileContents.LastModified,
				engine.TransformToCss(initialFileContents.Content, null).Replace("}", "}\n")
			);
		}

		private class NullDotLessCssLogger : ILogger
		{
			public void Debug(string message) { }
			public void Error(string message) { }
			public void Info(string message) { }
			public void Log(LogLevel level, string message) { }
			public void Warn(string message) { }
		}
	}
}
