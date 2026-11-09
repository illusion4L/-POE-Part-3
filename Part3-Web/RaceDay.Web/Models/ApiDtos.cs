namespace RaceDay.Web.Models;

// Shapes returned by the RaceDay API. Plain data only - no business logic lives in the MVC project.

public class AuthResponse
{
    public int UserId { get; set; }
    public string FullName { get; set; } = "";
    public string Role { get; set; } = "";
}

public class EventDto
{
    public int EventId { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = "";
    public decimal Distance { get; set; }
    public string EventType { get; set; } = "";
    public int OrganiserId { get; set; }
    public bool HasBanner { get; set; }
}

public class OrganiserEventSummaryDto : EventDto
{
    public int CategoryCount { get; set; }
    public int EnrolmentCount { get; set; }
}

public class EventDetailDto : EventDto
{
    public List<CategoryDto> Categories { get; set; } = new();
}

public class CategoryDto
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string Name { get; set; } = "";
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public decimal CategoryDistance { get; set; }
}

public class EnrolmentDto
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = "";
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public DateTime EnrolmentDate { get; set; }
    public DateTime EventDate { get; set; }
    public string EventLocation { get; set; } = "";
    public string Status { get; set; } = "Pending";
    public bool HasResult { get; set; }
    public TimeSpan? FinishTime { get; set; }
    public int? FinishPosition { get; set; }
}

public class MyResultDto
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = "";
    public DateTime EventDate { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "";
    public TimeSpan FinishTime { get; set; }
    public int? FinishPosition { get; set; }
    public int TotalFinishers { get; set; }
}

public record BannerImage(byte[] Bytes, string ContentType);
