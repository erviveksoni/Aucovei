using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Mvc;

namespace Microsoft.Azure.Devices.Applications.RemoteMonitoring.DeviceAdmin.Web.Controllers
{
    [Authorize]
    public class ClaimsController : Controller
    {
        // GET: Claims
        public ActionResult Index()
        {
            var identity = (System.Security.Claims.ClaimsIdentity)HttpContext.User.Identity;
            var userClaims = identity.Claims.ToList();
            StringBuilder builder = new StringBuilder();
            if (userClaims != null)
            {
                foreach (var role in userClaims)
                {
                    builder.AppendLine($"{role.Type} : {role.Value}");
                }
            }
            else
            {
                builder.AppendLine($"No claims found");
            }

            return Content(builder.ToString());
        }
    }
}