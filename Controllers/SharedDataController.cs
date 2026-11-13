using CarCareTracker.Helper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CarCareTracker.Controllers
{
    public class SharedDataController : Controller
    {
        private readonly IFileHelper _fileHelper;
        private readonly IConfigHelper _config;
        
        public SharedDataController(IFileHelper fileHelper, IConfigHelper config)
        {
            _fileHelper = fileHelper;
            _config = config;
        }
        [AllowAnonymous]
        [Route("/css/theme.css")]
        public async Task<IActionResult> GetConfiguredTheme()
        {
            string uiTheme = string.Empty;
            string themeContent = string.Empty;
            try
            {
                var userConfig = _config.GetUserConfig(User);
                if (string.IsNullOrWhiteSpace(userConfig.UserTheme))
                {
                    uiTheme = _config.GetServerTheme();
                } else
                {
                    uiTheme = userConfig.UserTheme;
                }
            }
            catch
            {
                uiTheme = _config.GetServerTheme();
            }
            if (!string.IsNullOrWhiteSpace(uiTheme))
            {
                themeContent = await _fileHelper.GetTheme(uiTheme);
            }
            return Content(themeContent, "text/css");
        }
    }
}
