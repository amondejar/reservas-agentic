using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Reservas.Domain;

namespace Reservas.Infrastructure;

public static class PersistenciaPostgres
{
    public static IServiceCollection AddPersistenciaPostgres(this IServiceCollection services, string conexion)
    {
        // Neon apaga la base de datos sin uso: la primera conexión puede tardar o fallar, y se reintenta.
        services.AddDbContext<ReservasDbContext>(options =>
            options.UseNpgsql(CadenaPostgres.Normalizar(conexion), npgsql => npgsql.EnableRetryOnFailure(3)));
        services.AddScoped<IRecursoRepository, RecursosEf>();
        services.AddScoped<IReservaRepository, ReservasEf>();
        services.AddScoped<IUnidadDeTrabajo, UnidadEf>();

        // Las claves que firman las confirmaciones de Keel, en la base de datos: sobreviven a los reinicios del contenedor.
        services.AddDataProtection()
            .SetApplicationName("reservas-agentic")
            .PersistKeysToDbContext<ReservasDbContext>();
        return services;
    }

    /// <summary>
    /// Crea las tablas si la base de datos está vacía, y la restricción que impide dos reservas confirmadas solapadas del
    /// mismo recurso. Para una demo basta; en un proyecto real, usa migraciones de EF Core.
    /// </summary>
    public static async Task PrepararPostgresAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ReservasDbContext>();
        await db.Database.EnsureCreatedAsync(ct);
        await db.Database.ExecuteSqlRawAsync("""
            CREATE EXTENSION IF NOT EXISTS btree_gist;
            DO $$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'reservas_sin_solapes') THEN
                    ALTER TABLE reservas ADD CONSTRAINT reservas_sin_solapes
                        EXCLUDE USING gist (recurso_id WITH =, tstzrange(inicio, fin) WITH &&)
                        WHERE (estado = 'Confirmada');
                END IF;
            END $$;
            """, ct);
    }
}

/// <summary>
/// Acepta la cadena de conexión como la da Neon (postgresql://usuario:clave@host/base?sslmode=require) o en el
/// formato de Npgsql (Host=...;Database=...).
/// </summary>
public static class CadenaPostgres
{
    public static string Normalizar(string conexion)
    {
        conexion = conexion.Trim();
        if (!conexion.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !conexion.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            return conexion;

        var uri = new Uri(conexion);
        var usuario = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(usuario[0]),
            Password = usuario.Length > 1 ? Uri.UnescapeDataString(usuario[1]) : null,
            SslMode = SslMode.Require,
        };

        foreach (var parametro in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var (clave, valor) = parametro.Split('=', 2) is [var k, var v] ? (k, Uri.UnescapeDataString(v)) : (parametro, "");
            switch (clave.ToLowerInvariant())
            {
                case "sslmode" when Enum.TryParse<SslMode>(valor.Replace("-", ""), ignoreCase: true, out var ssl):
                    builder.SslMode = ssl;
                    break;
                case "channel_binding" when Enum.TryParse<ChannelBinding>(valor, ignoreCase: true, out var binding):
                    builder.ChannelBinding = binding;
                    break;
            }
        }

        return builder.ConnectionString;
    }
}
