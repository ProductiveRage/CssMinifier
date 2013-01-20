using System;

namespace CSSMinifier.Logging
{
	public class NullLogger : ILogEvents
	{
		/// <summary>
		/// This will throw an exception if issues are encountered - this includes cases of null or empty content (Exception is optional and so may be null),
		/// logDate is specified so that asynchronous or postponed logging can be implemented
		/// </summary>
		public void Log(LogLevel logLevel, DateTime logDate, Func<string> contentGenerator, Exception exception) { }
	}
}
