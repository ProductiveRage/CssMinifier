namespace CSSMinifier
{
	public interface ICache
	{
		/// <summary>
		/// This will return null if the entry is not present in the cache. It will throw an exception for null or blank cacheKey.
		/// </summary>
		object this[string cacheKey] { get; }

		/// <summary>
		/// The caching mechanism (eg. cache duration) is determine by the ICache implementation. This will throw an exception for null or blank cacheKey or null value.
		/// </summary>
		void Add(string cacheKey, object value);

		/// <summary>
		/// This will do nothing if there is no entry for the specified cacheKey. It will throw an exception for null or blank cacheKey.
		/// </summary>
		void Remove(string cacheKey);
	}
}
