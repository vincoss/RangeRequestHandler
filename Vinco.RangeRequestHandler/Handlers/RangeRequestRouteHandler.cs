using System.Web.Routing;
using System.Web;


namespace Vinco.Handlers
{
    public class RangeRequestRouteHandler : IRouteHandler
    {
        public IHttpHandler GetHttpHandler(RequestContext requestContext)
        {
            return new DatabaseRangeRequestHandler();
        }
    }
}