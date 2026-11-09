using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

public class EnrolmentAndResultTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _f;
    public EnrolmentAndResultTests(RaceDayFactory f) => _f = f;

    [Fact]
    public async Task Participant_CanEnrol_AndEnrolmentIsRecorded()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (participant, participantId) = await TestHelpers.LoggedIn(_f, "Participant");
        var ev = await TestHelpers.CreateEvent(org);
        var cat = await TestHelpers.CreateCategory(org, ev.EventId);

        var resp = await participant.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null);

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var enrolment = await resp.Content.ReadFromJsonAsync<EnrolmentDto>();
        // The link between participant, event and chosen category is stored
        Assert.Equal(participantId, enrolment!.ParticipantId);
        Assert.Equal(ev.EventId, enrolment.EventId);
        Assert.Equal(cat.CategoryId, enrolment.CategoryId);

        var mine = await participant.GetFromJsonAsync<List<EnrolmentDto>>("/api/users/me/enrolments");
        Assert.Contains(mine!, e => e.EnrolmentId == enrolment.EnrolmentId);
    }

    [Fact]
    public async Task Participant_EnrolTwiceInSameEvent_Returns409()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (participant, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var ev = await TestHelpers.CreateEvent(org);
        var cat = await TestHelpers.CreateCategory(org, ev.EventId);

        await participant.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null);
        var second = await participant.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Enrol_NonExistentCategory_Returns404()
    {
        var (participant, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var resp = await participant.PostAsync("/api/categories/999999/enrol", null);
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanViewEnrolmentsForOwnCategory()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (participant, participantId) = await TestHelpers.LoggedIn(_f, "Participant");
        var ev = await TestHelpers.CreateEvent(org);
        var cat = await TestHelpers.CreateCategory(org, ev.EventId);
        await participant.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null);

        var list = await org.GetFromJsonAsync<List<EnrolmentDto>>($"/api/categories/{cat.CategoryId}/enrolments");

        Assert.Single(list!);
        Assert.Equal(participantId, list![0].ParticipantId);
    }

    [Fact]
    public async Task Participant_CanWithdraw_ButNotSomeoneElsesEnrolment()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (owner, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var (stranger, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var ev = await TestHelpers.CreateEvent(org);
        var cat = await TestHelpers.CreateCategory(org, ev.EventId);
        var enrol = await (await owner.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null))
            .Content.ReadFromJsonAsync<EnrolmentDto>();

        var forbidden = await stranger.DeleteAsync($"/api/enrolments/{enrol!.EnrolmentId}");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var ok = await owner.DeleteAsync($"/api/enrolments/{enrol.EnrolmentId}");
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanRecordResult_AndParticipantCanViewIt()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (participant, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var ev = await TestHelpers.CreateEvent(org);
        var cat = await TestHelpers.CreateCategory(org, ev.EventId);
        var enrol = await (await participant.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null))
            .Content.ReadFromJsonAsync<EnrolmentDto>();

        var post = await org.PostAsJsonAsync($"/api/enrolments/{enrol!.EnrolmentId}/result",
            new { finishTime = "00:52:31", finishPosition = 14 });
        Assert.Equal(HttpStatusCode.Created, post.StatusCode);

        var get = await participant.GetAsync($"/api/enrolments/{enrol.EnrolmentId}/result");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var result = await get.Content.ReadFromJsonAsync<ResultDto>();
        Assert.Equal(TimeSpan.Parse("00:52:31"), result!.FinishTime);
        Assert.Equal(14, result.FinishPosition);
    }

    [Fact]
    public async Task RecordResult_Twice_Returns409()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (participant, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var ev = await TestHelpers.CreateEvent(org);
        var cat = await TestHelpers.CreateCategory(org, ev.EventId);
        var enrol = await (await participant.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null))
            .Content.ReadFromJsonAsync<EnrolmentDto>();
        var body = new { finishTime = "00:45:00", finishPosition = 1 };

        await org.PostAsJsonAsync($"/api/enrolments/{enrol!.EnrolmentId}/result", body);
        var second = await org.PostAsJsonAsync($"/api/enrolments/{enrol.EnrolmentId}/result", body);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task OtherParticipant_CannotViewSomeoneElsesResult_Returns403()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (owner, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var (stranger, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var ev = await TestHelpers.CreateEvent(org);
        var cat = await TestHelpers.CreateCategory(org, ev.EventId);
        var enrol = await (await owner.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null))
            .Content.ReadFromJsonAsync<EnrolmentDto>();
        await org.PostAsJsonAsync($"/api/enrolments/{enrol!.EnrolmentId}/result",
            new { finishTime = "00:40:00", finishPosition = 1 });

        var resp = await stranger.GetAsync($"/api/enrolments/{enrol.EnrolmentId}/result");

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Leaderboard_IsSortedByPosition()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (p1, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var (p2, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var ev = await TestHelpers.CreateEvent(org);
        var cat = await TestHelpers.CreateCategory(org, ev.EventId);
        var e1 = await (await p1.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null))
            .Content.ReadFromJsonAsync<EnrolmentDto>();
        var e2 = await (await p2.PostAsync($"/api/categories/{cat.CategoryId}/enrol", null))
            .Content.ReadFromJsonAsync<EnrolmentDto>();

        await org.PostAsJsonAsync($"/api/enrolments/{e1!.EnrolmentId}/result", new { finishTime = "01:00:00", finishPosition = 2 });
        await org.PostAsJsonAsync($"/api/enrolments/{e2!.EnrolmentId}/result", new { finishTime = "00:50:00", finishPosition = 1 });

        var board = await TestHelpers.Anonymous(_f)
            .GetFromJsonAsync<List<ResultDto>>($"/api/categories/{cat.CategoryId}/results");

        Assert.Equal(new int?[] { 1, 2 }, board!.Select(r => r.FinishPosition).ToArray());
    }
}
