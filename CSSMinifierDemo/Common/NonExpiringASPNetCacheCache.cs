using System;
using System.Web.Caching;
using CSSMinifier.Caching;
using CSSMinifier.FileLoaders;

namespace CSSMinifierDemo.Common
{
	public class NonExpiringASPNetCacheCache : ICacheThingsWithModifiedDates<TextFileContents>
	{
		private Cache _cache;
		public NonExpiringASPNetCacheCache(Cache cache)
		{
			if (cache == null)
				throw new ArgumentNullException("cache");

			_cache = cache;
		}

		/// <summary>
		/// This will return null if the entry is not present in the cache. It will throw an exception for null or blank cacheKey. If data was found in the cache for the
		/// specified cache key that was not of type T then null will be returned, but whether the invalid entry is removed from the cache depends upon the implementation.
		/// </summary>
		public TextFileContents this[string cacheKey]
		{
			get
			{
				if (string.IsNullOrWhiteSpace(cacheKey))
					throw new ArgumentException("Null/blank cacheKeys specified");

				var cachedData = _cache[cacheKey];
				if (cachedData == null)
					return null;

				var cachedTextFileContentsData = cachedData as TextFileContents;
				if (cachedTextFileContentsData != null)
					return cachedTextFileContentsData;
				
				// If something's inserted invalid data into the cache then remove it, since whatever's call this getter will probably want to insert its own data
				// after it does the work to generate it (and the Add method won't overwrite data already in the cache)
				Remove(cacheKey);
				return null;
			}
		}

		/// <summary>
		/// The caching mechanism (eg. cache duration) is determine by the ICache implementation. This will throw an exception for null or blank cacheKey or null value.
		/// </summary>
		public void Add(string cacheKey, TextFileContents value)
		{
			if (string.IsNullOrWhiteSpace(cacheKey))
				throw new ArgumentException("Null/blank cacheKeys specified");
			if (value == null)
				throw new ArgumentNullException("value");

			// Since the CSSController will push cached data in with a LastModifiedDate and then replace those cache items (with a Remove followed by Add) then we can
			// use DateTime.MaxValue for AbsoluteExpiration and effectivel disable time-based expiration
			// file's LastModifiedDate we don't want this cache entry to expire
			// on a separate time based scheme)
			_cache.Add(
				cacheKey,
				value,
				null,
				DateTime.MaxValue,
				Cache.NoSlidingExpiration,
				CacheItemPriority.Normal,
				null
			);
		}

		/// <summary>
		/// This will do nothing if there is no entry for the specified cacheKey. It will throw an exception for null or blank cacheKey.
		/// </summary>
		public void Remove(string cacheKey)
		{
			if (string.IsNullOrWhiteSpace(cacheKey))
				throw new ArgumentException("Null/blank cacheKeys specified");

			_cache.Remove(cacheKey);
		}
	}
}
