using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

public class EventTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _f;
    public EventTests(RaceDayFactory f) => _f = f;

    [Fact]
    public async Task Organiser_CanCreateEvent_Returns201()
    {
        var (org, orgId) = await TestHelpers.LoggedIn(_f, "Organiser");

        var created = await TestHelpers.CreateEvent(org, "Created Event");

        Assert.Equal("Created Event", created.Name);
        Assert.Equal(orgId, created.OrganiserId);
    }

    [Fact]
    public async Task CreateEvent_InvalidEventType_Returns400()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var resp = await org.PostAsJsonAsync("/api/events", new
        {
            name = "Bad", eventDate = DateTime.UtcNow.AddDays(5), location = "Pretoria",
            distance = 5, eventType = "Swim"
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Anyone_CanListEvents_WithoutLogin()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        await TestHelpers.CreateEvent(org, "Public Event");

        var resp = await TestHelpers.Anonymous(_f).GetAsync("/api/events");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var events = await resp.Content.ReadFromJsonAsync<List<EventDto>>();
        Assert.Contains(events!, e => e.Name == "Public Event");
    }

    [Fact]
    public async Task GetEvent_NotFound_Returns404()
    {
        var resp = await TestHelpers.Anonymous(_f).GetAsync("/api/events/999999");
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanUpdateOwnEvent()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var ev = await TestHelpers.CreateEvent(org);

        var resp = await org.PutAsJsonAsync($"/api/events/{ev.EventId}", new
        {
            name = "Updated Name", eventDate = DateTime.UtcNow.AddDays(40), location = "Durban",
            distance = 21, eventType = "Run"
        });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var updated = await resp.Content.ReadFromJsonAsync<EventDto>();
        Assert.Equal("Updated Name", updated!.Name);
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent_Returns403()
    {
        var (owner, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (other, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var ev = await TestHelpers.CreateEvent(owner);

        var resp = await other.PutAsJsonAsync($"/api/events/{ev.EventId}", new
        {
            name = "Hijack", eventDate = DateTime.UtcNow.AddDays(40), location = "X",
            distance = 5, eventType = "Walk"
        });

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanDeleteOwnEvent_ThenItIsGone()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var ev = await TestHelpers.CreateEvent(org);

        var del = await org.DeleteAsync($"/api/events/{ev.EventId}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var get = await org.GetAsync($"/api/events/{ev.EventId}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Organiser_CannotDeleteAnotherOrganisersEvent_Returns403()
    {
        var (owner, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (other, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var ev = await TestHelpers.CreateEvent(owner);

        var resp = await other.DeleteAsync($"/api/events/{ev.EventId}");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task DeleteEvent_RemovesItsEnrolments()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (participant, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var ev = await TestHelpers.CreateEvent(org);
        var cat = await TestHelpers.CreateCategory(org, ev.EventId);
        (await participant.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null)).EnsureSuccessStatusCode();

        var del = await org.DeleteAsync($"/api/events/{ev.EventId}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var mine = await participant.GetFromJsonAsync<List<EnrolmentDto>>("/api/users/me/enrolments");
        Assert.Empty(mine!);
    }
}
