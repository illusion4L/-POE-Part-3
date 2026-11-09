using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private const long MaxBannerBytes = 5 * 1024 * 1024;   // 5 MB

    private readonly RaceDayDbContext _db;
    private readonly IBlobStorageService _blobs;
    private readonly ILogger<EventsController> _log;

    public EventsController(RaceDayDbContext db, IBlobStorageService blobs, ILogger<EventsController> log)
    {
        _db = db;
        _blobs = blobs;
        _log = log;
    }

    /// <summary>Lists all events. Optional filters: date (that day), location (contains), eventType.</summary>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<EventDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<EventDto>>> GetEvents(
        [FromQuery] DateTime? date, [FromQuery] string? location, [FromQuery] string? eventType)
    {
        var q = _db.Events.AsNoTracking().AsQueryable();

        if (date.HasValue)
        {
            var start = date.Value.Date;
            var end = start.AddDays(1);
            q = q.Where(e => e.EventDate >= start && e.EventDate < end);
        }
        if (!string.IsNullOrWhiteSpace(location))
            q = q.Where(e => e.Location.Contains(location));
        if (!string.IsNullOrWhiteSpace(eventType))
            q = q.Where(e => e.EventType == eventType);

        var events = await q.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(e => e.ToDto()));
    }

    /// <summary>Lists the logged-in Organiser's own events with category and enrolment totals.</summary>
    [HttpGet("mine")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<OrganiserEventSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<OrganiserEventSummaryDto>>> Mine()
    {
        var uid = User.GetUserId();
        var list = await _db.Events.AsNoTracking()
            .Where(e => e.OrganiserId == uid)
            .OrderBy(e => e.EventDate)
            .Select(e => new OrganiserEventSummaryDto
            {
                EventId = e.EventId, Name = e.Name, Description = e.Description, EventDate = e.EventDate,
                Location = e.Location, Distance = e.Distance, EventType = e.EventType, OrganiserId = e.OrganiserId,
                HasBanner = e.BannerBlobName != null,
                CategoryCount = e.Categories.Count,
                EnrolmentCount = e.Enrolments.Count
            })
            .ToListAsync();
        return Ok(list);
    }

    /// <summary>Returns one event with its categories.</summary>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EventDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDetailDto>> GetById(int id)
    {
        var ev = await _db.Events.AsNoTracking().Include(e => e.Categories)
            .FirstOrDefaultAsync(e => e.EventId == id);
        return ev is null ? NotFound() : Ok(ev.ToDetailDto());
    }

    /// <summary>Creates an event owned by the logged-in Organiser.</summary>
    [HttpPost]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<EventDto>> Create(EventRequest req)
    {
        var ev = new Event
        {
            Name = req.Name.Trim(),
            Description = req.Description,
            EventDate = req.EventDate!.Value,
            Location = req.Location.Trim(),
            Distance = req.Distance,
            EventType = req.EventType,
            OrganiserId = User.GetUserId()
        };
        _db.Events.Add(ev);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = ev.EventId }, ev.ToDto());
    }

    /// <summary>Updates an event. Only the owning Organiser may do this.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventDto>> Update(int id, EventRequest req)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != User.GetUserId()) return Forbid();

        ev.Name = req.Name.Trim();
        ev.Description = req.Description;
        ev.EventDate = req.EventDate!.Value;
        ev.Location = req.Location.Trim();
        ev.Distance = req.Distance;
        ev.EventType = req.EventType;
        await _db.SaveChangesAsync();
        return Ok(ev.ToDto());
    }

    /// <summary>Deletes an event, its categories, enrolments and results. Only the owning Organiser.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != User.GetUserId()) return Forbid();

        // Enrolment -> Event is not cascading in the schema, so remove enrolments (and their results) first
        var enrolments = await _db.Enrolments.Where(e => e.EventId == id).ToListAsync();
        var enrolmentIds = enrolments.Select(e => e.EnrolmentId).ToList();
        var results = await _db.Results.Where(r => enrolmentIds.Contains(r.EnrolmentId)).ToListAsync();

        _db.Results.RemoveRange(results);
        _db.Enrolments.RemoveRange(enrolments);
        var bannerBlob = ev.BannerBlobName;
        _db.Events.Remove(ev);          // categories cascade in the database
        await _db.SaveChangesAsync();

        await TryDeleteBlobAsync(bannerBlob);
        return NoContent();
    }

    // ---------------------------------------------------------------- Banner images (Azure Blob Storage)

    /// <summary>Uploads (or replaces) the event banner image in Azure Blob Storage. Owning Organiser only. JPG, PNG, GIF or WebP, max 5 MB.</summary>
    [HttpPost("{id:int}/banner")]
    [Authorize(Roles = AppRoles.Organiser)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxBannerBytes + 1024 * 1024)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<EventDto>> UploadBanner(int id, IFormFile file, CancellationToken ct)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id, ct);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != User.GetUserId()) return Forbid();

        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError("file", "Choose an image file to upload.");
            return ValidationProblem(ModelState);
        }
        if (file.Length > MaxBannerBytes)
        {
            ModelState.AddModelError("file", "The image must be 5 MB or smaller.");
            return ValidationProblem(ModelState);
        }

        // Identify the image from its real bytes, not the client-supplied name or content type
        var header = new byte[12];
        int read;
        await using (var probe = file.OpenReadStream())
            read = await probe.ReadAsync(header.AsMemory(0, header.Length), ct);
        var kind = ImageSniffer.Detect(header.AsSpan(0, read));
        if (kind is null)
        {
            ModelState.AddModelError("file", "Only JPG, PNG, GIF or WebP images are allowed.");
            return ValidationProblem(ModelState);
        }

        if (!_blobs.IsConfigured)
            return Problem(title: "Image storage is not configured on the server.",
                statusCode: StatusCodes.Status503ServiceUnavailable);

        var newBlob = $"events/{id}/{Guid.NewGuid():N}{kind.Value.Extension}";
        try
        {
            await using var stream = file.OpenReadStream();
            await _blobs.UploadAsync(newBlob, stream, kind.Value.ContentType, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "Banner upload to Azure Blob Storage failed for event {EventId}", id);
            return Problem(title: "The image could not be stored right now. Please try again.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        var oldBlob = ev.BannerBlobName;
        ev.BannerBlobName = newBlob;
        await _db.SaveChangesAsync(ct);

        await TryDeleteBlobAsync(oldBlob);
        return Ok(ev.ToDto());
    }

    /// <summary>Streams the event banner image from Azure Blob Storage.</summary>
    [HttpGet("{id:int}/banner")]
    [AllowAnonymous]
    [ResponseCache(Duration = 30, Location = ResponseCacheLocation.Any)]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetBanner(int id, CancellationToken ct)
    {
        var blobName = await _db.Events.AsNoTracking()
            .Where(e => e.EventId == id).Select(e => e.BannerBlobName).FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(blobName)) return NotFound();
        if (!_blobs.IsConfigured) return NotFound();

        try
        {
            var file = await _blobs.DownloadAsync(blobName, ct);
            return file is null ? NotFound() : File(file.Content, file.ContentType);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "Banner download from Azure Blob Storage failed for event {EventId}", id);
            return Problem(title: "The image could not be loaded right now.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    /// <summary>Removes the event banner image. Owning Organiser only.</summary>
    [HttpDelete("{id:int}/banner")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBanner(int id)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != User.GetUserId()) return Forbid();

        var blob = ev.BannerBlobName;
        ev.BannerBlobName = null;
        await _db.SaveChangesAsync();

        await TryDeleteBlobAsync(blob);
        return NoContent();
    }

    private async Task TryDeleteBlobAsync(string? blobName)
    {
        if (string.IsNullOrEmpty(blobName) || !_blobs.IsConfigured) return;
        try { await _blobs.DeleteAsync(blobName); }
        catch (Exception ex) { _log.LogWarning(ex, "Could not delete blob {Blob}", blobName); }
    }
}
