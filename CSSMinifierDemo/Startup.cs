using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(CSSMinifierDemo.Startup))]
namespace CSSMinifierDemo
{
	public partial class Startup
    {
        public void Configuration(IAppBuilder app) { }
    }
}
