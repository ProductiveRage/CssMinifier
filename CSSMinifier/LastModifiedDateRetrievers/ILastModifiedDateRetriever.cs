using System;

namespace CSSMinifier.FileLoaders.LastModifiedDateRetrievers
{
	public interface ILastModifiedDateRetriever
	{
		/// <summary>
		/// This will raise an exception if unable to determine the last modified date or if a null or empty relativePath is specified
		/// </summary>
		DateTime GetLastModifiedDate(string relativePath);
	}
}
