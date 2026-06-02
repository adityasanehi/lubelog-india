using CarCareTracker.External.Interfaces;
using CarCareTracker.Helper;
using CarCareTracker.Logic;
using CarCareTracker.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CarCareTracker.Controllers
{
    [Authorize]
    public class PushController : Controller
    {
        private readonly IPushSubscriptionDataAccess _pushSubscriptionDataAccess;
        private readonly IConfigHelper _config;
        private readonly INotificationLogic _notificationLogic;

        public PushController(IPushSubscriptionDataAccess pushSubscriptionDataAccess, IConfigHelper config, INotificationLogic notificationLogic)
        {
            _pushSubscriptionDataAccess = pushSubscriptionDataAccess;
            _config = config;
            _notificationLogic = notificationLogic;
        }

        private int GetUserID() => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "-1");

        [AllowAnonymous]
        [HttpGet]
        [Route("/api/push/vapid-public-key")]
        public IActionResult GetVapidPublicKey()
        {
            var key = _config.GetVapidPublicKey();
            if (string.IsNullOrWhiteSpace(key))
                return Json(new { enabled = false });
            return Json(new { enabled = true, publicKey = key });
        }

        [HttpPost]
        [Route("/api/push/subscribe")]
        public IActionResult Subscribe([FromBody] PushSubscriptionInput input)
        {
            if (string.IsNullOrWhiteSpace(input.Endpoint) || string.IsNullOrWhiteSpace(input.P256DH) || string.IsNullOrWhiteSpace(input.Auth))
            {
                Response.StatusCode = 400;
                return Json(OperationResponse.Failed("Invalid subscription data"));
            }
            var record = new PushSubscriptionRecord
            {
                UserId = GetUserID(),
                Endpoint = input.Endpoint,
                P256DH = input.P256DH,
                Auth = input.Auth
            };
            var result = _pushSubscriptionDataAccess.SaveSubscription(record);
            return Json(OperationResponse.Conditional(result, "Subscribed"));
        }

        [HttpDelete]
        [Route("/api/push/subscribe")]
        public IActionResult Unsubscribe([FromBody] PushSubscriptionInput input)
        {
            if (string.IsNullOrWhiteSpace(input.Endpoint))
            {
                Response.StatusCode = 400;
                return Json(OperationResponse.Failed("Endpoint required"));
            }
            var result = _pushSubscriptionDataAccess.DeleteSubscriptionByEndpoint(input.Endpoint);
            return Json(OperationResponse.Conditional(result, "Unsubscribed"));
        }

        [HttpPost]
        [Route("/api/push/test")]
        public async Task<IActionResult> TestPush()
        {
            var userId = GetUserID();
            var subs = _pushSubscriptionDataAccess.GetSubscriptionsByUserId(userId);
            if (!subs.Any())
                return Json(OperationResponse.Failed("No push subscriptions found for your account. Enable push notifications first."));
            var serverDomain = _config.GetServerDomain();
            await _notificationLogic.SendWebPushToUsers(new List<int> { userId }, "LubeLogger Test", "Push notifications are working!", serverDomain);
            return Json(OperationResponse.Succeed("Test notification sent"));
        }
    }

    public class PushSubscriptionInput
    {
        public string Endpoint { get; set; } = string.Empty;
        public string P256DH { get; set; } = string.Empty;
        public string Auth { get; set; } = string.Empty;
    }
}
