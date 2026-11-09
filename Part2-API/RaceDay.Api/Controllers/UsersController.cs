using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    public UsersController(RaceDayDbContext db) => _db = db;

    private static UserProfileDto ToProfile(User u) => new()
    {
        UserId = u.UserId, FullName = u.FullName, Email = u.Email,
        Role = u.Role.RoleName, CreatedAt = u.CreatedAt
    };

    /// <summary>Returns the profile of the logged-in user (any role).</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileDto>> GetMe()
    {
        var uid = User.GetUserId();
        var user = await _db.Users.Include(u => u.Role).AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == uid);
        return user is null ? NotFound() : Ok(ToProfile(user));
    }

    /// <summary>Updates the logged-in user's own profile (full name).</summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserProfileDto>> UpdateMe(UpdateProfileRequest req)
    {
        var uid = User.GetUserId();
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserId == uid);
        if (user is null) return NotFound();

        user.FullName = req.FullName.Trim();
        await _db.SaveChangesAsync();
        return Ok(ToProfile(user));
    }

    /// <summary>Returns a specific user's public profile. Organisers only.</summary>
    [HttpGet("{id:int}")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(UserProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserProfileDto>> GetById(int id)
    {
        var user = await _db.Users.Include(u => u.Role).AsNoTracking().FirstOrDefaultAsync(u => u.UserId == id);
        return user is null ? NotFound() : Ok(ToProfile(user));
    }

    /// <summary>Lists the events/categories the logged-in participant is enrolled in. Participants only.</summary>
    [HttpGet("me/enrolments")]
    [Authorize(Roles = AppRoles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<EnrolmentDto>>> MyEnrolments()
    {
        var uid = User.GetUserId();
        var list = await _db.Enrolments.AsNoTracking()
            .Include(e => e.Participant).Include(e => e.Event).Include(e => e.Category).Include(e => e.Result)
            .Where(e => e.ParticipantId == uid)
            .OrderByDescending(e => e.EnrolmentDate)
            .ToListAsync();
        return Ok(list.Select(e => e.ToDto()));
    }

    /// <summary>The logged-in participant's race history: every result recorded for them, newest event first.</summary>
    [HttpGet("me/results")]
    [Authorize(Roles = AppRoles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<MyResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<MyResultDto>>> MyResults()
    {
        var uid = User.GetUserId();
        var rows = await _db.Results.AsNoTracking()
            .Where(r => r.Enrolment.ParticipantId == uid)
            .OrderByDescending(r => r.Enrolment.Event.EventDate)
            .Select(r => new MyResultDto
            {
                ResultId = r.ResultId,
                EnrolmentId = r.EnrolmentId,
                EventId = r.Enrolment.EventId,
                EventName = r.Enrolment.Event.Name,
                EventDate = r.Enrolment.Event.EventDate,
                CategoryId = r.Enrolment.CategoryId,
                CategoryName = r.Enrolment.Category.Name,
                FinishTime = r.FinishTime,
                FinishPosition = r.FinishPosition,
                TotalFinishers = _db.Results.Count(x => x.Enrolment.CategoryId == r.Enrolment.CategoryId)
            })
            .ToListAsync();
        return Ok(rows);
    }
}
