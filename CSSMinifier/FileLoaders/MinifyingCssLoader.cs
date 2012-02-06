using System;
using System.IO;
using System.Text.RegularExpressions;

namespace CSSMinifier.FileLoaders
{
	public class MinifyingCssLoader : ITextFileLoader
	{
		private ITextFileLoader _contentLoader;
		private ICache _cache;
		public MinifyingCssLoader(ITextFileLoader contentLoader, ICache cache)
		{
			if (contentLoader == null)
				throw new ArgumentNullException("contentLoader");
			if (cache == null)
				throw new ArgumentNullException("cache");

			_contentLoader = contentLoader;
			 _cache = cache;
		}

		/// <summary>
		/// This will never return null. It will throw an exception for a null or blank relativePath.
		/// </summary>
		public TextFileContents Load(string relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				throw new ArgumentException("Null/blank relativePath specified");

			// Request the unprocessed content
			var unprocessedContent = _contentLoader.Load(relativePath);

			// Try to retrieve cached data
			var cacheKey = String.Format("{0}-{1}", this.GetType(), relativePath);
			var cachedData = _cache[cacheKey] as TextFileContents;
			if (cachedData != null)
			{
				// If the cached data is up-to-date then use it..
				if (cachedData.LastModified >= unprocessedContent.LastModified)
					return cachedData;

				// .. otherwise remove it from cache so it can be replaced with current data below
				_cache.Remove(cacheKey);
			}

			// Do the work and cache the result
			var processedContent = new TextFileContents(
				unprocessedContent.Filename,
				unprocessedContent.LastModified,
				MinifyCSS(unprocessedContent.Content)
			);
			_cache.Add(cacheKey, processedContent);
			return processedContent;
		}

		/// <summary>
		/// Simple method to minify CSS content using a few regular expressions
		/// </summary>
		private string MinifyCSS(string content)
		{
			if (content == null)
				throw new ArgumentNullException("content");

			content = content.Trim();
			if (content == "")
				return "";

			content = HashSurroundingWhitespaceRemover.Replace(content, "#");
			content = ExtraneousWhitespaceRemover.Replace(content, "");
			content = DuplicateWhitespaceRemover.Replace(content, " ");
			content = DelimiterWhitespaceRemover.Replace(content, "$1");
			content = content.Replace(";}", "}");
			content = UnitWhitespaceRemover.Replace(content, "$1");
			return CommentRemover.Replace(content, "");
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
