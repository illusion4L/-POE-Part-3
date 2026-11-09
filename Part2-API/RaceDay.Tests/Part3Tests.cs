using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

/// <summary>Endpoints added for the MVC front end: dashboard totals, enrolment status, personal results.</summary>
public class Part3Tests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _f;
    public Part3Tests(RaceDayFactory f) => _f = f;

    private async Task<EnrolmentDto> Enrol(HttpClient participant, int categoryId) =>
        (await (await participant.PostAsync($"/api/categories/{categoryId}/enrol", null))
            .Content.ReadFromJsonAsync<EnrolmentDto>())!;

    [Fact]
    public async Task NewEnrolment_IsPending_AndOrganiserCanConfirmIt()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (participant, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var ev = await TestHelpers.CreateEvent(org);
        var cat = await TestHelpers.CreateCategory(org, ev.EventId);

        var enrolment = await Enrol(participant, cat.CategoryId);
        Assert.Equal("Pending", enrolment.Status);

        var put = await org.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status", new { status = "Confirmed" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var mine = await participant.GetFromJsonAsync<List<EnrolmentDto>>("/api/users/me/enrolments");
        Assert.Equal("Confirmed", mine!.Single(e => e.EnrolmentId == enrolment.EnrolmentId).Status);
    }

    [Fact]
    public async Task SetStatus_InvalidValue_Returns400()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (participant, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var ev = await TestHelpers.CreateEvent(org);
        var cat = await TestHelpers.CreateCategory(org, ev.EventId);
        var enrolment = await Enrol(participant, cat.CategoryId);

        var put = await org.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status", new { status = "Maybe" });

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotChangeEnrolmentStatus_Returns403()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (participant, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var ev = await TestHelpers.CreateEvent(org);
        var cat = await TestHelpers.CreateCategory(org, ev.EventId);
        var enrolment = await Enrol(participant, cat.CategoryId);

        var put = await participant.PutAsJsonAsync($"/api/enrolments/{enrolment.EnrolmentId}/status", new { status = "Confirmed" });

        Assert.Equal(HttpStatusCode.Forbidden, put.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanListAllEnrolmentsForAnEvent()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (p1, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var (p2, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var ev = await TestHelpers.CreateEvent(org);
        var cat = await TestHelpers.CreateCategory(org, ev.EventId);
        await Enrol(p1, cat.CategoryId);
        await Enrol(p2, cat.CategoryId);

        var list = await org.GetFromJsonAsync<List<EnrolmentDto>>($"/api/events/{ev.EventId}/enrolments");

        Assert.Equal(2, list!.Count);
        Assert.All(list, e => Assert.Equal("Pending", e.Status));
    }

    [Fact]
    public async Task Dashboard_ListsOwnEventsWithEnrolmentCounts()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (other, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (participant, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var ev = await TestHelpers.CreateEvent(org);
        await TestHelpers.CreateEvent(other, "Someone else's event");
        var cat = await TestHelpers.CreateCategory(org, ev.EventId);
        await Enrol(participant, cat.CategoryId);

        var mine = await org.GetFromJsonAsync<List<OrganiserEventSummaryDto>>("/api/events/mine");

        var row = Assert.Single(mine!);
        Assert.Equal(ev.EventId, row.EventId);
        Assert.Equal(1, row.EnrolmentCount);
        Assert.Equal(1, row.CategoryCount);
    }

    [Fact]
    public async Task MyResults_ShowsPositionAndTotalFinishers()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (p1, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var (p2, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var ev = await TestHelpers.CreateEvent(org);
        var cat = await TestHelpers.CreateCategory(org, ev.EventId);
        var e1 = await Enrol(p1, cat.CategoryId);
        var e2 = await Enrol(p2, cat.CategoryId);
        await org.PostAsJsonAsync($"/api/enrolments/{e1.EnrolmentId}/result", new { finishTime = "00:50:00", finishPosition = 1 });
        await org.PostAsJsonAsync($"/api/enrolments/{e2.EnrolmentId}/result", new { finishTime = "00:55:10", finishPosition = 2 });

        var results = await p2.GetFromJsonAsync<List<MyResultDto>>("/api/users/me/results");

        var row = Assert.Single(results!);
        Assert.Equal(2, row.FinishPosition);
        Assert.Equal(2, row.TotalFinishers);
        Assert.Equal(TimeSpan.Parse("00:55:10"), row.FinishTime);
        Assert.Equal(ev.Name, row.EventName);
        Assert.Equal(cat.Name, row.CategoryName);
    }

    [Fact]
    public async Task Organiser_CanCorrectAResult()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (participant, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var ev = await TestHelpers.CreateEvent(org);
        var cat = await TestHelpers.CreateCategory(org, ev.EventId);
        var e = await Enrol(participant, cat.CategoryId);
        await org.PostAsJsonAsync($"/api/enrolments/{e.EnrolmentId}/result", new { finishTime = "00:50:00", finishPosition = 5 });

        var put = await org.PutAsJsonAsync($"/api/enrolments/{e.EnrolmentId}/result", new { finishTime = "00:49:30", finishPosition = 4 });

        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var result = await put.Content.ReadFromJsonAsync<ResultDto>();
        Assert.Equal(4, result!.FinishPosition);
        Assert.Equal(TimeSpan.Parse("00:49:30"), result.FinishTime);
    }
}
