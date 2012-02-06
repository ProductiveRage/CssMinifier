using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using CSSMinifier.FileLoaders;
using CSSMinifierDemo.Common;

namespace CSSMinifierDemo.Controllers
{
	public class CSSController : Controller
	{
		public ActionResult Process()
		{
			var relativePathMapper = new ServerUtilityPathMapper(Server);
			var relativePath = Request.FilePath;
			var fullPath = relativePathMapper.MapPath(relativePath);
			var file = new FileInfo(fullPath);
			if (!file.Exists)
			{
				Response.StatusCode = 404;
				Response.StatusDescription = "Not Found";
				return Content("File not found: " + relativePath, "text/css");
			}

			var lastModifiedDateFromRequest = TryToGetIfModifiedSinceDateFromRequest();
			if ((lastModifiedDateFromRequest != null) && AreDatesApproximatelyEqual(lastModifiedDateFromRequest.Value, file.LastWriteTime))
			{
				Response.StatusCode = 304;
				Response.StatusDescription = "Not Modified";
				return Content("", "text/css");
			}

			try
			{
				var loader = new MinifyingCssLoader(
					new SimpleTextFileContentLoader(relativePathMapper),
					new NonExpiringASPNetCacheCache(HttpContext.Cache)
				);
				var content = loader.Load(relativePath);
				if (content == null)
					throw new Exception("Received null response from Css Loader - this should not happen");
				if ((lastModifiedDateFromRequest != null) && AreDatesApproximatelyEqual(lastModifiedDateFromRequest.Value, file.LastWriteTime))
				{
					Response.StatusCode = 304;
					Response.StatusDescription = "Not Modified";
					return Content("", "text/css");
				}
				SetResponseCacheHeadersForSuccess(content.LastModified);
				return Content(content.Content, "text/css");
			}
			catch (Exception e)
			{
				Response.StatusCode = 500;
				Response.StatusDescription = "Internal Server Error";
#if DEBUG
				return Content("Error: " + e.StackTrace);
#else
				return Content("Error: " + e.Message);
#endif
			}
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
