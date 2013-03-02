namespace CSSMinifier.FileLoaders.Factories
{
	public interface IGenerateCssLoaders
	{
		/// <summary>
		/// This will never return null, it will raise an exception if unable to satisfy the request
		/// </summary>
		ITextFileLoader Get();
	}
}
