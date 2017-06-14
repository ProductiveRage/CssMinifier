using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Web.Mvc;
using CSSMinifier.Caching;
using CSSMinifier.FileLoaders;
using CSSMinifier.FileLoaders.Factories;
using CSSMinifier.FileLoaders.LastModifiedDateRetrievers;
using CSSMinifier.Logging;
using CSSMinifier.PathMapping;
using CSSMinifierDemo.Common;

namespace CSSMinifierDemo.Controllers
{
	public class CSSController : Controller
	{
		public ActionResult Process()
		{
			var relativePath = Request.FilePath;
			var fullPath = Server.MapPath(relativePath);
			var file = new FileInfo(fullPath);
			if (!file.Exists)
			{
				Response.StatusCode = 404;
				Response.StatusDescription = "Not Found";
				return Content("File not found: " + relativePath, "text/css");
			}

			try
			{
				return Process(
					relativePath,
					new NonExpiringASPNetCacheCache(HttpContext.Cache),
					TryToGetIfModifiedSinceDateFromRequest()
				);
			}
			catch (Exception e)
			{
				Response.StatusCode = 500;
				Response.StatusDescription = "Internal Server Error";
				return Content("Error: " + e.Message);
			}
		}

		/// <summary>
		/// This will combine a stylesheet with all of its imports (and any imports within those, and within those, etc..) and minify the resulting content for cases only
		/// where all files are in the same folder and no relative or absolute paths are specified in the import declarations. It incorporates caching of the minified
		/// content and implements 304 responses for cases where the request came with an If-Modified-Since header indicating that current content already exists on the
		/// client. The last-modified-date for the content is determined by retrieving the most recent LastWriteTime for any file in the folder - although this may lead
		/// to some false-positives if unrelated files are updated, it does mean that if any file that IS part of the combined stylesheet is updated then the content
		/// will be identified as stale and re-generated. The cached content will likewise be invalidated and updated if any files in the folder have changed since the
		/// date recorded for the cached data. GZip and Deflate compression of the response are supported where specified in Accept-Encoding request headers.
		/// </summary>
		private ActionResult Process(
			string relativePath,
			ICacheThingsWithModifiedDates<TextFileContents> memoryCache,
			DateTime? lastModifiedDateFromRequest)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				throw new ArgumentException("Null/blank relativePath specified");
			if (memoryCache == null)
				throw new ArgumentNullException("memoryCache");

			// Using the SingleFolderLastModifiedDateRetriever means that we can determine whether cached content (either in the ASP.Net cache or in the browser cache)
			// is up to date without having to perform the complete import flattening process. It may lead to some unnecessary work if an unrelated file in the folder
			// is updated but for the most common cases it should be an efficient approach.
			var lastModifiedDateRetriever = new SingleFolderLastModifiedDateRetriever(new ServerUtilityPathMapper(Server), new[] { "css", "less" });
			var lastModifiedDate = lastModifiedDateRetriever.GetLastModifiedDate(relativePath);
			if ((lastModifiedDateFromRequest != null) && AreDatesApproximatelyEqual(lastModifiedDateFromRequest.Value, lastModifiedDate))
			{
				Response.StatusCode = 304;
				Response.StatusDescription = "Not Modified";
				return Content("", "text/css");
			}

			// In the interests of compatability with this generic example project, we'll use the DefaultNonCachedLessCssLoaderFactory which will enable LESS compilation,
			// import-flattening, minification and pseudo-source-mapping but doesn't support the advanced features that the EnhancedNonCachedLessCssLoaderFactory does
			// such as media-query-grouping and scope-restricting html-tag stripping (see comments in the EnhancedNonCachedLessCssLoaderFactory for more information)
			var cssLoader = (new DefaultNonCachedLessCssLoaderFactory(Server)).Get();
			
			// Layers of caching are added to persist the processed content in memory and on disk (last-modified-date checking is incorporated so that cache entries are
			// expired if the source files have changed since the cached data was stored)
			var diskCachingCssLoader = new DiskCachingTextFileLoader(
				cssLoader,
				relativePathRequested => new FileInfo(Server.MapPath(relativePathRequested) + ".cache"),
				lastModifiedDateRetriever,
				DiskCachingTextFileLoader.InvalidContentBehaviourOptions.Delete,
				ErrorBehaviourOptions.LogAndRaiseException,
				new NullLogger()
			);
			var memoryAndDiskCachingCssLoader = new CachingTextFileLoader(
				diskCachingCssLoader,
				lastModifiedDateRetriever,
				memoryCache
			);

			var content = memoryAndDiskCachingCssLoader.Load(relativePath);
			if (content == null)
				throw new Exception("Received null response from Css Loader - this should not happen");
			if ((lastModifiedDateFromRequest != null) && AreDatesApproximatelyEqual(lastModifiedDateFromRequest.Value, lastModifiedDate))
			{
				Response.StatusCode = 304;
				Response.StatusDescription = "Not Modified";
				return Content("", "text/css");
			}
			SetResponseCacheHeadersForSuccess(content.LastModified);
			return Content(content.Content, "text/css");
		}

		/// <summary>
		/// Try to get the If-Modified-Since HttpHeader value - if not present or not valid (ie. not interpretable as a date) then null will be returned
		/// </summary>
		private DateTime? TryToGetIfModifiedSinceDateFromRequest()
		{
			var lastModifiedDateRaw = Request.Headers["If-Modified-Since"];
			if (lastModifiedDateRaw == null)
				return null;

			DateTime lastModifiedDate;
			if (DateTime.TryParse(lastModifiedDateRaw, out lastModifiedDate))
				return lastModifiedDate;

			return null;
		}

		/// <summary>
		/// Dates from HTTP If-Modified-Since headers are only precise to whole seconds while files' LastWriteTime are granular to milliseconds, so when
		/// comparing them a small grace period is required
		/// </summary>
		private bool AreDatesApproximatelyEqual(DateTime d1, DateTime d2)
		{
			return Math.Abs(d1.Subtract(d2).TotalSeconds) < 1;
		}

		/// <summary>
		/// Mark the response as being cacheable and implement content-encoding requests such that gzip is used if supported by requester
		/// </summary>
		private void SetResponseCacheHeadersForSuccess(DateTime lastModifiedDateOfLiveData)
		{
			// Mark the response as cacheable
			// - Specify "Vary" "Content-Encoding" header to ensure that if cached by proxiesthat different versions are stored for different encodings
			//  (eg. gzip'd vs non-gzip'd)
			Response.Cache.SetCacheability(System.Web.HttpCacheability.Public);
			Response.Cache.SetLastModified(lastModifiedDateOfLiveData);
			Response.AppendHeader("Vary", "Content-Encoding");

			// Handle requested content-encoding method
			var encodingsAccepted = (Request.Headers["Accept-Encoding"] ?? "")
				.Split(',')
				.Select(e => e.Trim().ToLower())
				.ToArray();
			if (encodingsAccepted.Contains("gzip"))
			{
				Response.AppendHeader("Content-encoding", "gzip");
				Response.Filter = new GZipStream(Response.Filter, CompressionMode.Compress);
			}
			else if (encodingsAccepted.Contains("deflate"))
			{
				Response.AppendHeader("Content-encoding", "deflate");
				Response.Filter = new DeflateStream(Response.Filter, CompressionMode.Compress);
			}
		}
	}
}
