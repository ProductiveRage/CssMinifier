using System;

namespace CSSMinifier.FileLoaders
{
	/// <summary>
	/// Represent a last-modified-date-marked text file we can store in cache
	/// </summary>
	[Serializable]
	public class TextFileContents
	{
		public TextFileContents(string filename, DateTime lastModified, string content)
		{
			if (string.IsNullOrWhiteSpace(filename))
				throw new ArgumentException("Null/blank filename specified");
			if (content == null)
				throw new ArgumentNullException("content");

			Filename = filename.Trim();
			LastModified = lastModified;
			Content = content.Trim();
		}

		/// <summary>
		/// This will never be null or empty
		/// </summary>
		public string Filename { get; private set; }

		public DateTime LastModified { get; private set; }

		/// <summary>
		/// This will never be null but it may be empty if the source file had no content
		/// </summary>
		public string Content { get; private set; }
	}
}
