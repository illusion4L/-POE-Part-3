using System.ComponentModel.DataAnnotations;

namespace RaceDay.Web.Models;

// ---------------------------------------------------------------- account
public class LoginViewModel
{
    [Required(ErrorMessage = "Enter your email address."), EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Enter your password."), DataType(DataType.Password)]
    public string Password { get; set; } = "";

    public string? ReturnUrl { get; set; }
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Enter your full name."), StringLength(100, ErrorMessage = "Your name can be up to 100 characters.")]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Enter your email address."), EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(150)]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Choose a password."), MinLength(8, ErrorMessage = "Your password needs at least 8 characters.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Repeat your password."), DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "The passwords don't match.")]
    [Display(Name = "Confirm password")]
    public string ConfirmPassword { get; set; } = "";

    [Required(ErrorMessage = "Choose how you'll use RaceDay.")]
    [RegularExpression("^(Organiser|Participant)$", ErrorMessage = "Choose Participant or Organiser.")]
    public string Role { get; set; } = "Participant";
}

// ---------------------------------------------------------------- public / participant
public class EventListViewModel
{
    public List<EventDto> Events { get; set; } = new();
    public string? Location { get; set; }
    public string? EventType { get; set; }
    public DateTime? Date { get; set; }
    public bool IsFiltered => !string.IsNullOrWhiteSpace(Location) || !string.IsNullOrWhiteSpace(EventType) || Date.HasValue;
}

public class EventDetailsViewModel
{
    public EventDetailDto Event { get; set; } = new();
    /// <summary>The logged-in participant's enrolment in this event, if any.</summary>
    public EnrolmentDto? MyEnrolment { get; set; }
    public bool IsPast => Event.EventDate.Date < DateTime.Today;
}

// ---------------------------------------------------------------- organiser
public class DashboardViewModel
{
    public List<OrganiserEventSummaryDto> Events { get; set; } = new();
    public int TotalEvents => Events.Count;
    public int TotalEnrolments => Events.Sum(e => e.EnrolmentCount);
    public List<OrganiserEventSummaryDto> Upcoming =>
        Events.Where(e => e.EventDate.Date >= DateTime.Today).OrderBy(e => e.EventDate).ToList();
}

public class EventFormViewModel
{
    public int? EventId { get; set; }

    [Required(ErrorMessage = "Enter the event name."), StringLength(150, ErrorMessage = "The name can be up to 150 characters.")]
    public string Name { get; set; } = "";

    [StringLength(4000, ErrorMessage = "The description can be up to 4000 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Choose the event date and start time.")]
    [Display(Name = "Date and start time")]
    public DateTime? EventDate { get; set; }

    [Required(ErrorMessage = "Enter the event location."), StringLength(150, ErrorMessage = "The location can be up to 150 characters.")]
    public string Location { get; set; } = "";

    [Range(0.01, 9999.99, ErrorMessage = "Enter a distance between 0.01 and 9999.99 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? Distance { get; set; }

    [Required(ErrorMessage = "Choose an event type.")]
    [RegularExpression("^(Run|Walk|Cycle)$", ErrorMessage = "Choose Run, Walk or Cycle.")]
    [Display(Name = "Event type")]
    public string EventType { get; set; } = "Run";

    [Display(Name = "Banner image")]
    public IFormFile? BannerFile { get; set; }

    public bool RemoveBanner { get; set; }
    public bool HasBanner { get; set; }
    public bool IsEdit => EventId.HasValue;
}

public class CategoryFormViewModel
{
    public int? CategoryId { get; set; }
    public int EventId { get; set; }

    [Required(ErrorMessage = "Enter a category name, for example Under 20 or 10 km."), StringLength(50, ErrorMessage = "The name can be up to 50 characters.")]
    public string Name { get; set; } = "";

    [Range(0, 120, ErrorMessage = "Minimum age must be between 0 and 120.")]
    [Display(Name = "Minimum age")]
    public int? MinAge { get; set; }

    [Range(0, 120, ErrorMessage = "Maximum age must be between 0 and 120.")]
    [Display(Name = "Maximum age")]
    public int? MaxAge { get; set; }

    [Required(ErrorMessage = "Enter the category distance."), Range(0.01, 9999.99, ErrorMessage = "Enter a distance between 0.01 and 9999.99 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? CategoryDistance { get; set; }
}

public class CategoriesPageViewModel
{
    public EventDetailDto Event { get; set; } = new();
    public CategoryFormViewModel Form { get; set; } = new();
}

public class EventEnrolmentsViewModel
{
    public EventDetailDto Event { get; set; } = new();
    public List<EnrolmentDto> AllEnrolments { get; set; } = new();
    public List<EnrolmentDto> Enrolments { get; set; } = new();
    public string? Status { get; set; }
    public int? CategoryId { get; set; }
}

public class ResultsEntryViewModel
{
    public EventDetailDto Event { get; set; } = new();
    public List<EnrolmentDto> Enrolments { get; set; } = new();
    public bool HasNotHappenedYet => Event.EventDate > DateTime.Now;
}

public class ResultInputModel
{
    public int EventId { get; set; }
    public int EnrolmentId { get; set; }
    public bool IsUpdate { get; set; }

    [Required(ErrorMessage = "Enter the finish time.")]
    [RegularExpression(@"^\d{1,2}:[0-5]\d:[0-5]\d$", ErrorMessage = "Enter the finish time as hh:mm:ss, for example 00:52:31.")]
    public string FinishTime { get; set; } = "";

    [Range(1, 100000, ErrorMessage = "The finishing position must be a whole number of 1 or more.")]
    public int? FinishPosition { get; set; }
}

public class StatusPageViewModel
{
    public int Code { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
}
