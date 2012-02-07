using System;
using CSSMinifier.FileLoaders.LastModifiedDateRetrievers;

namespace CSSMinifier.FileLoaders
{
	public class CachingTextFileLoader : ITextFileLoader
	{
		private ITextFileLoader _contentLoader;
		private ILastModifiedDateRetriever _lastModifiedDateRetriever;
		private ICache _cache;
		public CachingTextFileLoader(ITextFileLoader contentLoader, ILastModifiedDateRetriever lastModifiedDateRetriever, ICache cache)
		{
			if (contentLoader == null)
				throw new ArgumentNullException("contentLoader");
			if (lastModifiedDateRetriever == null)
				throw new ArgumentNullException("lastModifiedDateRetriever");
			if (cache == null)
				throw new ArgumentNullException("cache");

			_contentLoader = contentLoader;
			_lastModifiedDateRetriever = lastModifiedDateRetriever;
			_cache = cache;
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

			// Try to retrieve cached data
			var cacheKey = "CachingTextFileLoader-" + relativePath;
			var cachedData = _cache[cacheKey] as TextFileContents;
			if (cachedData != null)
			{
				// If the cached data is up-to-date then use it..
				var lastModifiedDate = _lastModifiedDateRetriever.GetLastModifiedDate(relativePath);
				if (cachedData.LastModified >= lastModifiedDate)
					return cachedData;

				// .. otherwise remove it from cache so it can be replaced with current data below
				_cache.Remove(cacheKey);
			}

			// Do the work and cache the result
			var content = _contentLoader.Load(relativePath);
			_cache.Add(cacheKey, content);
			return content;
		}
	}
}
