using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Vehistra.Infrastructure;
using Vehistra.Infrastructure.Persistence;

namespace Vehistra.Migrations.Sqlite;

/// <summary>
/// Wird ausschliesslich von "dotnet ef" verwendet, um die Migrationen fuer
/// den Solo-Platz zu erzeugen. Zur Laufzeit kommt der Kontext aus dem
/// DI-Container.
/// </summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<VehistraDbContext>
{
    public VehistraDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<VehistraDbContext>()
            .UseSqlite(
                "Data Source=entwurfszeit.db",
                sqlite => sqlite.MigrationsAssembly(DependencyInjection.SqliteMigrationsAssembly))
            .Options;

        return new VehistraDbContext(options);
    }
}
