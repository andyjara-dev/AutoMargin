using Microsoft.EntityFrameworkCore;
using Remates.Domain.Parameters;
using Remates.Infrastructure.Entities;
using Remates.Infrastructure.Persistence;

namespace Remates.Api.Services;

/// <summary>
/// Entrega el conjunto de parámetros activo. Devuelve también su id, porque cada análisis
/// persistido debe registrar con qué parámetros se calculó.
/// </summary>
public sealed class ParameterProvider(RematesDbContext db)
{
    public async Task<(ParameterSet Set, AnalysisParameters Parameters)> GetActiveAsync(CancellationToken ct)
    {
        var set = await db.ParameterSets
            .Include(s => s.Values)
            .FirstOrDefaultAsync(s => s.IsActive, ct)
            ?? throw new InvalidOperationException(
                "No hay un conjunto de parámetros activo. Revisa el sembrado inicial de la base.");

        return (set, ParameterSetMapper.ToParameters(set.Values));
    }
}
