using System;
using System.IO;
using CSSMinifier.PathMapping;

namespace CSSMinifier.FileLoaders
{
	/// <summary>
	/// This will try to load the contents of a single file from disk, with no processing. If the file is not accessible, it will throw an exception.
	/// </summary>
	public class SimpleTextFileContentLoader : ITextFileLoader
	{
		private IRelativePathMapper _relativePathMapper;
		public SimpleTextFileContentLoader(IRelativePathMapper relativePathMapper)
		{
			if (relativePathMapper == null)
				throw new ArgumentNullException("relativePathMapper");

			_relativePathMapper = relativePathMapper;
		}

		/// <summary>
		/// This will never return null, it will throw an exception for a null or empty relativePath - it is up to the particular implementation whether or not to throw
		/// an exception for invalid / inaccessible filenames (if no exception is thrown, the issue should be logged). It is up the the implementation to handle mapping
		/// the relative path to a full file path.
		/// </summary>
		public TextFileContents Load(string relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				throw new ArgumentException("Null/blank relativePath specified");

			var file = new FileInfo(_relativePathMapper.MapPath(relativePath));
			if (!file.Exists)
				throw new ArgumentException("Requested file does not exist: " + file.FullName);

			try
			{
				var fileContents = new TextFileContents(
					file.FullName,
					file.LastWriteTime,
					File.ReadAllText(file.FullName)
				);
				return fileContents;
			}
			catch (Exception e)
			{
				throw new ArgumentException("Unable to load requested file: " + file.FullName, e);
			}
		}
	}

}
