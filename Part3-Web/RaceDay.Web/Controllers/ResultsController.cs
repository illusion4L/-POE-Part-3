using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Infrastructure;

namespace RaceDay.Web.Controllers;

[Route("my-results")]
[SessionAuthorize(Role = Roles.Participant)]
public class ResultsController : Controller
{
    private readonly IRaceDayApi _api;
    public ResultsController(IRaceDayApi api) => _api = api;

    /// <summary>The participant's race history across all completed events.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var results = await _api.GetMyResultsAsync();
        return View(results.OrderByDescending(r => r.EventDate).ToList());
    }
}
