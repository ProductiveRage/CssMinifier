using System;
using System.Linq;
using System.Text;

namespace CSSMinifier.FileLoaders
{
	/// <summary>
	/// This will remove CSS and LessCSS comments (the former only supports /*..*/ multi-line comments, while the latter also supports // single line comments) without
	/// removing any line returns, such that all remaining content lines still have the same line number as before the comment removal
	/// </summary>
	public class LessCssCommentRemovingTextFileLoader : ITextFileLoader
	{
		private readonly ITextFileLoader _fileLoader;
		public LessCssCommentRemovingTextFileLoader(ITextFileLoader fileLoader)
		{
			if (fileLoader == null)
				throw new ArgumentNullException("fileLoader");

			_fileLoader = fileLoader;
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
				Process(unprocessedContent.Content)
			);
		}

		/// <summary>
		/// This will throw an exception for null content or if otherwise unable to satisfy the request, it will never return null
		/// </summary>
		private string Process(string content)
		{
			if (content == null)
				throw new ArgumentNullException("content");

			var stringBuilder = new StringBuilder();
			foreach (var cssSegment in CSSParser.Parser.ParseLESS(content))
			{
				if (cssSegment.CharacterCategorisation != CSSParser.ContentProcessors.CharacterCategorisationOptions.Comment)
				{
					stringBuilder.Append(cssSegment.Value);
					continue;
				}
				stringBuilder.Append(string.Join<char>("", cssSegment.Value.Where(c => (c == '\r') || (c == '\n'))));
			}
			return stringBuilder.ToString();
		}
	}
}
