using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Infrastructure;

namespace RaceDay.Web.Controllers;

[Route("my-enrolments")]
[SessionAuthorize(Role = Roles.Participant)]
public class EnrolmentsController : Controller
{
    private readonly IRaceDayApi _api;
    public EnrolmentsController(IRaceDayApi api) => _api = api;

    /// <summary>A participant's own enrolments and their status.</summary>
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var list = await _api.GetMyEnrolmentsAsync();
        return View(list.OrderBy(e => e.EventDate).ToList());
    }

    [HttpPost("enrol")]
    public async Task<IActionResult> Enrol(int eventId, int? categoryId)
    {
        if (categoryId is null)
        {
            TempData["Error"] = "Choose a category before enrolling.";
            return RedirectToAction("Details", "Events", new { id = eventId });
        }

        try
        {
            var enrolment = await _api.EnrolAsync(categoryId.Value);
            TempData["Success"] = $"You're enrolled in {enrolment.EventName} ({enrolment.CategoryName}). " +
                                  "Your enrolment is pending until the organiser confirms it.";
            return RedirectToAction(nameof(Index));
        }
        catch (ApiException ex) when (ex.StatusCode is 400 or 404 or 409)
        {
            TempData["Error"] = ex.StatusCode == 404 ? "That category no longer exists." : ex.Message;
            return RedirectToAction("Details", "Events", new { id = eventId });
        }
    }

    [HttpPost("{id:int}/withdraw")]
    public async Task<IActionResult> Withdraw(int id)
    {
        try
        {
            await _api.WithdrawAsync(id);
            TempData["Success"] = "You've withdrawn from the event.";
        }
        catch (ApiException ex) when (ex.StatusCode is 403 or 404)
        {
            TempData["Error"] = "We couldn't find that enrolment.";
        }
        return RedirectToAction(nameof(Index));
    }
}
