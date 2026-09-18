using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fuhrpark.Infrastructure.Persistence;

/// <summary>
/// Wird ausschliesslich von den Entwicklungswerkzeugen (dotnet ef) verwendet, um
/// Migrationen zu erzeugen. Zur Laufzeit kommt der Kontext aus dem DI-Container.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<FuhrparkDbContext>
{
    public FuhrparkDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("FUHRPARK_DESIGNTIME_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=FuhrparkDB;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<FuhrparkDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(FuhrparkDbContext).Assembly.FullName))
            .Options;

        return new FuhrparkDbContext(options);
    }
}
