using System.Net;
using System.Net.Http.Json;

namespace RaceDay.Tests;

/// <summary>Proves that unauthenticated users and the wrong role are rejected.</summary>
public class RoleAccessTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _f;
    public RoleAccessTests(RaceDayFactory f) => _f = f;

    private static object NewEventBody() => new
    {
        name = "Nope", eventDate = DateTime.UtcNow.AddDays(10), location = "Cape Town",
        distance = 5, eventType = "Run"
    };

    [Fact]
    public async Task Anonymous_CannotCreateEvent_Returns401()
    {
        var resp = await TestHelpers.Anonymous(_f).PostAsJsonAsync("/api/events", NewEventBody());
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotCreateEvent_Returns403()
    {
        var (participant, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var resp = await participant.PostAsJsonAsync("/api/events", NewEventBody());
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotAddCategory_Returns403()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (participant, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var ev = await TestHelpers.CreateEvent(org);

        var resp = await participant.PostAsJsonAsync($"/api/events/{ev.EventId}/categories",
            new { name = "Sneaky", categoryDistance = 5 });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotEnrol_Returns403()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var ev = await TestHelpers.CreateEvent(org);
        var cat = await TestHelpers.CreateCategory(org, ev.EventId);

        var resp = await org.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Anonymous_CannotEnrol_Returns401()
    {
        var resp = await TestHelpers.Anonymous(_f).PostAsync("/api/categories/1/enrol", null);
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotViewOtherUsersProfile_Returns403()
    {
        var (participant, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var resp = await participant.GetAsync("/api/users/1");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotViewParticipantOnlyEnrolmentList_Returns403()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var resp = await org.GetAsync("/api/users/me/enrolments");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotRecordResult_Returns403()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (participant, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var ev = await TestHelpers.CreateEvent(org);
        var cat = await TestHelpers.CreateCategory(org, ev.EventId);
        await participant.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null);

        var resp = await participant.PostAsJsonAsync("/api/enrolments/1/result",
            new { finishTime = "00:50:00", finishPosition = 1 });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotListEnrolmentsOfAnotherOrganisersCategory_Returns403()
    {
        var (owner, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (other, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var ev = await TestHelpers.CreateEvent(owner);
        var cat = await TestHelpers.CreateCategory(owner, ev.EventId);

        var resp = await other.GetAsync($"/api/categories/{cat.CategoryId}/enrolments");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
