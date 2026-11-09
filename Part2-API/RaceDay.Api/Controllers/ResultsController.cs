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
public class ResultsController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    public ResultsController(RaceDayDbContext db) => _db = db;

    /// <summary>Records a finish time and position for an enrolment. Owning Organiser only.</summary>
    [HttpPost("enrolments/{id:int}/result")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ResultDto>> Record(int id, ResultRequest req)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Event).Include(e => e.Participant).Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return NotFound();
        if (enrolment.Event.OrganiserId != User.GetUserId()) return Forbid();

        var time = req.FinishTime!.Value;
        if (time <= TimeSpan.Zero || time >= TimeSpan.FromHours(24))
        {
            ModelState.AddModelError(nameof(req.FinishTime), "FinishTime must be between 00:00:01 and 23:59:59.");
            return ValidationProblem(ModelState);
        }

        if (enrolment.Result is not null)
            return Problem(title: "A result has already been recorded for this enrolment.",
                statusCode: StatusCodes.Status409Conflict);

        var result = new Result
        {
            Enrolment = enrolment,
            FinishTime = time,
            FinishPosition = req.FinishPosition
        };
        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetForEnrolment), new { id }, result.ToDto());
    }

    /// <summary>Corrects an already-recorded finish time/position. Owning Organiser only.</summary>
    [HttpPut("enrolments/{id:int}/result")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultDto>> Update(int id, ResultRequest req)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Event).Include(e => e.Participant).Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null || enrolment.Result is null) return NotFound();
        if (enrolment.Event.OrganiserId != User.GetUserId()) return Forbid();

        var time = req.FinishTime!.Value;
        if (time <= TimeSpan.Zero || time >= TimeSpan.FromHours(24))
        {
            ModelState.AddModelError(nameof(req.FinishTime), "FinishTime must be between 00:00:01 and 23:59:59.");
            return ValidationProblem(ModelState);
        }

        enrolment.Result.FinishTime = time;
        enrolment.Result.FinishPosition = req.FinishPosition;
        enrolment.Result.RecordedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        enrolment.Result.Enrolment = enrolment;
        return Ok(enrolment.Result.ToDto());
    }

    /// <summary>Results leaderboard for a category, sorted by finishing position.</summary>
    [HttpGet("categories/{id:int}/results")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ResultDto>>> Leaderboard(int id)
    {
        if (!await _db.Categories.AnyAsync(c => c.CategoryId == id)) return NotFound();

        var results = await _db.Results.AsNoTracking()
            .Include(r => r.Enrolment).ThenInclude(e => e.Participant)
            .Where(r => r.Enrolment.CategoryId == id)
            .OrderBy(r => r.FinishPosition == null)
            .ThenBy(r => r.FinishPosition)
            .ThenBy(r => r.FinishTime)
            .ToListAsync();
        return Ok(results.Select(r => r.ToDto()));
    }

    /// <summary>Result for a single enrolment. Visible to the participant it belongs to or the owning Organiser.</summary>
    [HttpGet("enrolments/{id:int}/result")]
    [Authorize]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ResultDto>> GetForEnrolment(int id)
    {
        var enrolment = await _db.Enrolments.AsNoTracking()
            .Include(e => e.Event).Include(e => e.Participant).Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentId == id);
        if (enrolment is null) return NotFound();

        var uid = User.GetUserId();
        if (enrolment.ParticipantId != uid && enrolment.Event.OrganiserId != uid) return Forbid();
        if (enrolment.Result is null) return NotFound();

        enrolment.Result.Enrolment = enrolment;
        return Ok(enrolment.Result.ToDto());
    }
}
