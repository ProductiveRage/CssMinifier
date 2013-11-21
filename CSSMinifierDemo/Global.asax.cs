using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace CSSMinifier
{
	// Note: For instructions on enabling IIS6 or IIS7 classic mode, 
	// visit http://go.microsoft.com/?LinkId=9394801

	public class MvcApplication : System.Web.HttpApplication
	{
		public static void RegisterGlobalFilters(GlobalFilterCollection filters)
		{
			filters.Add(new HandleErrorAttribute());
		}

		public static void RegisterRoutes(RouteCollection routes)
		{
			routes.RouteExistingFiles = true; // Have to set this to true so that stylesheets get processed rather than returned direct

			routes.MapRoute(
				"Stylesheets",
				"{*allwithextension}",
				new { controller = "CSS", action = "Process" },
				new { allwithextension = @".*\.(css|less)(/.*)?" }
			);

			routes.MapRoute(
				"HomePage",
				"",
				new { controller = "Home", action = "Index" }
			);
		}

		protected void Application_Start()
		{
			AreaRegistration.RegisterAllAreas();

			RegisterGlobalFilters(GlobalFilters.Filters);
			RegisterRoutes(RouteTable.Routes);
		}
	}
}