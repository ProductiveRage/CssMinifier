namespace CSSMinifier.Caching
{
	public interface ICacheThingsWithModifiedDates<T> where T : class, IKnowWhenIWasLastModified // must be a class so that we can return null if not found
	{
		/// <summary>
		/// This will return null if the entry is not present in the cache. It will throw an exception for null or blank cacheKey. If data was found in the cache for the
		/// specified cache key that was not of type T then null will be returned, but whether the invalid entry is removed from the cache depends upon the implementation.
		/// </summary>
		T this[string cacheKey] { get; }

		/// <summary>
		/// The caching mechanism (eg. cache duration) is determined by the ICacheThingsWithModifiedDates implementation. This will throw an exception for null or
		/// blank cacheKey or null value.
		/// </summary>
		void Add(string cacheKey, T value);

		/// <summary>
		/// This will do nothing if there is no entry for the specified cacheKey. It will throw an exception for null or blank cacheKey.
		/// </summary>
		void Remove(string cacheKey);
	}
}
