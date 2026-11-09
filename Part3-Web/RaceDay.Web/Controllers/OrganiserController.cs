using Microsoft.AspNetCore.Mvc;
using RaceDay.Web.Infrastructure;
using RaceDay.Web.Models;

namespace RaceDay.Web.Controllers;

/// <summary>Everything an Organiser can do: dashboard, events, categories, enrolments and results.</summary>
[Route("organiser")]
[SessionAuthorize(Role = Roles.Organiser)]
public class OrganiserController : Controller
{
    private const long MaxBannerBytes = 5 * 1024 * 1024;

    private readonly IRaceDayApi _api;
    public OrganiserController(IRaceDayApi api) => _api = api;

    // ================================================================= dashboard

    [HttpGet("")]
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard() =>
        View(new DashboardViewModel { Events = await _api.GetMyEventsAsync() });

    // ================================================================= events

    [HttpGet("events")]
    public async Task<IActionResult> Events() => View(await _api.GetMyEventsAsync());

    [HttpGet("events/create")]
    public IActionResult CreateEvent() =>
        View("EventForm", new EventFormViewModel { EventDate = DateTime.Today.AddDays(30).AddHours(7) });

    [HttpPost("events/create")]
    public async Task<IActionResult> CreateEvent(EventFormViewModel model)
    {
        ValidateBanner(model);
        if (!ModelState.IsValid) return View("EventForm", model);

        try
        {
            var created = await _api.CreateEventAsync(ToRequest(model));

            if (model.BannerFile is { Length: > 0 })
            {
                try { await _api.UploadBannerAsync(created.EventId, model.BannerFile); }
                catch (ApiException ex)
                {
                    TempData["Warning"] = $"Your event was created, but the banner image wasn't saved: {BannerMessage(ex)}";
                    return RedirectToAction(nameof(EditEvent), new { id = created.EventId });
                }
            }

            TempData["Success"] = $"{created.Name} has been created. Add categories so participants can enrol.";
            return RedirectToAction(nameof(Categories), new { id = created.EventId });
        }
        catch (ApiException ex) when (ex.StatusCode is 400 or 409)
        {
            ModelState.AddApiErrors(ex);
            return View("EventForm", model);
        }
    }

    [HttpGet("events/{id:int}/edit")]
    public async Task<IActionResult> EditEvent(int id)
    {
        var ev = await _api.GetEventAsync(id);
        return View("EventForm", new EventFormViewModel
        {
            EventId = ev.EventId, Name = ev.Name, Description = ev.Description, EventDate = ev.EventDate,
            Location = ev.Location, Distance = ev.Distance, EventType = ev.EventType, HasBanner = ev.HasBanner
        });
    }

    [HttpPost("events/{id:int}/edit")]
    public async Task<IActionResult> EditEvent(int id, EventFormViewModel model)
    {
        model.EventId = id;
        ValidateBanner(model);
        if (!ModelState.IsValid) return View("EventForm", model);

        try
        {
            await _api.UpdateEventAsync(id, ToRequest(model));

            if (model.RemoveBanner && model.BannerFile is null)
                await _api.DeleteBannerAsync(id);

            if (model.BannerFile is { Length: > 0 })
            {
                try { await _api.UploadBannerAsync(id, model.BannerFile); }
                catch (ApiException ex)
                {
                    TempData["Warning"] = $"Your changes were saved, but the banner image wasn't: {BannerMessage(ex)}";
                    return RedirectToAction(nameof(EditEvent), new { id });
                }
            }

            TempData["Success"] = "Event updated.";
            return RedirectToAction(nameof(Events));
        }
        catch (ApiException ex) when (ex.StatusCode is 400 or 409)
        {
            ModelState.AddApiErrors(ex);
            return View("EventForm", model);
        }
    }

    [HttpPost("events/{id:int}/delete")]
    public async Task<IActionResult> DeleteEvent(int id)
    {
        await _api.DeleteEventAsync(id);
        TempData["Success"] = "Event deleted, along with its categories, enrolments and results.";
        return RedirectToAction(nameof(Events));
    }

    // ================================================================= categories

    [HttpGet("events/{id:int}/categories")]
    public async Task<IActionResult> Categories(int id, int? edit = null)
    {
        var ev = await _api.GetEventAsync(id);
        var form = new CategoryFormViewModel { EventId = id, CategoryDistance = ev.Distance };

        if (edit is not null && ev.Categories.FirstOrDefault(c => c.CategoryId == edit) is { } c)
            form = new CategoryFormViewModel
            {
                EventId = id, CategoryId = c.CategoryId, Name = c.Name,
                MinAge = c.MinAge, MaxAge = c.MaxAge, CategoryDistance = c.CategoryDistance
            };

        return View(new CategoriesPageViewModel { Event = ev, Form = form });
    }

    [HttpPost("events/{id:int}/categories")]
    public async Task<IActionResult> SaveCategory(int id, CategoryFormViewModel form)
    {
        form.EventId = id;
        if (form.MinAge.HasValue && form.MaxAge.HasValue && form.MinAge > form.MaxAge)
            ModelState.AddModelError("Form.MinAge", "Minimum age can't be higher than maximum age.");

        if (ModelState.IsValid)
        {
            var request = new
            {
                name = form.Name.Trim(), minAge = form.MinAge, maxAge = form.MaxAge,
                categoryDistance = form.CategoryDistance
            };
            try
            {
                if (form.CategoryId is { } categoryId) await _api.UpdateCategoryAsync(categoryId, request);
                else await _api.CreateCategoryAsync(id, request);

                TempData["Success"] = form.CategoryId.HasValue ? "Category updated." : "Category added.";
                return RedirectToAction(nameof(Categories), new { id });
            }
            catch (ApiException ex) when (ex.StatusCode is 400 or 409)
            {
                ModelState.AddApiErrors(ex);
            }
        }

        return View("Categories", new CategoriesPageViewModel { Event = await _api.GetEventAsync(id), Form = form });
    }

    [HttpPost("events/{id:int}/categories/{categoryId:int}/delete")]
    public async Task<IActionResult> DeleteCategory(int id, int categoryId)
    {
        await _api.DeleteCategoryAsync(categoryId);
        TempData["Success"] = "Category removed.";
        return RedirectToAction(nameof(Categories), new { id });
    }

    // ================================================================= enrolments

    [HttpGet("events/{id:int}/enrolments")]
    public async Task<IActionResult> Enrolments(int id, string? status = null, int? categoryId = null)
    {
        var ev = await _api.GetEventAsync(id);
        var all = await _api.GetEventEnrolmentsAsync(id);

        var shown = all.AsEnumerable();
        if (!string.IsNullOrEmpty(status)) shown = shown.Where(e => e.Status == status);
        if (categoryId.HasValue) shown = shown.Where(e => e.CategoryId == categoryId);

        return View(new EventEnrolmentsViewModel
        {
            Event = ev, AllEnrolments = all, Enrolments = shown.ToList(), Status = status, CategoryId = categoryId
        });
    }

    [HttpPost("events/{id:int}/enrolments/{enrolmentId:int}/status")]
    public async Task<IActionResult> SetStatus(int id, int enrolmentId, string status)
    {
        if (status is not ("Pending" or "Confirmed" or "Cancelled"))
        {
            TempData["Error"] = "That isn't a valid status.";
            return RedirectToAction(nameof(Enrolments), new { id });
        }

        await _api.SetEnrolmentStatusAsync(enrolmentId, status);
        TempData["Success"] = $"Enrolment marked as {status.ToLowerInvariant()}.";
        return RedirectToAction(nameof(Enrolments), new { id });
    }

    // ================================================================= results

    [HttpGet("events/{id:int}/results")]
    public async Task<IActionResult> Results(int id)
    {
        var ev = await _api.GetEventAsync(id);
        var enrolments = (await _api.GetEventEnrolmentsAsync(id)).Where(e => e.Status != "Cancelled").ToList();
        return View(new ResultsEntryViewModel { Event = ev, Enrolments = enrolments });
    }

    [HttpPost("events/{id:int}/results")]
    public async Task<IActionResult> SaveResult(int id, ResultInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).First(m => m.Length > 0);
            return RedirectToAction(nameof(Results), new { id });
        }

        if (!TimeSpan.TryParse(input.FinishTime, System.Globalization.CultureInfo.InvariantCulture, out var time) ||
            time >= TimeSpan.FromHours(24))
        {
            TempData["Error"] = "Enter the finish time as hh:mm:ss, for example 00:52:31.";
            return RedirectToAction(nameof(Results), new { id });
        }

        try
        {
            await _api.RecordResultAsync(input.EnrolmentId, time, input.FinishPosition, input.IsUpdate);
            TempData["Success"] = input.IsUpdate ? "Result updated." : "Result published. The participant can see it now.";
        }
        catch (ApiException ex) when (ex.StatusCode is 400 or 409)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Results), new { id });
    }

    // ================================================================= helpers

    private static object ToRequest(EventFormViewModel m) => new
    {
        name = m.Name.Trim(),
        description = string.IsNullOrWhiteSpace(m.Description) ? null : m.Description.Trim(),
        eventDate = m.EventDate,
        location = m.Location.Trim(),
        distance = m.Distance,
        eventType = m.EventType
    };

    /// <summary>Friendly pre-check so a huge or wrong file gets a clear message. The API enforces the same rules.</summary>
    private void ValidateBanner(EventFormViewModel model)
    {
        if (model.BannerFile is not { Length: > 0 } file) return;

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".gif" or ".webp"))
            ModelState.AddModelError(nameof(model.BannerFile), "Choose a JPG, PNG, GIF or WebP image.");
        else if (file.Length > MaxBannerBytes)
            ModelState.AddModelError(nameof(model.BannerFile), "The image must be 5 MB or smaller.");
    }

    private static string BannerMessage(ApiException ex) =>
        ex.StatusCode is 400 ? ex.Message : "image storage is unavailable right now. Edit the event to try again.";
}
