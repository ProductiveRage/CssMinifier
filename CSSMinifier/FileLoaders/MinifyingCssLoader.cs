using System;
using System.IO;
using System.Text.RegularExpressions;

namespace CSSMinifier.FileLoaders
{
	public class MinifyingCssLoader
	{
		private ICache _cache;
		public MinifyingCssLoader(ICache cache)
		{
			if (cache == null)
				throw new ArgumentNullException("cache");

			_cache = cache;
		}

		/// <summary>
		/// This will never return null. It will throw an exception for a null or blank filename.
		/// </summary>
		public CSSContent Load(string filename, DateTime? lastModifiedDateFromRequest)
		{
			if (string.IsNullOrWhiteSpace(filename))
				throw new ArgumentException("Null/blank filename specified");

			DateTime lastModifiedDateOfData;
			try
			{
				var file = new FileInfo(filename);
				if (!file.Exists)
					throw new FileNotFoundException("Requested file does not exist", filename);

				lastModifiedDateOfData = file.LastWriteTime;
			}
			catch (Exception e)
			{
				return new CSSContent(
					CSSContent.StatusOptions.Error,
					String.Format(
						"/* Unable to determine LastModifiedDate for file: {0} [{1}] */",
						filename,
						e.Message
					),
					DateTime.MinValue
				);
			}

			if ((lastModifiedDateFromRequest != null) && (Math.Abs(lastModifiedDateFromRequest.Value.Subtract(lastModifiedDateOfData).TotalSeconds) < 2))
			{
				// Add a small grace period to the comparison (if only because lastModifiedDateOfLiveData is granular to milliseconds while
				// lastModifiedDate only considers seconds and so will nearly always be between zero and one seconds older)
				return new CSSContent(
					CSSContent.StatusOptions.NotModified,
					"",
					lastModifiedDateOfData
				);
			}

			// Try to retrieve from cache
			var cacheKey = "CSSController-" + filename;
			var cachedData = _cache[cacheKey] as TextFileContents;
			if (cachedData != null)
			{
				// If the cached data is up-to-date then use it..
				if (cachedData.LastModified >= lastModifiedDateOfData)
				{
					return new CSSContent(
						CSSContent.StatusOptions.Success,
						cachedData.Content,
						lastModifiedDateOfData
					);
				}

				// .. otherwise remove it from cache so it can be replaced with current data below
				_cache.Remove(cacheKey);
			}

			try
			{
				var content = MinifyCSS(System.IO.File.ReadAllText(filename));
				_cache.Add(cacheKey, new TextFileContents(filename, lastModifiedDateOfData, content));
				return new CSSContent(
					CSSContent.StatusOptions.Success,
					content,
					lastModifiedDateOfData
				);
			}
			catch (Exception e)
			{
				return new CSSContent(
					CSSContent.StatusOptions.Error,
					"/* Error: " + e.Message + " */",
					DateTime.MinValue
				);
			}
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

		[Serializable]
		public class CSSContent
		{
			public CSSContent(StatusOptions status, string content, DateTime lastModified)
			{
				if (!Enum.IsDefined(typeof(StatusOptions), status))
					throw new ArgumentOutOfRangeException("status");
				if (content == null)
					throw new ArgumentNullException("content");

				Status = status;
				Content = content;
				LastModified = lastModified;
			}

			public StatusOptions Status { get; private set; }

			/// <summary>
			/// This will never be null
			/// </summary>
			public string Content { get; private set; }

			public DateTime LastModified { get; private set; }

			[Serializable]
			public enum StatusOptions
			{
				Error,
				Success,
				NotModified
			}
		}
	}
}
