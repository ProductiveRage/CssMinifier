using System;

namespace CSSMinifier.Caching
{
	/// <summary>
	/// This can use wherever an ICache is required but where caching is not desirable
	/// </summary>
	public class NullCache<T> : ICacheThingsWithModifiedDates<T> where T : class, IKnowWhenIWasLastModified
	{
		/// <summary>
		/// This will return null if the entry is not present in the cache. It will throw an exception for null or blank cacheKey. If data was found in the cache for the
		/// specified cache key that was not of type T then null will be returned, but whether the invalid entry is removed from the cache depends upon the implementation.
		/// </summary>
		public T this[string cacheKey]
		{
			get
			{
				if (string.IsNullOrWhiteSpace(cacheKey))
					throw new ArgumentException("Null/blank cacheKey specified");

				return null;
			}
		}

		/// <summary>
		/// The caching mechanism (eg. cache duration) is determine by the ICache implementation. This will throw an exception for null or blank cacheKey or null value.
		/// </summary>
		public void Add(string cacheKey, T value)
		{
			if (string.IsNullOrWhiteSpace(cacheKey))
				throw new ArgumentException("Null/blank cacheKey specified");
			if (value == null)
				throw new ArgumentNullException("value");
		}

		/// <summary>
		/// This will do nothing if there is no entry for the specified cacheKey. It will throw an exception for null or blank cacheKey.
		/// </summary>
		public void Remove(string cacheKey)
		{
			if (string.IsNullOrWhiteSpace(cacheKey))
				throw new ArgumentException("Null/blank cacheKey specified");
		}
	}
}
