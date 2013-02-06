using System;
using CSSMinifier.Caching;

namespace CSSMinifier.FileLoaders
{
	/// <summary>
	/// Represent a last-modified-date-marked text file we can store in cache
	/// </summary>
	[Serializable]
	public class TextFileContents : IKnowWhenIWasLastModified
	{
		public TextFileContents(string relativePath, DateTime lastModified, string content)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				throw new ArgumentException("Null/blank relativePath specified");
			if (content == null)
				throw new ArgumentNullException("content");

			RelativePath = relativePath.Trim();
			LastModified = lastModified;
			Content = content;
		}

		/// <summary>
		/// This will never be null or empty
		/// </summary>
		public string RelativePath { get; private set; }

		public DateTime LastModified { get; private set; }

		/// <summary>
		/// This will never be null but it may be empty if the source file had no content
		/// </summary>
		public string Content { get; private set; }
	}
}
