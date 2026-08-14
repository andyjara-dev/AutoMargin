using System.ComponentModel.DataAnnotations;

namespace Remates.Api.Contracts;

public sealed class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed class RefreshRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    /// <summary>
    /// La política real la aplica Identity y sus mensajes son los que ve el usuario.
    /// Este mínimo solo evita el viaje al servidor en el caso obvio.
    /// </summary>
    [Required, MinLength(10)]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed record AuthenticatedUser(
    long Id,
    string Email,
    string? FullName,
    IReadOnlyList<string> Roles);

public sealed record AuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    string RefreshToken,
    AuthenticatedUser User);
