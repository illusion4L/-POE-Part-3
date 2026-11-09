using System.Net;
using System.Net.Http.Json;
using RaceDay.Api.Dtos;

namespace RaceDay.Tests;

public class AuthTests : IClassFixture<RaceDayFactory>
{
    private readonly RaceDayFactory _f;
    public AuthTests(RaceDayFactory f) => _f = f;

    [Theory]
    [InlineData("Organiser")]
    [InlineData("Participant")]
    public async Task Register_ValidDetails_Returns201WithRole(string role)
    {
        var client = TestHelpers.Anonymous(_f);
        var resp = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "New User", email = $"{Guid.NewGuid():N}@raceday.test",
            password = TestHelpers.Password, role
        });

        Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.Equal(role, body!.Role);
        Assert.True(body.UserId > 0);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var client = TestHelpers.Anonymous(_f);
        var payload = new
        {
            fullName = "Dup", email = $"{Guid.NewGuid():N}@raceday.test",
            password = TestHelpers.Password, role = "Participant"
        };
        await client.PostAsJsonAsync("/api/auth/register", payload);
        var second = await client.PostAsJsonAsync("/api/auth/register", payload);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Register_InvalidRole_Returns400()
    {
        var client = TestHelpers.Anonymous(_f);
        var resp = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "Hacker", email = $"{Guid.NewGuid():N}@raceday.test",
            password = TestHelpers.Password, role = "Admin"
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Register_MissingFields_Returns400()
    {
        var client = TestHelpers.Anonymous(_f);
        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "", email = "", password = "", role = "" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_Returns200AndStartsSession()
    {
        var (client, _) = await TestHelpers.LoggedIn(_f, "Participant");

        var me = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var profile = await me.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.Equal("Participant", profile!.Role);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var client = TestHelpers.Anonymous(_f);
        var email = $"{Guid.NewGuid():N}@raceday.test";
        await client.PostAsJsonAsync("/api/auth/register",
            new { fullName = "User", email, password = TestHelpers.Password, role = "Participant" });

        var resp = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "WrongPassword1" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401()
    {
        var client = TestHelpers.Anonymous(_f);
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "nobody@raceday.test", password = TestHelpers.Password });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Profile_WithoutSession_Returns401()
    {
        var resp = await TestHelpers.Anonymous(_f).GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsSession()
    {
        var (client, _) = await TestHelpers.LoggedIn(_f, "Organiser");
        var logout = await client.PostAsync("/api/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        var me = await client.GetAsync("/api/users/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task Profile_Update_ChangesFullName()
    {
        var (client, _) = await TestHelpers.LoggedIn(_f, "Participant");
        var resp = await client.PutAsJsonAsync("/api/users/me", new { fullName = "Renamed Person" });

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var profile = await resp.Content.ReadFromJsonAsync<UserProfileDto>();
        Assert.Equal("Renamed Person", profile!.FullName);
    }
}
