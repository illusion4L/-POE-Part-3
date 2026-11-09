using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace RaceDay.Web.Infrastructure;

/// <summary>
/// Turns API failures that a controller did not handle itself into something sensible for the user:
/// expired session -> login, forbidden -> access denied, missing -> 404 page, outage -> friendly error page.
/// </summary>
public class ApiExceptionFilter : IExceptionFilter
{
    private readonly ITempDataDictionaryFactory _tempData;
    private readonly ILogger<ApiExceptionFilter> _log;

    public ApiExceptionFilter(ITempDataDictionaryFactory tempData, ILogger<ApiExceptionFilter> log)
    {
        _tempData = tempData;
        _log = log;
    }

    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not ApiException ex) return;   // anything else -> /Home/Error

        var http = context.HttpContext;
        var temp = _tempData.GetTempData(http);

        switch (ex.StatusCode)
        {
            case 401:
                http.Session.Clear();     // the API no longer recognises this session
                temp["Warning"] = "Your session has expired. Please log in again.";
                var returnUrl = http.Request.Method == HttpMethods.Get ? http.Request.Path + http.Request.QueryString : null;
                context.Result = new RedirectToActionResult("Login", "Account", new { returnUrl });
                break;

            case 403:
                context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
                break;

            case 404:
                context.Result = new StatusCodeResult(404);     // rendered by /Home/Status
                break;

            case 400 or 409 or 422:
                temp["Error"] = ex.Message;
                context.Result = new RedirectToActionResult("Index", "Home", null);
                break;

            default:
                _log.LogError(ex, "RaceDay API call failed with status {Status}", ex.StatusCode);
                context.Result = new StatusCodeResult(503);
                break;
        }

        context.ExceptionHandled = true;
    }
}
