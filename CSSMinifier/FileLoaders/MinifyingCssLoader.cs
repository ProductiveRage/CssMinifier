using System;
using System.Text.RegularExpressions;

namespace CSSMinifier.FileLoaders
{
	public class MinifyingCssLoader : ITextFileLoader
	{
		private ITextFileLoader _contentLoader;
		public MinifyingCssLoader(ITextFileLoader contentLoader)
		{
			if (contentLoader == null)
				throw new ArgumentNullException("contentLoader");

			_contentLoader = contentLoader;
		}

		/// <summary>
		/// This will never return null. It will throw an exception for a null or blank relativePath.
		/// </summary>
		public TextFileContents Load(string relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				throw new ArgumentException("Null/blank relativePath specified");

			var content = _contentLoader.Load(relativePath);
			return new TextFileContents(
				content.RelativePath,
				content.LastModified,
				MinifyCSS(content.Content)
			);
		}

		/// <summary>
		/// Simple method to minify CSS content using a few regular expressions. This will throw an exception for null input.
		/// </summary>
		private string MinifyCSS(string content)
		{
			if (content == null)
				throw new ArgumentNullException("content");

			content = content.Trim();
			if (content == "")
				return "";

			content = CommentRemover.Replace(content += "/**/", ""); // Ensure that any unclosed comments are handled
			content = HashSurroundingWhitespaceRemover.Replace(content, "#");
			content = ExtraneousWhitespaceRemover.Replace(content, "");
			content = DuplicateWhitespaceRemover.Replace(content, " ");
			content = DelimiterWhitespaceRemover.Replace(content, "$1");
			content = content.Replace(";}", "}");
			content = UnitWhitespaceRemover.Replace(content, "$1");
			return content;
		}

		// Courtesy of http://madskristensen.net/post/Efficient-stylesheet-minification-in-C.aspx
		private static readonly Regex HashSurroundingWhitespaceRemover = new Regex(@"[a-zA-Z]+#", RegexOptions.Compiled);
		private static readonly Regex ExtraneousWhitespaceRemover = new Regex(@"[\n\r]+\s*", RegexOptions.Compiled);
		private static readonly Regex DuplicateWhitespaceRemover = new Regex(@"\s+", RegexOptions.Compiled);
		private static readonly Regex DelimiterWhitespaceRemover = new Regex(@"\s?([:,;{}])\s?", RegexOptions.Compiled);
		private static readonly Regex UnitWhitespaceRemover = new Regex(@"([\s:]0)(px|pt|%|em)", RegexOptions.Compiled);
		private static readonly Regex CommentRemover = new Regex(@"/\*[\d\D]*?\*/", RegexOptions.Compiled);
	}
}
