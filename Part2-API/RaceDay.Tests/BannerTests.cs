using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

public class BannerTests : IClassFixture<RaceDayFactory>
{
    private static readonly byte[] Png =
        { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0x0D, 0x49, 0x48, 0x44, 0x52 };

    private readonly RaceDayFactory _f;
    public BannerTests(RaceDayFactory f) => _f = f;

    private static MultipartFormDataContent FileContent(byte[] bytes, string fileName, string contentType)
    {
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return new MultipartFormDataContent { { part, "file", fileName } };
    }

    [Fact]
    public async Task Organiser_CanUploadBanner_AndAnyoneCanFetchIt()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var ev = await TestHelpers.CreateEvent(org);

        var upload = await org.PostAsync($"/api/events/{ev.EventId}/banner", FileContent(Png, "banner.png", "image/png"));

        Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
        var dto = await upload.Content.ReadFromJsonAsync<EventDto>();
        Assert.True(dto!.HasBanner);

        var image = await TestHelpers.Anonymous(_f).GetAsync($"/api/events/{ev.EventId}/banner");
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType!.MediaType);
        Assert.Equal(Png, await image.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Upload_NotAnImage_Returns400()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var ev = await TestHelpers.CreateEvent(org);

        // Claims to be a PNG but the bytes are plain text
        var resp = await org.PostAsync($"/api/events/{ev.EventId}/banner",
            FileContent("not an image"u8.ToArray(), "banner.png", "image/png"));

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Participant_CannotUploadBanner_Returns403()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (participant, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var ev = await TestHelpers.CreateEvent(org);

        var resp = await participant.PostAsync($"/api/events/{ev.EventId}/banner", FileContent(Png, "b.png", "image/png"));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task OtherOrganiser_CannotUploadBanner_Returns403()
    {
        var (owner, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var (other, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var ev = await TestHelpers.CreateEvent(owner);

        var resp = await other.PostAsync($"/api/events/{ev.EventId}/banner", FileContent(Png, "b.png", "image/png"));

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task EventWithoutBanner_Returns404()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var ev = await TestHelpers.CreateEvent(org);

        var resp = await TestHelpers.Anonymous(_f).GetAsync($"/api/events/{ev.EventId}/banner");

        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
    }

    [Fact]
    public async Task Organiser_CanRemoveBanner()
    {
        var (org, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var ev = await TestHelpers.CreateEvent(org);
        await org.PostAsync($"/api/events/{ev.EventId}/banner", FileContent(Png, "b.png", "image/png"));

        var del = await org.DeleteAsync($"/api/events/{ev.EventId}/banner");

        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
        var after = await org.GetFromJsonAsync<EventDto>($"/api/events/{ev.EventId}");
        Assert.False(after!.HasBanner);
    }
}
