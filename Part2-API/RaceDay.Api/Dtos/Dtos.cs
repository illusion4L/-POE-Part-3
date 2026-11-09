using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

// ---------- Auth ----------
public class RegisterRequest
{
    [Required, StringLength(100)] public string FullName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(150)] public string Email { get; set; } = string.Empty;
    [Required, MinLength(8)] public string Password { get; set; } = string.Empty;
    /// <summary>Either "Organiser" or "Participant".</summary>
    [Required] public string Role { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    [Required] public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

// ---------- Users ----------
public class UserProfileDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class UpdateProfileRequest
{
    [Required, StringLength(100)] public string FullName { get; set; } = string.Empty;
}

// ---------- Events ----------
public class EventRequest
{
    [Required, StringLength(150)] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    [Required] public DateTime? EventDate { get; set; }
    [Required, StringLength(150)] public string Location { get; set; } = string.Empty;
    [Range(0.01, 9999.99)] public decimal Distance { get; set; }
    /// <summary>Run, Walk or Cycle.</summary>
    [Required, RegularExpression("^(Run|Walk|Cycle)$", ErrorMessage = "EventType must be Run, Walk or Cycle.")]
    public string EventType { get; set; } = string.Empty;
}

public class EventDto
{
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal Distance { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int OrganiserId { get; set; }
    /// <summary>True when a banner image is stored in Azure Blob Storage. Fetch it from GET /api/events/{id}/banner.</summary>
    public bool HasBanner { get; set; }
}

/// <summary>Organiser dashboard row: an event plus enrolment/category totals.</summary>
public class OrganiserEventSummaryDto : EventDto
{
    public int CategoryCount { get; set; }
    public int EnrolmentCount { get; set; }
}

public class EventDetailDto : EventDto
{
    public List<CategoryDto> Categories { get; set; } = new();
}

// ---------- Categories ----------
public class CategoryRequest
{
    [Required, StringLength(50)] public string Name { get; set; } = string.Empty;
    [Range(0, 120)] public int? MinAge { get; set; }
    [Range(0, 120)] public int? MaxAge { get; set; }
    [Range(0.01, 9999.99)] public decimal CategoryDistance { get; set; }
}

public class CategoryDto
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public decimal CategoryDistance { get; set; }
}

// ---------- Enrolments ----------
public class EnrolmentDto
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public DateTime EnrolmentDate { get; set; }
    public DateTime EventDate { get; set; }
    public string EventLocation { get; set; } = string.Empty;
    /// <summary>Pending, Confirmed or Cancelled.</summary>
    public string Status { get; set; } = EnrolmentStatuses.Pending;
    public bool HasResult { get; set; }
    public TimeSpan? FinishTime { get; set; }
    public int? FinishPosition { get; set; }
}

public class EnrolmentStatusRequest
{
    [Required, RegularExpression("^(Pending|Confirmed|Cancelled)$",
        ErrorMessage = "Status must be Pending, Confirmed or Cancelled.")]
    public string Status { get; set; } = string.Empty;
}

// ---------- Results ----------
public class ResultRequest
{
    /// <summary>Format hh:mm:ss, e.g. 00:52:31.</summary>
    [Required] public TimeSpan? FinishTime { get; set; }
    [Range(1, int.MaxValue)] public int? FinishPosition { get; set; }
}

public class ResultDto
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public TimeSpan FinishTime { get; set; }
    public int? FinishPosition { get; set; }
    public DateTime RecordedAt { get; set; }
}

/// <summary>One row of a participant's personal race history.</summary>
public class MyResultDto
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public TimeSpan FinishTime { get; set; }
    public int? FinishPosition { get; set; }
    /// <summary>Number of finishers with a recorded result in the same category.</summary>
    public int TotalFinishers { get; set; }
}

// ---------- Mapping ----------
public static class Mapping
{
    public static EventDto ToDto(this Event e) => new()
    {
        EventId = e.EventId, Name = e.Name, Description = e.Description, EventDate = e.EventDate,
        Location = e.Location, Distance = e.Distance, EventType = e.EventType, OrganiserId = e.OrganiserId,
        HasBanner = e.BannerBlobName != null
    };

    public static EventDetailDto ToDetailDto(this Event e) => new()
    {
        EventId = e.EventId, Name = e.Name, Description = e.Description, EventDate = e.EventDate,
        Location = e.Location, Distance = e.Distance, EventType = e.EventType, OrganiserId = e.OrganiserId,
        HasBanner = e.BannerBlobName != null,
        Categories = e.Categories.OrderBy(c => c.CategoryId).Select(c => c.ToDto()).ToList()
    };

    public static CategoryDto ToDto(this Category c) => new()
    {
        CategoryId = c.CategoryId, EventId = c.EventId, Name = c.Name,
        MinAge = c.MinAge, MaxAge = c.MaxAge, CategoryDistance = c.CategoryDistance
    };

    /// <summary>Requires Participant, Event and Category (and Result, if wanted) to be loaded.</summary>
    public static EnrolmentDto ToDto(this Enrolment e) => new()
    {
        EnrolmentId = e.EnrolmentId, ParticipantId = e.ParticipantId, ParticipantName = e.Participant.FullName,
        EventId = e.EventId, EventName = e.Event.Name, CategoryId = e.CategoryId,
        CategoryName = e.Category.Name, EnrolmentDate = e.EnrolmentDate,
        EventDate = e.Event.EventDate, EventLocation = e.Event.Location, Status = e.Status,
        HasResult = e.Result != null, FinishTime = e.Result?.FinishTime, FinishPosition = e.Result?.FinishPosition
    };

    /// <summary>Requires Enrolment and Enrolment.Participant to be loaded.</summary>
    public static ResultDto ToDto(this Result r) => new()
    {
        ResultId = r.ResultId, EnrolmentId = r.EnrolmentId, ParticipantName = r.Enrolment.Participant.FullName,
        FinishTime = r.FinishTime, FinishPosition = r.FinishPosition, RecordedAt = r.RecordedAt
    };
}
