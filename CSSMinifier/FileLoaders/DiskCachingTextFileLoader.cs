using System;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Text;
using CSSMinifier.FileLoaders.LastModifiedDateRetrievers;
using CSSMinifier.Logging;

namespace CSSMinifier.FileLoaders
{
	/// <summary>
	/// This will add a disk cache layer to the loader. It implements ITextFileLoader instead of being an ICacheThingsWithModifiedDates implementation so that is has
	/// access to the source file's relative path when generating a filename for the cache data. If caching the output of processed and import-flattened style sheet
	/// content then it requires a way to determine whether the cached data has expired, which is the purpose of the lastModifiedDateRetriever (if import-flattening
	/// is being done with a SameFolderImportFlatteningCssLoader then this could be the most recent write time of any .css or .less file in the folder indicated by
	/// the relativePath value - this would allow the cache file to be written in the same location if a .csscache file extension is used, for example). Some
	/// metadata about the content will be stored in a comment in the written cache file and so the written file should not be edited manually. 
	/// </summary>
	public class DiskCachingTextFileLoader : ITextFileLoader
	{
		private readonly ITextFileLoader _contentLoader;
		private readonly CacheFileLocationRetriever _cacheFileLocationRetriever;
		private readonly ILastModifiedDateRetriever _lastModifiedDateRetriever;
		private readonly ErrorBehaviourOptions _errorBehaviour;
		private readonly ILogEvents _logger;
		public DiskCachingTextFileLoader(
			ITextFileLoader contentLoader,
			CacheFileLocationRetriever cacheFileLocationRetriever,
			ErrorBehaviourOptions errorBehaviour,
			ILastModifiedDateRetriever lastModifiedDateRetriever,
			ILogEvents logger)
		{
			if (contentLoader == null)
				throw new ArgumentNullException("contentLoader");
			if (cacheFileLocationRetriever == null)
				throw new ArgumentNullException("cacheFileLocationRetriever");
			if (lastModifiedDateRetriever == null)
				throw new ArgumentNullException("lastModifiedDateRetriever");
			if (!Enum.IsDefined(typeof(ErrorBehaviourOptions), errorBehaviour))
				throw new ArgumentOutOfRangeException("errorBehaviour");
			if (logger == null)
				throw new ArgumentNullException("logger");

			_contentLoader = contentLoader;
			_cacheFileLocationRetriever = cacheFileLocationRetriever;
			_lastModifiedDateRetriever = lastModifiedDateRetriever;
			_errorBehaviour = errorBehaviour;
			_logger = logger;
		}

		/// <summary>
		/// This determine where in the file system that the backing file for the specified relative path should be. This will never be called with a null or blank
		/// relativePath and must never return null.
		/// </summary>
		public delegate FileInfo CacheFileLocationRetriever(string relativePath);

		/// <summary>
		/// This will never return null, it will throw an exception for a null or empty relativePath - it is up to the particular implementation whether or not to throw
		/// an exception for invalid / inaccessible filenames (if no exception is thrown, the issue should be logged). It is up the the implementation to handle mapping
		/// the relative path to a full file path.
		/// </summary>
		public TextFileContents Load(string relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				throw new ArgumentException("Null/blank relativePath specified");

			// Try to retrieve cached data
			var lastModifiedDateOfSource = _lastModifiedDateRetriever.GetLastModifiedDate(relativePath);
			var cacheFile = _cacheFileLocationRetriever(relativePath);
			cacheFile.Refresh();
			if (cacheFile.Exists)
			{
				try
				{
					using (var stream = cacheFile.Open(FileMode.Open, FileAccess.Read, FileShare.Read))
					{
						using (var reader = new StreamReader(stream))
						{
							var cachedData = GetFileContents(reader);
							if (cachedData.LastModified >= lastModifiedDateOfSource)
								return cachedData;
						}
					}
				}
				catch (Exception e)
				{
					_logger.LogIgnoringAnyError(LogLevel.Error, () => "DiskCachingTextFileLoader.Load: Error loading content - " + e.Message);
					if (_errorBehaviour == ErrorBehaviourOptions.LogAndRaiseException)
						throw;
					return null;
				}
			}

			// Do the work and cache the result
			var content = _contentLoader.Load(relativePath);
			try
			{
				File.WriteAllText(cacheFile.FullName, GetFileContentRepresentation(content));
			}
			catch (Exception e)
			{
				_logger.LogIgnoringAnyError(LogLevel.Warning, () => "DiskCachingTextFileLoader.Add: Load writing file - " + e.Message, e);
				if (_errorBehaviour == ErrorBehaviourOptions.LogAndRaiseException)
					throw;
			}
			return content;
		}

		private const string LastModifiedDateFormat = "yyyy-MM-dd HH:mm:ss.fff";

		/// <summary>
		/// This is only exposed for unit testing. This will throw an exception if unable to generate the content, it will never return null or a blank string.
		/// </summary>
		public static string GetFileContentRepresentation(TextFileContents contents)
		{
			if (contents == null)
				throw new ArgumentNullException("contents");

			var contentBuilder = new StringBuilder();
			contentBuilder.AppendFormat(
				"/*{0}:{1}:{2}*/{3}",
				contents.RelativePath.Length.ToString(
					new string('0', int.MaxValue.ToString().Length) // Pad out the length to the number of digits required to display int.MaxValue
				),
				contents.RelativePath,
				contents.LastModified.ToString(LastModifiedDateFormat),
				Environment.NewLine
			);
			contentBuilder.Append(contents.Content);
			return contentBuilder.ToString();
		}

		/// <summary>
		/// This is only exposed for unit testing. This will throw an exception if unable to retrieve the content from the reader (including cases where the reader is null, specifically
		/// an InvalidCacheFileFormatException if the content could be read but it was invalid). It will never return null.
		/// </summary>
		public static TextFileContents GetFileContents(TextReader reader)
		{
			if (reader == null)
				throw new ArgumentNullException("reader");

			var openCommentBuffer = new char[2];
			if ((reader.ReadBlock(openCommentBuffer, 0, openCommentBuffer.Length) < openCommentBuffer.Length) || (openCommentBuffer[0] != '/') || (openCommentBuffer[1] != '*'))
				throw new InvalidCacheFileFormatException("Invalid content (opening comment)");
			
			var maxValueLength = int.MaxValue.ToString().Length;
			var relativePathLengthBuffer = new char[maxValueLength];
			if (reader.ReadBlock(relativePathLengthBuffer, 0, maxValueLength) < maxValueLength)
				throw new InvalidCacheFileFormatException("Invalid content (relative path length)");
			int relativePathLength;
			if (!int.TryParse(new string(relativePathLengthBuffer), out relativePathLength))
				throw new InvalidCacheFileFormatException("Invalid content (relative path length content)");
			
			var lengthPathSeparatorBuffer = new char[1];
			if ((reader.ReadBlock(lengthPathSeparatorBuffer, 0, lengthPathSeparatorBuffer.Length) < lengthPathSeparatorBuffer.Length) || (lengthPathSeparatorBuffer[0] != ':'))
				throw new InvalidCacheFileFormatException("Invalid content (Length:RelativePath SeparatorBuffer)");
			
			var relativePathBuffer = new char[relativePathLength];
			if (reader.ReadBlock(relativePathBuffer, 0, relativePathBuffer.Length) < relativePathBuffer.Length)
				throw new InvalidCacheFileFormatException("Invalid content (relative path)");

			var pathModifiedDateSeparatorBuffer = new char[1];
			if ((reader.ReadBlock(pathModifiedDateSeparatorBuffer, 0, pathModifiedDateSeparatorBuffer.Length) < pathModifiedDateSeparatorBuffer.Length) || (pathModifiedDateSeparatorBuffer[0] != ':'))
				throw new InvalidCacheFileFormatException("Invalid content (RelativePath:LastModifiedDate SeparatorBuffer)");

			var modifiedDateBuffer = new char[LastModifiedDateFormat.Length];
			if ((reader.ReadBlock(modifiedDateBuffer, 0, modifiedDateBuffer.Length) < modifiedDateBuffer.Length))
				throw new InvalidCacheFileFormatException("Invalid content (LastModifiedDate)");
			DateTime lastModified;
			if (!DateTime.TryParseExact(new string(modifiedDateBuffer), LastModifiedDateFormat, null, DateTimeStyles.None, out lastModified))
				throw new InvalidCacheFileFormatException("Invalid content (LastModifiedDate format)");

			var closeCommentBuffer = new char[2];
			if ((reader.ReadBlock(closeCommentBuffer, 0, closeCommentBuffer.Length) < closeCommentBuffer.Length) || (closeCommentBuffer[0] != '*') || (closeCommentBuffer[1] != '/'))
				throw new InvalidCacheFileFormatException("Invalid content (closing comment)");
			
			var newLineBuffer = new char[Environment.NewLine.Length];
			if (reader.ReadBlock(newLineBuffer, 0, newLineBuffer.Length) < newLineBuffer.Length)
				throw new InvalidCacheFileFormatException("Invalid content (new line length)");
			for (var index = 0; index < Environment.NewLine.Length; index++)
			{
				if (newLineBuffer[index] != Environment.NewLine[index])
					throw new InvalidCacheFileFormatException("Invalid content (new line content)");
			}
			
			return new TextFileContents(
				new string(relativePathBuffer),
				lastModified,
				reader.ReadToEnd()
			);
		}

		public class InvalidCacheFileFormatException : Exception
		{
			public InvalidCacheFileFormatException(string message) : base(message)
			{
				if (string.IsNullOrWhiteSpace(message))
					throw new ArgumentException("Null/blank message specified");
			}

			protected InvalidCacheFileFormatException(SerializationInfo info, StreamingContext context) : base(info, context) { }
		}
	}
}
