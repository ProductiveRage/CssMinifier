using System;
using System.Collections.Generic;

namespace CSSMinifier.FileLoaders
{
	/// <summary>
	/// This will make a series of replacements in content, in the order in which they are specified. No intelligence is applied, nor is any parsing performed - these
	/// are just straight replacements. This may be used with the SourceMappingMarkerIdGenerator / LessCssLineNumberingTextFileLoader combination.
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
		/// This must not return null, nor any entries with a null Key or Value. It will apply the replacements in the order provided.
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
