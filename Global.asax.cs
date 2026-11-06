using System.Web.Mvc;
using System.Web.Routing;

namespace FormsSandbox
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_BeginRequest()
        {
            // External scripts only. The nonce is not needed for this form.
            Response.Headers["Content-Security-Policy"] =
                "default-src 'self'; script-src 'self'; style-src 'self'; " +
                "script-src-attr 'none'; object-src 'none'; base-uri 'self'; form-action 'self'";
        }

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            RouteTable.Routes.IgnoreRoute("{resource}.axd/{*pathInfo}");
            RouteTable.Routes.MapRoute(
                name: "Default",
                url: "{controller}/{action}/{id}",
                defaults: new { controller = "Document", action = "Edit", id = UrlParameter.Optional });
        }
    }
}