using Microsoft.AspNetCore.Mvc;

namespace CarCareTracker.Controllers
{
    public class ErrorController : Controller
    {
        [Route("Error/{statusCode?}")]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Index(int? statusCode)
        {
            var cleanedStatusCode = statusCode ?? 500;
            Response.StatusCode = cleanedStatusCode;
            if (User.IsInRole("APIAuth"))
            {
                return new EmptyResult();
            }
            switch (cleanedStatusCode)
            {
                case 401:
                    return View("401");
                case 403:
                    return View("403");
                case 404:
                    return View("404");
                default:
                    return View("500");
            }
        }
    }
}
