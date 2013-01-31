using System;

namespace CSSMinifier.FileLoaders
{
	/// <summary>
	/// This will perform a simple string replacement on the loaded content. It is intended for use with LessCssOpeningBodyTagRenamer (see its comments for more details)
	/// </summary>
	public class ContentReplacingTextFileLoader : ITextFileLoader
	{
		private readonly ITextFileLoader _fileLoader;
		private readonly string _stringToBeReplaced;
		private readonly string _stringToReplaceWith;
		public ContentReplacingTextFileLoader(ITextFileLoader fileLoader, string stringToBeReplaced, string stringToReplaceWith)
		{
			if (fileLoader == null)
				throw new ArgumentNullException("fileLoader");
			if (string.IsNullOrEmpty(stringToBeReplaced))
				throw new ArgumentException("Null/blank stringToBeReplaced specified");
			if (stringToReplaceWith == null)
				throw new ArgumentNullException("stringToReplaceWith");

			_fileLoader = fileLoader;
			_stringToBeReplaced = stringToBeReplaced;
			_stringToReplaceWith = stringToReplaceWith;
		}

		/// <summary>
		/// This will never return null, it will throw an exception for a null or empty filename - it is up to the particular implementation whether or not to throw an
		/// exception for invalid / inaccessible filenames (if no exception is thrown, the issue should be logged). It is up the the implementation to handle mapping
		/// the relative path to a full file path.
		/// </summary>
		public TextFileContents Load(string relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				throw new ArgumentException("Null/blank relativePath specified");

			var unprocessedContent = _fileLoader.Load(relativePath);
			return new TextFileContents(
				unprocessedContent.RelativePath,
				unprocessedContent.LastModified,
				unprocessedContent.Content.Replace(_stringToBeReplaced, _stringToReplaceWith)
			);
		}
	}
}
