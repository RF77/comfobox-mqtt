using Microsoft.Extensions.Configuration;

namespace ComfoBox.Gateway.Configuration;

/// <summary>
/// Die Konfigurationsquelle der Optionen des Home-Assistant-Add-ons.
/// </summary>
/// <remarks>
/// <see cref="AddOnOptionsProvider"/> ist ein <see cref="IConfigurationProvider"/>
/// - die Quelle ist nur der Kleber, mit dem der Provider in den
/// Konfigurations-Baum gehängt wird.
/// </remarks>
public sealed class AddOnOptionsSource : IConfigurationSource
{
    /// <summary>
    /// Erzeugt die Quelle.
    /// </summary>
    /// <param name="provider">Der Provider, der die Optionen liest.</param>
    public AddOnOptionsSource(AddOnOptionsProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        Provider = provider;
    }

    /// <summary>
    /// Der Provider, der die Optionen liest.
    /// </summary>
    public AddOnOptionsProvider Provider { get; }

    /// <inheritdoc />
    public IConfigurationProvider Build(IConfigurationBuilder builder) => Provider;
}
