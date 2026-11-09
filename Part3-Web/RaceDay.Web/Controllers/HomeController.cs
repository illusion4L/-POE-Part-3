using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Models;

namespace RaceDay.Web.Controllers;

public class HomeController : Controller
{
    private readonly IRaceDayApi _api;
    public HomeController(IRaceDayApi api) => _api = api;

    /// <summary>Upcoming events with filters. Organisers land on their dashboard instead.</summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? location, string? eventType, DateTime? date)
    {
        if (HttpContext.Session.GetUser() is { IsOrganiser: true })
            return RedirectToAction("Dashboard", "Organiser");

        if (eventType is not (null or "" or "Run" or "Walk" or "Cycle")) eventType = null;

        var all = await _api.GetEventsAsync(date, location, eventType);
        var model = new EventListViewModel
        {
            Location = location,
            EventType = eventType,
            Date = date,
            Events = all.Where(e => e.EventDate.Date >= DateTime.Today).OrderBy(e => e.EventDate).ToList()
        };
        return View(model);
    }

    // Re-executed by the status-code middleware for failed requests of ANY method (e.g. a rejected POST)
    [AcceptVerbs("GET", "POST", "PUT", "DELETE")]
    [IgnoreAntiforgeryToken]
    public IActionResult Status(int code)
    {
        var (title, message) = code switch
        {
            404 => ("Page not found", "We couldn't find that page. It may have moved, or the event may have been removed."),
            400 => ("That request didn't work", "Something about that request wasn't right. Go back and try again."),
            403 => ("Access denied", "You don't have permission to view that page."),
            503 => ("RaceDay is unavailable", "We can't reach the RaceDay service right now. Please try again in a moment."),
            _ => ("Something went wrong", "We hit a problem handling that. Please try again.")
        };
        Response.StatusCode = code is >= 400 and < 600 ? code : 500;
        return View("Status", new StatusPageViewModel { Code = code, Title = title, Message = message });
    }

    [AcceptVerbs("GET", "POST", "PUT", "DELETE")]
    [IgnoreAntiforgeryToken]
    public IActionResult Error()
    {
        Response.StatusCode = 500;
        return View("Status", new StatusPageViewModel
        {
            Code = 500,
            Title = "Something went wrong",
            Message = "We hit a problem handling that. Please try again, and if it keeps happening let the organiser know."
        });
    }
}
