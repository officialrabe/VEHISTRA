using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Vehistra.Infrastructure.Persistence;

/// <summary>
/// Wird ausschliesslich von den Entwicklungswerkzeugen (dotnet ef) verwendet, um
/// Migrationen zu erzeugen. Zur Laufzeit kommt der Kontext aus dem DI-Container.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<VehistraDbContext>
{
    public VehistraDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("FUHRPARK_DESIGNTIME_CONNECTION")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=VehistraDB;Trusted_Connection=True;TrustServerCertificate=True";

        var options = new DbContextOptionsBuilder<VehistraDbContext>()
            .UseSqlServer(connectionString, sql => sql.MigrationsAssembly(typeof(VehistraDbContext).Assembly.FullName))
            .Options;

        return new VehistraDbContext(options);
    }
}
