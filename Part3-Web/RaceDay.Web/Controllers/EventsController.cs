using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Models;

namespace RaceDay.Web.Controllers;

[Route("events")]
public class EventsController : Controller
{
    private readonly IRaceDayApi _api;
    public EventsController(IRaceDayApi api) => _api = api;

    /// <summary>Full event information and the categories a participant can enrol in. Public.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var model = new EventDetailsViewModel { Event = await _api.GetEventAsync(id) };

        if (HttpContext.Session.GetUser() is { IsParticipant: true })
        {
            var mine = await _api.GetMyEnrolmentsAsync();
            model.MyEnrolment = mine.FirstOrDefault(e => e.EventId == id);
        }
        return View(model);
    }

    /// <summary>
    /// Serves the banner through the MVC app so the browser never talks to the API or Azure directly.
    /// The bytes come from Azure Blob Storage via the API.
    /// </summary>
    [HttpGet("{id:int}/banner")]
    public async Task<IActionResult> Banner(int id)
    {
        var image = await _api.GetBannerAsync(id);
        if (image is null) return NotFound();

        Response.Headers.CacheControl = "private, max-age=30";
        return File(image.Bytes, image.ContentType);
    }
}
