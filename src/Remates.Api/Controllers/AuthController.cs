using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Remates.Api.Contracts;
using Remates.Infrastructure.Auth;
using Remates.Infrastructure.Entities;
using Remates.Infrastructure.Persistence;

namespace Remates.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public sealed class AuthController(
    UserManager<AppUser> userManager,
    ITokenService tokenService,
    RematesDbContext db,
    IOptions<JwtOptions> jwtOptions,
    TimeProvider timeProvider,
    ILogger<AuthController> logger) : ControllerBase
{
    private readonly JwtOptions _jwt = jwtOptions.Value;

    /// <summary>Autentica con correo y contraseña, y devuelve el par de tokens.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        // Mismo mensaje para usuario inexistente y contraseña incorrecta: no revelar qué correos existen.
        if (user is null || !user.IsActive || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            logger.LogWarning("Intento de acceso fallido para {Email}", request.Email);
            return Unauthorized(new ProblemDetails { Title = "Credenciales inválidas." });
        }

        var response = await IssueTokensAsync(user, ct);

        user.LastLoginAt = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(ct);

        return Ok(response);
    }

    /// <summary>Canjea un refresh token por un par nuevo. El token usado queda revocado.</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromBody] RefreshRequest request, CancellationToken ct)
    {
        var hash = tokenService.HashRefreshToken(request.RefreshToken);
        var now = timeProvider.GetUtcNow();

        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored?.User is null || stored.RevokedAt is not null || stored.ExpiresAt <= now)
            return Unauthorized(new ProblemDetails { Title = "Refresh token inválido o expirado." });

        if (!stored.User.IsActive)
            return Unauthorized(new ProblemDetails { Title = "La cuenta está desactivada." });

        // Rotación: cada canje invalida el token anterior.
        stored.RevokedAt = now;

        var response = await IssueTokensAsync(stored.User, ct);
        return Ok(response);
    }

    /// <summary>Revoca el refresh token entregado. El access token expira solo.</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var hash = tokenService.HashRefreshToken(request.RefreshToken);

        var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is not null && stored.RevokedAt is null)
        {
            stored.RevokedAt = timeProvider.GetUtcNow();
            await db.SaveChangesAsync(ct);
        }

        return NoContent();
    }

    /// <summary>Datos del usuario autenticado. Lo usa el frontend al recargar la página.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType<AuthenticatedUser>(StatusCodes.Status200OK)]
    public async Task<ActionResult<AuthenticatedUser>> Me()
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Unauthorized();

        var roles = await userManager.GetRolesAsync(user);
        return Ok(new AuthenticatedUser(user.Id, user.Email!, user.FullName, [.. roles]));
    }

    private async Task<AuthResponse> IssueTokensAsync(AppUser user, CancellationToken ct)
    {
        var roles = await userManager.GetRolesAsync(user);
        var pair = tokenService.Issue(user, roles);
        var now = timeProvider.GetUtcNow();

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenService.HashRefreshToken(pair.RefreshToken),
            CreatedAt = now,
            ExpiresAt = now.AddDays(_jwt.RefreshTokenDays)
        });

        await db.SaveChangesAsync(ct);

        return new AuthResponse(
            pair.AccessToken,
            pair.AccessTokenExpiresAt,
            pair.RefreshToken,
            new AuthenticatedUser(user.Id, user.Email!, user.FullName, [.. roles]));
    }
}
