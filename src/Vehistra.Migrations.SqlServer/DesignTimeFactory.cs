using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Vehistra.Infrastructure;
using Vehistra.Infrastructure.Persistence;

namespace Vehistra.Migrations.SqlServer;

/// <summary>
/// Wird ausschliesslich von "dotnet ef" verwendet, um die Migrationen fuer
/// Microsoft SQL Server zu erzeugen. Zur Laufzeit kommt der Kontext aus dem
/// DI-Container.
/// </summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<VehistraDbContext>
{
    public VehistraDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<VehistraDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\MSSQLLocalDB;Database=VehistraDB;Trusted_Connection=True;TrustServerCertificate=True",
                sql => sql.MigrationsAssembly(DependencyInjection.SqlServerMigrationsAssembly))
            .Options;

        return new VehistraDbContext(options);
    }
}
