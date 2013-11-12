using System;
using System.Collections.Generic;

namespace CSSMinifier.FileLoaders
{
	/// <summary>
	/// TODO
	/// </summary>
	public class MultiContentReplacingTextFileLoader : ITextFileLoader
	{
		private readonly ITextFileLoader _fileLoader;
		private readonly ReplacementRetriever _replacementRetriever;
		public MultiContentReplacingTextFileLoader(ITextFileLoader fileLoader, ReplacementRetriever replacementRetriever)
		{
			if (fileLoader == null)
				throw new ArgumentNullException("fileLoader");
			if (replacementRetriever == null)
				throw new ArgumentNullException("replacementRetriever");

			_fileLoader = fileLoader;
			_replacementRetriever = replacementRetriever;
		}

		/// <summary>
		/// TODO
		/// </summary>
		public delegate IEnumerable<KeyValuePair<string, string>> ReplacementRetriever();

		/// <summary>
		/// This will never return null, it will throw an exception for a null or empty filename - it is up to the particular implementation whether or not to throw an
		/// exception for invalid / inaccessible filenames (if no exception is thrown, the issue should be logged). It is up the the implementation to handle mapping
		/// the relative path to a full file path.
		/// </summary>
		public TextFileContents Load(string relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				throw new ArgumentException("Null/blank relativePath specified");

			var content = _fileLoader.Load(relativePath);
			foreach (var replacement in _replacementRetriever())
			{
				content = new TextFileContents(
					content.RelativePath,
					content.LastModified,
					content.Content.Replace(replacement.Key, replacement.Value)
				);
			}
			return content;
		}
	}
}
