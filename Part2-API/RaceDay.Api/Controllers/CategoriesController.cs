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
public class CategoriesController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    public CategoriesController(RaceDayDbContext db) => _db = db;

    private static bool AgeRangeInvalid(CategoryRequest r) =>
        r.MinAge.HasValue && r.MaxAge.HasValue && r.MinAge > r.MaxAge;

    private ActionResult AgeRangeError()
    {
        ModelState.AddModelError(nameof(CategoryRequest.MinAge), "MinAge cannot be greater than MaxAge.");
        return ValidationProblem(ModelState);
    }

    /// <summary>Lists all categories for an event.</summary>
    [HttpGet("events/{eventId:int}/categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<CategoryDto>>> GetForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId)) return NotFound();
        var cats = await _db.Categories.AsNoTracking()
            .Where(c => c.EventId == eventId).OrderBy(c => c.CategoryId).ToListAsync();
        return Ok(cats.Select(c => c.ToDto()));
    }

    /// <summary>Adds a category (e.g. Under 20, 10km) to an event. Owning Organiser only.</summary>
    [HttpPost("events/{eventId:int}/categories")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> Create(int eventId, CategoryRequest req)
    {
        var ev = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFound();
        if (ev.OrganiserId != User.GetUserId()) return Forbid();
        if (AgeRangeInvalid(req)) return AgeRangeError();

        var cat = new Category
        {
            EventId = eventId,
            Name = req.Name.Trim(),
            MinAge = req.MinAge,
            MaxAge = req.MaxAge,
            CategoryDistance = req.CategoryDistance
        };
        _db.Categories.Add(cat);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(GetForEvent), new { eventId }, cat.ToDto());
    }

    /// <summary>Updates a category. Owning Organiser only.</summary>
    [HttpPut("categories/{id:int}")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> Update(int id, CategoryRequest req)
    {
        var cat = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (cat is null) return NotFound();
        if (cat.Event.OrganiserId != User.GetUserId()) return Forbid();
        if (AgeRangeInvalid(req)) return AgeRangeError();

        cat.Name = req.Name.Trim();
        cat.MinAge = req.MinAge;
        cat.MaxAge = req.MaxAge;
        cat.CategoryDistance = req.CategoryDistance;
        await _db.SaveChangesAsync();
        return Ok(cat.ToDto());
    }

    /// <summary>Deletes a category and its enrolments/results. Owning Organiser only.</summary>
    [HttpDelete("categories/{id:int}")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var cat = await _db.Categories.Include(c => c.Event).FirstOrDefaultAsync(c => c.CategoryId == id);
        if (cat is null) return NotFound();
        if (cat.Event.OrganiserId != User.GetUserId()) return Forbid();

        var enrolments = await _db.Enrolments.Where(e => e.CategoryId == id).ToListAsync();
        var enrolmentIds = enrolments.Select(e => e.EnrolmentId).ToList();
        var results = await _db.Results.Where(r => enrolmentIds.Contains(r.EnrolmentId)).ToListAsync();

        _db.Results.RemoveRange(results);
        _db.Enrolments.RemoveRange(enrolments);
        _db.Categories.Remove(cat);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
