namespace CSSMinifier.FileLoaders
{
	public interface ITextFileLoader
	{
		/// <summary>
		/// This will never return null, it will throw an exception for a null or empty relativePath - it is up to the particular implementation whether or not to throw
		/// an exception for invalid / inaccessible filenames (if no exception is thrown, the issue should be logged). It is up the the implementation to handle mapping
		/// the relative path to a full file path.
		/// </summary>
		TextFileContents Load(string relativePath);
	}
}
