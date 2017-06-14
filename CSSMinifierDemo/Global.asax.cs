using System.Web.Mvc;
using System.Web.Routing;

namespace CSSMinifierDemo
{
	public class MvcApplication : System.Web.HttpApplication
	{
		protected void Application_Start()
		{
			AreaRegistration.RegisterAllAreas();
			GlobalFilters.Filters.Add(new HandleErrorAttribute());
			RouteTable.Routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
			RouteTable.Routes.RouteExistingFiles = true;
			RouteTable.Routes.MapRoute(
				"Stylesheets",
				"{*allwithextension}",
				new { controller = "CSS", action = "Process" },
				new { allwithextension = @".*\.(css|less)(/.*)?" }
			);
			RouteTable.Routes.MapRoute(
				name: "Default",
				url: "{controller}/{action}/{id}",
				defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
			);
		}
	}
}
