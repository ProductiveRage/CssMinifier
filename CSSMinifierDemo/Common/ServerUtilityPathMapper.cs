using System;
using System.Web;
using CSSMinifier.PathMapping;

namespace CSSMinifierDemo.Common
{
	/// <summary>
	/// This will throw an exception for null or empty input, it will never return null
	/// </summary>
	public class ServerUtilityPathMapper : IRelativePathMapper
	{
		private HttpServerUtilityBase _server;
		public ServerUtilityPathMapper(HttpServerUtilityBase server)
		{
			if (server == null)
				throw new ArgumentNullException("server");

			_server = server;
		}

		public string MapPath(string relativePath)
		{
			if (string.IsNullOrWhiteSpace(relativePath))
				throw new ArgumentException("Null/blank relativePath specified");

			return _server.MapPath(relativePath);
		}
	}
}
