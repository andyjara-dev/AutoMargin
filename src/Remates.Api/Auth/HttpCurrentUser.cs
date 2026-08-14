using System.Security.Claims;
using Remates.Infrastructure.Persistence;

namespace Remates.Api.Auth;

/// <summary>
/// Lee la identidad del token de la petición en curso. Es lo que alimenta al interceptor de
/// auditoría sin que Infrastructure tenga que saber de HTTP.
/// </summary>
public sealed class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public long? UserId =>
        long.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? accessor.HttpContext?.User.FindFirstValue("sub"), out var id)
            ? id
            : null;

    public string? UserName =>
        accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Name)
        ?? accessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email);
}
