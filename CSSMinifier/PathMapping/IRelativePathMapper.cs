namespace CSSMinifier.PathMapping
{
	public interface IRelativePathMapper
	{
		/// <summary>
		/// This will throw an exception for null or empty input, it will never return null
		/// </summary>
		string MapPath(string relativePath);
	}
}
