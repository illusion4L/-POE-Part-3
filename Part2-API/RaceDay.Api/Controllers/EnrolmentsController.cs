using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api")]
public class EnrolmentsController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    public EnrolmentsController(RaceDayDbContext db) => _db = db;

    /// <summary>Enrols the logged-in Participant into a category (and therefore its event).</summary>
    [HttpPost("categories/{id:int}/enrol")]
    [Authorize(Roles = AppRoles.Participant)]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EnrolmentDto>> Enrol(int id)
    {
        var category = await _db.Categories.FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFound();

        var uid = User.GetUserId();
        if (await _db.Enrolments.AnyAsync(e => e.ParticipantId == uid && e.EventId == category.EventId))
            return Problem(title: "You are already enrolled in this event.",
                statusCode: StatusCodes.Status409Conflict);

        var enrolment = new Enrolment
        {
            ParticipantId = uid,
            EventId = category.EventId,
            CategoryId = category.CategoryId
        };
        _db.Enrolments.Add(enrolment);
        await _db.SaveChangesAsync();

        var saved = await _db.Enrolments.AsNoTracking()
            .Include(e => e.Participant).Include(e => e.Event).Include(e => e.Category)
            .FirstAsync(e => e.EnrolmentId == enrolment.EnrolmentId);

        return StatusCode(StatusCodes.Status201Created, saved.ToDto());
    }

    /// <summary>Lists all enrolments in a category. Owning Organiser only.</summary>
    [HttpGet("categories/{id:int}/enrolments")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EnrolmentDto>>> ForCategory(int id)
    {
        var category = await _db.Categories.Include(c => c.Event).AsNoTracking()
            .FirstOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFound();
        if (category.Event.OrganiserId != User.GetUserId()) return Forbid();

        var list = await _db.Enrolments.AsNoTracking()
            .Include(e => e.Participant).Include(e => e.Event).Include(e => e.Category).Include(e => e.Result)
            .Where(e => e.CategoryId == id)
            .OrderBy(e => e.EnrolmentDate)
            .ToListAsync();
        return Ok(list.Select(e => e.ToDto()));
    }

    /// <summary>Lists every enrolment for an event (all categories) with status and any result. Owning Organiser only.</summary>
    [HttpGet("events/{eventId:int}/enrolments")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<EnrolmentDto>>> ForEvent(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != User.GetUserId()) return Forbid();

        var list = await _db.Enrolments.AsNoTracking()
            .Include(e => e.Participant).Include(e => e.Event).Include(e => e.Category).Include(e => e.Result)
            .Where(e => e.EventId == eventId)
            .OrderBy(e => e.CategoryId).ThenBy(e => e.EnrolmentDate)
            .ToListAsync();
        return Ok(list.Select(e => e.ToDto()));
    }

    /// <summary>Sets an enrolment's status (Pending, Confirmed or Cancelled). Owning Organiser only.</summary>
    [HttpPut("enrolments/{id:int}/status")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EnrolmentDto>> SetStatus(int id, EnrolmentStatusRequest req)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Participant).Include(e => e.Event).Include(e => e.Category).Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return NotFound();
        if (enrolment.Event.OrganiserId != User.GetUserId()) return Forbid();

        enrolment.Status = req.Status;
        await _db.SaveChangesAsync();
        return Ok(enrolment.ToDto());
    }

    /// <summary>Withdraws the logged-in Participant from an event. Owner only.</summary>
    [HttpDelete("enrolments/{id:int}")]
    [Authorize(Roles = AppRoles.Participant)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Withdraw(int id)
    {
        var enrolment = await _db.Enrolments.Include(e => e.Result).FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return NotFound();
        if (enrolment.ParticipantId != User.GetUserId()) return Forbid();

        if (enrolment.Result is not null) _db.Results.Remove(enrolment.Result);
        _db.Enrolments.Remove(enrolment);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
