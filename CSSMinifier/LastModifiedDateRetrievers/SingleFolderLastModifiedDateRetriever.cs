using System;
using System.IO;
using System.Linq;
using CSSMinifier.PathMapping;

namespace CSSMinifier.FileLoaders.LastModifiedDateRetrievers
{
	/// <summary>
	/// This will return the most recent LastWriteTime of any file in the same folder as the specified file. If unable to access the file (including file-not-found
	/// cases), an exception will be raised when the GetLastModifiedDate method is called.
	/// </summary>
	public class SingleFolderLastModifiedDateRetriever : ILastModifiedDateRetriever
	{
		private IRelativePathMapper _relativePathMapper;
		public SingleFolderLastModifiedDateRetriever(IRelativePathMapper relativePathMapper)
		{
			if (relativePathMapper == null)
				throw new ArgumentNullException("relativePathMapper");

			_relativePathMapper = relativePathMapper;
		}

		/// <summary>
		/// This will raise an exception if unable to determine the last modified date or if a null or empty relativePath is specified
		/// </summary>
		public DateTime GetLastModifiedDate(string relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				throw new ArgumentException("Null/blank relativePath specified");

			var file = new FileInfo(
				_relativePathMapper.MapPath(relativePath)
			);
			if (!file.Exists)
				throw new ArgumentException("Invalid relativePath - file does not exist: " + relativePath);
			return file.Directory.GetFiles().Max(f => f.LastWriteTime);
		}
	}
}
