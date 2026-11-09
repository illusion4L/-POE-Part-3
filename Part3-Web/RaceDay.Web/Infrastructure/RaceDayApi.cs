using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using RaceDay.Web.Models;

namespace RaceDay.Web.Infrastructure;

/// <summary>Everything the MVC app can ask of the Part 2 API. Controllers never use HttpClient directly.</summary>
public interface IRaceDayApi
{
    // Auth
    Task<AuthResponse> LoginAsync(string email, string password);
    Task RegisterAsync(string fullName, string email, string password, string role);
    Task LogoutAsync();

    // Events
    Task<List<EventDto>> GetEventsAsync(DateTime? date, string? location, string? eventType);
    Task<EventDetailDto> GetEventAsync(int id);
    Task<List<OrganiserEventSummaryDto>> GetMyEventsAsync();
    Task<EventDto> CreateEventAsync(object request);
    Task<EventDto> UpdateEventAsync(int id, object request);
    Task DeleteEventAsync(int id);

    // Banner images (Azure Blob Storage sits behind the API)
    Task UploadBannerAsync(int eventId, IFormFile file);
    Task DeleteBannerAsync(int eventId);
    Task<BannerImage?> GetBannerAsync(int eventId);

    // Categories
    Task<CategoryDto> CreateCategoryAsync(int eventId, object request);
    Task<CategoryDto> UpdateCategoryAsync(int categoryId, object request);
    Task DeleteCategoryAsync(int categoryId);

    // Enrolments
    Task<EnrolmentDto> EnrolAsync(int categoryId);
    Task<List<EnrolmentDto>> GetMyEnrolmentsAsync();
    Task WithdrawAsync(int enrolmentId);
    Task<List<EnrolmentDto>> GetEventEnrolmentsAsync(int eventId);
    Task SetEnrolmentStatusAsync(int enrolmentId, string status);

    // Results
    Task RecordResultAsync(int enrolmentId, TimeSpan finishTime, int? position, bool update);
    Task<List<MyResultDto>> GetMyResultsAsync();
}

public class RaceDayApi : IRaceDayApi
{
    private const string ApiCookieName = "RaceDay.Session";   // cookie issued by the API on login
    private const string ApiCookieKey = "RaceDay.ApiCookie";  // where we keep it, server-side, in the MVC session

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IHttpContextAccessor _accessor;

    public RaceDayApi(HttpClient http, IHttpContextAccessor accessor)
    {
        _http = http;
        _accessor = accessor;
    }

    private ISession Session => _accessor.HttpContext!.Session;

    // ------------------------------------------------------------------ core plumbing

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, HttpContent? content = null)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };

        // Forward the user's API session so the API knows who is calling and in which role
        var cookie = Session.GetString(ApiCookieKey);
        if (!string.IsNullOrEmpty(cookie))
            request.Headers.TryAddWithoutValidation("Cookie", $"{ApiCookieName}={cookie}");

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ApiException(503, ApiException.DefaultMessage(503));
        }

        CaptureCookie(response);

        if (!response.IsSuccessStatusCode)
        {
            var error = await ApiException.FromResponseAsync(response);
            response.Dispose();
            throw error;
        }
        return response;
    }

    /// <summary>Keeps the API's (sliding) session cookie up to date; drops it when the API ends the session.</summary>
    private void CaptureCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var headers)) return;

        foreach (var header in headers)
        {
            if (!header.StartsWith(ApiCookieName + "=", StringComparison.Ordinal)) continue;

            var value = header[(ApiCookieName.Length + 1)..].Split(';')[0];
            if (string.IsNullOrEmpty(value)) Session.Remove(ApiCookieKey);
            else Session.SetString(ApiCookieKey, value);
        }
    }

    private async Task<T> GetAsync<T>(string url)
    {
        using var response = await SendAsync(HttpMethod.Get, url);
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task<T> SendJsonAsync<T>(HttpMethod method, string url, object body)
    {
        using var response = await SendAsync(method, url, JsonContent.Create(body, options: Json));
        return (await response.Content.ReadFromJsonAsync<T>(Json))!;
    }

    private async Task SendJsonAsync(HttpMethod method, string url, object? body = null)
    {
        using var response = await SendAsync(method, url, body is null ? null : JsonContent.Create(body, options: Json));
    }

    // ------------------------------------------------------------------ auth

    public async Task<AuthResponse> LoginAsync(string email, string password)
    {
        Session.Remove(ApiCookieKey);
        using var response = await SendAsync(HttpMethod.Post, "api/auth/login",
            JsonContent.Create(new { email, password }, options: Json));
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
    }

    public Task RegisterAsync(string fullName, string email, string password, string role) =>
        SendJsonAsync(HttpMethod.Post, "api/auth/register", new { fullName, email, password, role });

    public async Task LogoutAsync()
    {
        try { await SendJsonAsync(HttpMethod.Post, "api/auth/logout"); }
        catch (ApiException) { /* already logged out / API down - the local session is cleared regardless */ }
    }

    // ------------------------------------------------------------------ events

    public Task<List<EventDto>> GetEventsAsync(DateTime? date, string? location, string? eventType)
    {
        var query = new List<string>();
        if (date.HasValue) query.Add($"date={date.Value:yyyy-MM-dd}");
        if (!string.IsNullOrWhiteSpace(location)) query.Add($"location={Uri.EscapeDataString(location.Trim())}");
        if (!string.IsNullOrWhiteSpace(eventType)) query.Add($"eventType={Uri.EscapeDataString(eventType)}");
        return GetAsync<List<EventDto>>("api/events" + (query.Count > 0 ? "?" + string.Join('&', query) : ""));
    }

    public Task<EventDetailDto> GetEventAsync(int id) => GetAsync<EventDetailDto>($"api/events/{id}");
    public Task<List<OrganiserEventSummaryDto>> GetMyEventsAsync() => GetAsync<List<OrganiserEventSummaryDto>>("api/events/mine");
    public Task<EventDto> CreateEventAsync(object request) => SendJsonAsync<EventDto>(HttpMethod.Post, "api/events", request);
    public Task<EventDto> UpdateEventAsync(int id, object request) => SendJsonAsync<EventDto>(HttpMethod.Put, $"api/events/{id}", request);
    public Task DeleteEventAsync(int id) => SendJsonAsync(HttpMethod.Delete, $"api/events/{id}");

    // ------------------------------------------------------------------ banner images

    public async Task UploadBannerAsync(int eventId, IFormFile file)
    {
        await using var stream = file.OpenReadStream();
        using var form = new MultipartFormDataContent();
        var part = new StreamContent(stream);
        if (!string.IsNullOrWhiteSpace(file.ContentType))
            part.Headers.ContentType = MediaTypeHeaderValue.Parse(file.ContentType);
        form.Add(part, "file", Path.GetFileName(file.FileName));

        using var response = await SendAsync(HttpMethod.Post, $"api/events/{eventId}/banner", form);
    }

    public Task DeleteBannerAsync(int eventId) => SendJsonAsync(HttpMethod.Delete, $"api/events/{eventId}/banner");

    public async Task<BannerImage?> GetBannerAsync(int eventId)
    {
        try
        {
            using var response = await SendAsync(HttpMethod.Get, $"api/events/{eventId}/banner");
            var bytes = await response.Content.ReadAsByteArrayAsync();
            return new BannerImage(bytes, response.Content.Headers.ContentType?.MediaType ?? "image/jpeg");
        }
        catch (ApiException)
        {
            return null;    // no banner (or storage unavailable) -> the page shows its placeholder
        }
    }

    // ------------------------------------------------------------------ categories

    public Task<CategoryDto> CreateCategoryAsync(int eventId, object request) =>
        SendJsonAsync<CategoryDto>(HttpMethod.Post, $"api/events/{eventId}/categories", request);
    public Task<CategoryDto> UpdateCategoryAsync(int categoryId, object request) =>
        SendJsonAsync<CategoryDto>(HttpMethod.Put, $"api/categories/{categoryId}", request);
    public Task DeleteCategoryAsync(int categoryId) => SendJsonAsync(HttpMethod.Delete, $"api/categories/{categoryId}");

    // ------------------------------------------------------------------ enrolments

    public async Task<EnrolmentDto> EnrolAsync(int categoryId)
    {
        using var response = await SendAsync(HttpMethod.Post, $"api/categories/{categoryId}/enrol");
        return (await response.Content.ReadFromJsonAsync<EnrolmentDto>(Json))!;
    }

    public Task<List<EnrolmentDto>> GetMyEnrolmentsAsync() => GetAsync<List<EnrolmentDto>>("api/users/me/enrolments");
    public Task WithdrawAsync(int enrolmentId) => SendJsonAsync(HttpMethod.Delete, $"api/enrolments/{enrolmentId}");
    public Task<List<EnrolmentDto>> GetEventEnrolmentsAsync(int eventId) => GetAsync<List<EnrolmentDto>>($"api/events/{eventId}/enrolments");
    public Task SetEnrolmentStatusAsync(int enrolmentId, string status) =>
        SendJsonAsync(HttpMethod.Put, $"api/enrolments/{enrolmentId}/status", new { status });

    // ------------------------------------------------------------------ results

    public Task RecordResultAsync(int enrolmentId, TimeSpan finishTime, int? position, bool update) =>
        SendJsonAsync(update ? HttpMethod.Put : HttpMethod.Post, $"api/enrolments/{enrolmentId}/result",
            new { finishTime = finishTime.ToString("c"), finishPosition = position });

    public Task<List<MyResultDto>> GetMyResultsAsync() => GetAsync<List<MyResultDto>>("api/users/me/results");
}
