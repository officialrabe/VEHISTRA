using System.Reflection;

namespace Vehistra.Application.Services;

/// <summary>Stellt die Programmversion bereit (aus der Assembly oder gesetzt durch den Host).</summary>
public sealed class ApplicationVersionProvider
{
    public ApplicationVersionProvider(string? version = null)
    {
        Version = version
            ?? Assembly.GetEntryAssembly()?
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion.Split('+')[0]
            ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
            ?? "1.0.0";
    }

    public string Version { get; }
}
