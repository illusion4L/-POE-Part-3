using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IPasswordHasher _hasher;

    public AuthController(RaceDayDbContext db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    /// <summary>Registers a new account as an Organiser or a Participant.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register(RegisterRequest req)
    {
        var roleName = AppRoles.All.FirstOrDefault(r =>
            string.Equals(r, req.Role?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (roleName is null)
        {
            ModelState.AddModelError(nameof(req.Role), "Role must be 'Organiser' or 'Participant'.");
            return ValidationProblem(ModelState);
        }

        var email = req.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Problem(title: "Email is already registered.", statusCode: StatusCodes.Status409Conflict);

        var role = await _db.Roles.FirstAsync(r => r.RoleName == roleName);
        var (hash, salt) = _hasher.Hash(req.Password);

        var user = new User
        {
            FullName = req.FullName.Trim(),
            Email = email,
            PasswordHash = hash,
            PasswordSalt = salt,
            RoleId = role.RoleId
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created,
            new AuthResponse { UserId = user.UserId, FullName = user.FullName, Role = role.RoleName });
    }

    /// <summary>Logs in and starts a session (cookie) holding the user's id and role.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginRequest req)
    {
        var email = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Email == email);

        if (user is null || !_hasher.Verify(req.Password, user.PasswordHash, user.PasswordSalt))
            return Problem(title: "Invalid email or password.", statusCode: StatusCodes.Status401Unauthorized);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role.RoleName)
        };
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return Ok(new AuthResponse { UserId = user.UserId, FullName = user.FullName, Role = user.Role.RoleName });
    }

    /// <summary>Ends the current session.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
