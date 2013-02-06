using System;
using System.Collections.Generic;
using System.Linq;

namespace CSSMinifier.Caching
{
	/// <summary>
	/// This can wrap multiple cache implementations so that a layered cache approach may be taken - eg. the first cache may be in-memory but shorter-term and the second cache
	/// may be persisted to disk. Get requests will be sent to each cache (from the first specified in the constructor arguments to the last), exiting when one of them can
	/// provide the data (if any). If the data is available but any caches were queried that couldn't provide it, the Add method for each of these caches will be called
	/// to "up fill" them. Add and Remove requests are sent to all of the caches with no special logic.
	/// </summary>
	public class LayeredCache<T> : ICacheThingsWithModifiedDates<T> where T : class, IKnowWhenIWasLastModified
	{
		private readonly ICacheThingsWithModifiedDates<T>[] _caches;
		public LayeredCache(IEnumerable<ICacheThingsWithModifiedDates<T>> caches)
		{
			if (caches == null)
				throw new ArgumentNullException("caches");

			var cachesTidied = caches.ToArray();
			if (cachesTidied.Any(c => c == null))
				throw new ArgumentException("Null reference encountered in caches set");

			_caches = cachesTidied;
		}

		/// <summary>
		/// This will return null if the entry is not present in the cache. It will throw an exception for null or blank cacheKey. If data was found in the cache for the
		/// specified cache key that was not of type T then null will be returned, but whether the invalid entry is removed from the cache depends upon the implementation.
		/// </summary>
		public T this[string cacheKey]
		{
			get
			{
				if (string.IsNullOrWhiteSpace(cacheKey))
					throw new ArgumentNullException("Null/blank cacheKey specified");

				if (_caches.Length == 0)
					return null;

				for (var index = 0; index < _caches.Length; index++)
				{
					var cachedData = _caches[index][cacheKey];
					if (cachedData == null)
						continue;

					for (var indexUpFill = 0; indexUpFill < index; indexUpFill++)
						_caches[indexUpFill].Add(cacheKey, cachedData);
					return cachedData;
				}
				return null;
			}
		}

		/// <summary>
		/// The caching mechanism (eg. cache duration) is determined by the ICacheThingsWithModifiedDates implementation. This will throw an exception for null or
		/// blank cacheKey or null value.
		/// </summary>
		public void Add(string cacheKey, T value)
		{
			if (string.IsNullOrWhiteSpace(cacheKey))
				throw new ArgumentNullException("Null/blank cacheKey specified");

			foreach (var cache in _caches)
				cache.Add(cacheKey, value);
		}

		/// <summary>
		/// This will do nothing if there is no entry for the specified cacheKey. It will throw an exception for null or blank cacheKey.
		/// </summary>
		public void Remove(string cacheKey)
		{
			if (string.IsNullOrWhiteSpace(cacheKey))
				throw new ArgumentNullException("Null/blank cacheKey specified");

			foreach (var cache in _caches)
				cache.Remove(cacheKey);
		}
	}
}
