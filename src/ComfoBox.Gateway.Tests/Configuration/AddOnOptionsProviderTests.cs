using System.Text.Json;
using ComfoBox.Gateway.Configuration;
using Microsoft.Extensions.Configuration;

namespace ComfoBox.Gateway.Tests.Configuration;

/// <summary>
/// Die Tests des Options-Mappings des Home-Assistant-Add-ons (Aufgabe 8b.2).
/// </summary>
public sealed class AddOnOptionsProviderTests
{
    /// <summary>
    /// Jede Option des Add-ons landet in ihrem Gateway-Schlüssel.
    /// </summary>
    [Fact]
    public void Jede_Option_Wird_Abgebildet()
    {
        var options = Json("""
        {
          "device": "/dev/serial/by-id/usb-Prolific-if00-port0",
          "baudrate": 57600,
          "base_topic": "ComfoBox2",
          "broker_host": "broker.local",
          "broker_port": 8883,
          "broker_username": "comfobox",
          "broker_password": "geheim",
          "use_tls": true,
          "enable_discovery": false,
          "log_level": "Debug"
        }
        """);

        var target = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        AddOnOptionsProvider.Map(options, target);

        Assert.Equal("/dev/serial/by-id/usb-Prolific-if00-port0", target["Bacnet:Port"]);
        Assert.Equal("57600", target["Bacnet:Baudrate"]);
        Assert.Equal("ComfoBox2", target["Gateway:BaseTopic"]);
        Assert.Equal("broker.local", target["Mqtt:Brokers:0:Host"]);
        Assert.Equal("8883", target["Mqtt:Brokers:0:Port"]);
        Assert.Equal("comfobox", target["Mqtt:Brokers:0:Username"]);
        Assert.Equal("geheim", target["Mqtt:Brokers:0:Password"]);
        Assert.Equal("true", target["Mqtt:Brokers:0:UseTls"]);
        Assert.Equal("false", target["Mqtt:EnableDiscovery"]);
        Assert.Equal("Debug", target["Logging:Level"]);
    }

    /// <summary>
    /// Eine fehlende Datei ist **kein** Fehler (manueller Betrieb).
    /// </summary>
    [Fact]
    public void Eine_Fehlende_Datei_Ist_Kein_Fehler()
    {
        var path = Path.Combine(Path.GetTempPath(), "comfobox-options-" + Guid.NewGuid().ToString("N") + ".json");
        var warnings = new List<string>();

        var provider = new AddOnOptionsProvider(path, null, warnings.Add);
        provider.Load();

        Assert.Empty(provider.Values);
        Assert.Empty(warnings);
    }

    /// <summary>
    /// Leere Werte werden ignoriert - der Supervisor schreibt **alle**
    /// Optionen, auch die nicht benutzten.
    /// </summary>
    [Fact]
    public void Leere_Werte_Werden_Ignoriert()
    {
        var options = Json("""
        {
          "device": "",
          "base_topic": "",
          "broker_host": "",
          "broker_username": "",
          "broker_password": "",
          "log_level": ""
        }
        """);

        var target = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        AddOnOptionsProvider.Map(options, target);

        Assert.False(target.ContainsKey("Bacnet:Port"));
        Assert.False(target.ContainsKey("Gateway:BaseTopic"));
        Assert.False(target.ContainsKey("Mqtt:Brokers:0:Host"));
        Assert.False(target.ContainsKey("Mqtt:Brokers:0:Username"));
        Assert.False(target.ContainsKey("Mqtt:Brokers:0:Password"));
        Assert.False(target.ContainsKey("Logging:Level"));
    }

    /// <summary>
    /// Ohne Broker dürfen die Broker-Optionen **nicht** abgebildet werden -
    /// sonst entstünde ein Broker ohne Host und der Gateway würde nicht
    /// starten (ValidateOnStart).
    /// </summary>
    [Fact]
    public void Ohne_Broker_Werden_Die_Broker_Optionen_Ignoriert()
    {
        var options = Json("""
        {
          "broker_host": "",
          "broker_port": 1883,
          "use_tls": true,
          "enable_discovery": true
        }
        """);

        var target = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        AddOnOptionsProvider.Map(options, target);

        Assert.False(target.ContainsKey("Mqtt:Brokers:0:Host"));
        Assert.False(target.ContainsKey("Mqtt:Brokers:0:Port"));
        Assert.False(target.ContainsKey("Mqtt:Brokers:0:UseTls"));

        // Die Discovery ist keine Broker-Option - sie bleibt erhalten.
        Assert.Equal("true", target["Mqtt:EnableDiscovery"]);
    }

    /// <summary>
    /// Gibt es bereits einen Broker (aus der config.json), werden die
    /// Broker-Optionen trotzdem übernommen.
    /// </summary>
    [Fact]
    public void Ein_Vorhandener_Broker_Wird_Uebernommen()
    {
        var options = Json("""
        {
          "broker_host": "",
          "broker_port": 8883,
          "broker_username": "comfobox"
        }
        """);

        var target = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        AddOnOptionsProvider.Map(
            options,
            target,
            key => key == "Mqtt:Brokers:0:Host" ? "broker.local" : null);

        Assert.False(target.ContainsKey("Mqtt:Brokers:0:Host"));
        Assert.Equal("8883", target["Mqtt:Brokers:0:Port"]);
        Assert.Equal("comfobox", target["Mqtt:Brokers:0:Username"]);
    }

    /// <summary>
    /// Ein kaputter Wert wird gemeldet und ignoriert.
    /// </summary>
    [Fact]
    public void Ein_Kaputter_Wert_Wird_Gemeldet_Und_Ignoriert()
    {
        var options = Json("""
        {
          "baudrate": "sehr schnell",
          "broker_host": "broker.local",
          "use_tls": "vielleicht"
        }
        """);

        var warnings = new List<string>();
        var target = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        AddOnOptionsProvider.Map(options, target, null, warnings.Add);

        Assert.False(target.ContainsKey("Bacnet:Baudrate"));
        Assert.Equal(2, warnings.Count);
        Assert.Contains(warnings, w => w.Contains("baudrate", StringComparison.Ordinal));
        Assert.Contains(warnings, w => w.Contains("use_tls", StringComparison.Ordinal));
    }

    /// <summary>
    /// Eine kaputte Datei darf den Gateway nicht stoppen.
    /// </summary>
    [Fact]
    public void Eine_Kaputte_Datei_Stoert_Den_Betrieb_Nicht()
    {
        var path = Path.Combine(Path.GetTempPath(), "comfobox-options-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, "{ keine gültige JSON-Datei ");

        try
        {
            var warnings = new List<string>();

            var provider = new AddOnOptionsProvider(path, null, warnings.Add);
            provider.Load();

            Assert.Empty(provider.Values);
            Assert.Single(warnings);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// Die Optionen des Add-ons überschreiben die <c>config.json</c> **und**
    /// die Umgebungsvariablen.
    /// </summary>
    /// <remarks>
    /// Der Test ist bewusst **nicht hermetisch** im Sinne „keine
    /// Umgebungsvariablen": er **setzt** sie sogar selbst. Genau das ist der
    /// Kern der Aufgabe – das Image setzt <c>COMFOBOX_Bacnet__Port</c> als
    /// Vorgabe, und die Portwahl des Add-ons muss stärker sein.
    /// </remarks>
    [Fact]
    public void Die_Optionen_Ueberschreiben_Die_Config_Json_Und_Die_Umgebung()
    {
        using var directory = new TemporaryDirectory();

        // Die config.json (Assistent oder SPK).
        File.WriteAllText(
            Path.Combine(directory.Path, "config.json"),
            """
            {
              "Bacnet": { "Port": "COM3", "Baudrate": 38400 },
              "Gateway": { "BaseTopic": "ComfoBox" }
            }
            """);

        // Die Optionen des Add-ons.
        var optionsPath = Path.Combine(directory.Path, "options.json");
        File.WriteAllText(
            optionsPath,
            """
            {
              "device": "/dev/ttyUSB0",
              "baudrate": 57600,
              "base_topic": "ComfoBox2"
            }
            """);

        // Die Vorgabe des Images – die Optionen des Add-ons müssen stärker sein.
        using var dataDirectory = new EnvironmentVariable("COMFOBOX_Gateway__DataDirectory", directory.Path);
        using var port = new EnvironmentVariable("COMFOBOX_Bacnet__Port", "COM_TEST");
        using var optionsVariable = new EnvironmentVariable("COMFOBOX_Gateway__AddOnOptionsPath", optionsPath);

        var configuration = new ConfigurationManager();

        GatewayConfiguration.AddGatewaySources(configuration, directory.Path);

        // Die options.json schlägt die config.json …
        Assert.Equal("/dev/ttyUSB0", configuration["Bacnet:Port"]);
        Assert.Equal("57600", configuration["Bacnet:Baudrate"]);
        Assert.Equal("ComfoBox2", configuration["Gateway:BaseTopic"]);
    }

    /// <summary>
    /// Die Optionen des Add-ons überschreiben die Umgebungsvariablen – auch
    /// dann, wenn die <c>config.json</c> gar nicht existiert.
    /// </summary>
    [Fact]
    public void Die_Optionen_Ueberschreiben_Die_Umgebungsvariablen()
    {
        using var directory = new TemporaryDirectory();

        var optionsPath = Path.Combine(directory.Path, "options.json");
        File.WriteAllText(optionsPath, """{ "device": "/dev/ttyUSB0" }""");

        using var dataDirectory = new EnvironmentVariable("COMFOBOX_Gateway__DataDirectory", directory.Path);
        using var port = new EnvironmentVariable("COMFOBOX_Bacnet__Port", "COM_TEST");
        using var optionsVariable = new EnvironmentVariable("COMFOBOX_Gateway__AddOnOptionsPath", optionsPath);

        var configuration = new ConfigurationManager();

        GatewayConfiguration.AddGatewaySources(configuration, directory.Path);

        Assert.Equal(optionsPath, configuration["Gateway:AddOnOptionsPath"]);
        Assert.Equal("/dev/ttyUSB0", configuration["Bacnet:Port"]);
    }

    /// <summary>
    /// Liest ein JSON-Objekt.
    /// </summary>
    private static JsonElement Json(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Ein temporäres Verzeichnis, das beim Testende wieder entfernt wird.
    /// </summary>
    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "comfobox-options-" + Guid.NewGuid().ToString("N"));

            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (IOException)
            {
                // Der Inhalt ist gesperrt - das stört den Test nicht.
            }
        }
    }

    /// <summary>
    /// Eine Umgebungsvariable, die beim Testende wieder zurückgesetzt wird.
    /// </summary>
    /// <remarks>
    /// Ohne diese Hilfe lecken <c>COMFOBOX_*</c> aus der Shell in den Test –
    /// und der Test wird von der Umgebung abhängig.
    /// </remarks>
    private sealed class EnvironmentVariable : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        public EnvironmentVariable(string name, string? value)
        {
            _name = name;
            _previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose() => Environment.SetEnvironmentVariable(_name, _previous);
    }
}
