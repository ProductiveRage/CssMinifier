using System;
using System.IO;
using System.Text;
using CSSMinifier.FileLoaders;
using CSSMinifier.Logging;

namespace CSSMinifier.Caching
{
	/// <summary>
	/// This should only be used with TextFileContent instances that represent CSS due to the manner in which meta data about the relative path is stored in the contents
	/// </summary>
	public class CSSFileDiskCache : ICacheThingsWithModifiedDates<TextFileContents>
	{
		private readonly CacheKeyTranslator _cacheKeyTranslator;
		private readonly ErrorBehaviourOptions _errorBehaviour;
		private readonly ILogEvents _logger;
		public CSSFileDiskCache(CacheKeyTranslator cacheKeyTranslator, ErrorBehaviourOptions errorBehaviour, ILogEvents logger)
		{
			if (cacheKeyTranslator == null)
				throw new ArgumentNullException("cacheKeyTranslator");
			if (!Enum.IsDefined(typeof(ErrorBehaviourOptions), errorBehaviour))
				throw new ArgumentOutOfRangeException("errorBehaviour");
			if (logger == null)
				throw new ArgumentNullException("logger");

			_cacheKeyTranslator = cacheKeyTranslator;
			_errorBehaviour = errorBehaviour;
			_logger = logger;
		}

		/// <summary>
		/// This determine where in the file system that the backing file for each cache key is located. If multiple cache keys correspond to the same file then only one
		/// entry will be recorded, further Add calls for cache keys that share the file will overwrite the file. This will never be called with a null or blank cacheKey
		/// and must never return null.
		/// </summary>
		public delegate FileInfo CacheKeyTranslator(string cacheKey);

		public enum ErrorBehaviourOptions
		{
			LogAndContinue,
			LogAndRaiseException
		}

		/// <summary>
		/// This will return null if the entry is not present in the cache. It will throw an exception for null or blank cacheKey. If data was found in the cache for the
		/// specified cache key that was not of type T then null will be returned, but whether the invalid entry is removed from the cache depends upon the implementation.
		/// </summary>
		public TextFileContents this[string cacheKey]
		{
			get
			{
				if (string.IsNullOrWhiteSpace(cacheKey))
					throw new ArgumentException("Null/blank cacheKey specified");

				FileInfo file;
				try
				{
					file = _cacheKeyTranslator(cacheKey);
					if (file == null)
						throw new Exception("CacheKeyTranslator returned null");
				}
				catch
				{
					_logger.LogIgnoringAnyError(LogLevel.Error, () => "TextFileContentsDiskCache.Remove: CacheKeyTranslator returned null");
					if (_errorBehaviour == ErrorBehaviourOptions.LogAndRaiseException)
						throw;
					return null;
				}

				try
				{
					using (var stream = file.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
					{
						using (var reader = new StreamReader(stream))
						{
							return GetFileContents(reader, file.LastWriteTime);
						}
					}
				}
				catch (Exception e)
				{
					_logger.LogIgnoringAnyError(LogLevel.Error, () => "TextFileContentsDiskCache.Remove: Error loading content - " + e.Message);
					if (_errorBehaviour == ErrorBehaviourOptions.LogAndRaiseException)
						throw;
					return null;
				}
			}
		}

		/// <summary>
		/// The caching mechanism (eg. cache duration) is determine by the ICache implementation. This will throw an exception for null or blank cacheKey or null value.
		/// </summary>
		public void Add(string cacheKey, TextFileContents value)
		{
			if (string.IsNullOrWhiteSpace(cacheKey))
				throw new ArgumentException("Null/blank cacheKey specified");
			if (value == null)
				throw new ArgumentNullException("value");

			FileInfo file;
			try
			{
				file = _cacheKeyTranslator(cacheKey);
				if (file == null)
					throw new Exception("CacheKeyTranslator returned null");
				if (file.LastWriteTime >= value.LastModified)
					return;
			}
			catch
			{
				_logger.LogIgnoringAnyError(LogLevel.Error, () => "TextFileContentsDiskCache.Remove: CacheKeyTranslator returned null");
				if (_errorBehaviour == ErrorBehaviourOptions.LogAndRaiseException)
					throw;
				return;
			}

			try
			{
				File.WriteAllText(file.FullName, GetFileContentRepresentation(value));
			}
			catch (Exception e)
			{
				_logger.LogIgnoringAnyError(LogLevel.Warning, () => "TextFileContentsDiskCache.Add: Error writing file - " + e.Message, e);
				if (_errorBehaviour == ErrorBehaviourOptions.LogAndRaiseException)
					throw;
			}
		}

		/// <summary>
		/// This will do nothing if there is no entry for the specified cacheKey. It will throw an exception for null or blank cacheKey.
		/// </summary>
		public void Remove(string cacheKey)
		{
			if (string.IsNullOrWhiteSpace(cacheKey))
				throw new ArgumentException("Null/blank cacheKey specified");

			FileInfo file;
			try
			{
				file = _cacheKeyTranslator(cacheKey);
				if (file == null)
					throw new Exception("CacheKeyTranslator returned null");
			}
			catch
			{
				_logger.LogIgnoringAnyError(LogLevel.Error, () => "TextFileContentsDiskCache.Remove: CacheKeyTranslator returned null");
				if (_errorBehaviour == ErrorBehaviourOptions.LogAndRaiseException)
					throw;
				return;
			}

			try
			{
				if (file.Exists)
					file.Delete();
			}
			catch (Exception e)
			{
				_logger.LogIgnoringAnyError(LogLevel.Warning, () => "TextFileContentsDiskCache.Remove: Error deleting file - " + e.Message, e);
				if (_errorBehaviour == ErrorBehaviourOptions.LogAndRaiseException)
					throw;
			}
		}

		/// <summary>
		/// This is only exposed for unit testing. This will throw an exception if unable to generate the content, it will never return null or a blank string.
		/// </summary>
		public static string GetFileContentRepresentation(TextFileContents contents)
		{
			if (contents == null)
				throw new ArgumentNullException("contents");

			var contentBuilder = new StringBuilder();
			contentBuilder.AppendFormat(
				"/*{0}:{1}*/{2}",
				contents.RelativePath.Length.ToString(
					new string('0', int.MaxValue.ToString().Length) // Pad out the length to the number of digits required to display int.MaxValue
				),
				contents.RelativePath,
				Environment.NewLine
			);
			contentBuilder.Append(contents.Content);
			return contentBuilder.ToString();
		}

		/// <summary>
		/// This is only exposed for unit testing. This will throw an exception if unable to retrieve the content from the reader (including cases where the reader is null),
		/// it will never return null.
		/// </summary>
		public static TextFileContents GetFileContents(TextReader reader, DateTime lastModified)
		{
			if (reader == null)
				throw new ArgumentNullException("reader");

			var openCommentBuffer = new char[2];
			if ((reader.ReadBlock(openCommentBuffer, 0, openCommentBuffer.Length) < openCommentBuffer.Length) || (openCommentBuffer[0] != '/') || (openCommentBuffer[1] != '*'))
				throw new ArgumentException("Invalid content (opening comment)");
			var maxValueLength = int.MaxValue.ToString().Length;
			var relativePathLengthBuffer = new char[maxValueLength];
			if (reader.ReadBlock(relativePathLengthBuffer, 0, maxValueLength) < maxValueLength)
				throw new ArgumentException("Invalid content (relative path length)");
			int relativePathLength;
			if (!int.TryParse(new string(relativePathLengthBuffer), out relativePathLength))
				throw new ArgumentException("Invalid content (relative path length content)");
			var separatorBuffer = new char[1];
			if ((reader.ReadBlock(separatorBuffer, 0, separatorBuffer.Length) < separatorBuffer.Length) || (separatorBuffer[0] != ':'))
				throw new ArgumentException("Invalid content (separator)");
			var relativePathBuffer = new char[relativePathLength];
			if (reader.ReadBlock(relativePathBuffer, 0, relativePathBuffer.Length) < relativePathBuffer.Length)
				throw new ArgumentException("Invalid content (relative path)");
			var closeCommentBuffer = new char[2];
			if ((reader.ReadBlock(closeCommentBuffer, 0, closeCommentBuffer.Length) < closeCommentBuffer.Length) || (closeCommentBuffer[0] != '*') || (closeCommentBuffer[1] != '/'))
				throw new ArgumentException("Invalid content (closing comment)");
			var newLineBuffer = new char[Environment.NewLine.Length];
			if (reader.ReadBlock(newLineBuffer, 0, newLineBuffer.Length) < newLineBuffer.Length)
				throw new ArgumentException("Invalid content (new line length)");
			for (var index = 0; index < Environment.NewLine.Length; index++)
			{
				if (newLineBuffer[index] != Environment.NewLine[index])
					throw new ArgumentException("Invalid content (new line content)");
			}
			return new TextFileContents(
				new string(relativePathBuffer),
				lastModified,
				reader.ReadToEnd()
			);
		}
	}
}
