using ComfoBox.Domain.Items;

namespace ComfoBox.Domain.Tests;

/// <summary>
/// Tests für den Item-Katalog.
/// </summary>
/// <remarks>
/// Der Katalog wird aus dem Legacy-Quelltext erzeugt
/// (<c>tools/generate-topics/generate-topics.mjs</c>). Die Tests sichern ab,
/// dass die Metadaten vollständig und korrekt übernommen wurden.
/// </remarks>
public sealed class CatalogTests
{
    private static readonly ItemCatalog Catalog = new();

    /// <summary>
    /// Die Gateway-Status-Topics sind reserviert (Phase 10, §5.4).
    /// Kein Katalog-Item darf denselben Pfad belegen.
    /// </summary>
    private static readonly string[] ReservedTopicPaths =
    [
        "Connected",
        "Version",
        "Special/NumberOfWritesPer24h",
        "Status/Health",
        "Status/Version",
        "Status/Uptime",
        "Status/StartedAt",
        "Status/Writes",
        "Status/WritesMax",
        "Status/Bacnet",
        "Status/Mqtt",
        "Status/LastError",
        "Status/Errors",
        "Status/Restart",
        "Status/Hardware/BufferTop",
        "Status/Hardware/BufferBottom",
        "Status/Hardware/CoolingBufferTop",
        "Status/Hardware/CoolingBufferBottom",
        "Status/Hardware/Solar",
        "Status/Hardware/HeatPumpSensors",
        "Status/Hardware/WarmWaterBottom",
        "Command/Restart",
    ];

    [Fact]
    public void Catalog_Umfasst_Exakt_194_Items()
    {
        // 127 V1-Items (eingefroren) + 67 V2-Items (Phase 10 ohne Energiemessung).
        Assert.Equal(194, Catalog.All.Count);
    }

    [Fact]
    public void V1_Und_V2_Teilen_Den_Katalog_Vollstaendig_Auf()
    {
        Assert.Equal(127, Catalog.V1.Count);
        Assert.Equal(67, Catalog.V2.Count);
        Assert.Equal(Catalog.All.Count, Catalog.V1.Count + Catalog.V2.Count);

        Assert.All(Catalog.V1, item => Assert.False(item.IsV2, $"{item.Id} ist V1, trägt aber IsV2."));
        Assert.All(Catalog.V2, item => Assert.True(item.IsV2, $"{item.Id} ist V2, trägt aber kein IsV2."));

        var v1Pfade = Catalog.V1.Select(i => i.TopicPath).ToHashSet(StringComparer.Ordinal);
        Assert.False(Catalog.V2.Any(item => v1Pfade.Contains(item.TopicPath)),
            "Ein V2-Item belegt denselben Topic-Pfad wie ein V1-Item.");
    }

    [Fact]
    public void Die_V2_Items_Verteilen_Sich_Auf_Vierzehn_Gruppen()
    {
        // Welle 2a: Zone, WarmWater, HeatPump, Buffer.
        // Welle 2b: Counters, Solar und das Command-Topic (ohne Energiemessung).
        // Welle 2c: sieben Config-Gruppen (nur mit Experten-Modus schreibbar).
        var gruppen = Catalog.V2.GroupBy(item => item.Group).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(14, gruppen.Count);
        Assert.Equal(4, gruppen["Status/Zone"]);
        Assert.Equal(4, gruppen["Status/WarmWater"]);
        Assert.Equal(13, gruppen["Status/HeatPump"]);
        Assert.Equal(5, gruppen["Status/Buffer"]);
        Assert.Equal(11, gruppen["Status/Counters"]);
        Assert.Equal(5, gruppen["Status/Solar"]);
        Assert.Equal(1, gruppen["Command"]);
        Assert.Equal(6, gruppen["Config/General"]);
        Assert.Equal(7, gruppen["Config/EnergyProducer"]);
        Assert.Equal(1, gruppen["Config/HeatingSystem"]);
        Assert.Equal(3, gruppen["Config/Warmwater"]);
        Assert.Equal(2, gruppen["Config/Cooling"]);
        Assert.Equal(4, gruppen["Config/Ventilation"]);
        Assert.Equal(1, gruppen["Config/Service"]);
    }

    [Fact]
    public void Alle_V2_Status_Items_Sind_Rein_Lesend()
    {
        // Bis auf das Command-Topic sind alle V2-Items reine Lesetopics.
        var statusItems = Catalog.V2
            .Where(item => item.Group.StartsWith("Status/", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(42, statusItems.Count);
        Assert.All(statusItems, item =>
        {
            Assert.True(item.IsReadOnly, $"{item.Id} ist ein V2-Lesetopic, aber nicht read-only.");
            Assert.False(item.IsWritable, $"{item.Id} ist schreibbar.");
        });
    }

    [Fact]
    public void Die_Energiemessung_Ist_Nicht_Teil_Des_Katalogs()
    {
        // Die ComfoBox Series 5 hat keinen Wärmemengenzähler. Die Items des
        // Handbuchs (§4.8) sind deshalb bewusst nicht im Katalog: sie liefern
        // dauerhaft 0 und wären in Home Assistant nur irreführend.
        Assert.DoesNotContain(Catalog.All, item => item.Group == "Status/Energy");

        foreach (var objectId in new[] { 3340, 3341, 3342, 3343, 3344, 3345, 3346, 3347, 3348, 3349, 3354, 3355, 3356, 3357 })
        {
            Assert.DoesNotContain(Catalog.All, item => item.BacnetObjectId == objectId);
        }
    }

    [Fact]
    public void Die_Vier_Typ_Parameter_Sind_Rein_Lesend_Und_Langsam()
    {
        // Sie deklarieren, welche Baugruppen die Anlage hat - daraus leitet
        // das Gateway ab, welche Items es überhaupt liest.
        (string TopicPath, int ObjectId, string Enum)[] erwartet =
        [
            ("Config/General/BufferType", 805, "BufferTypes"),
            ("Config/General/CoolingBufferType", 3918, "BufferTypes"),
            ("Config/General/SolarHydraulics", 807, "SolarHydraulicsTypes"),
            ("Config/General/HeatPumpSensor", 2800, "PresenceTypes"),
        ];

        foreach (var (topicPath, objectId, _) in erwartet)
        {
            var item = Catalog.FindByTopicPath(topicPath);

            Assert.NotNull(item);
            Assert.True(item!.IsV2, $"{topicPath} ist kein V2-Item.");
            Assert.Equal(objectId, item.BacnetObjectId);
            Assert.Equal(ItemValueKind.Enum, item.Kind);
            Assert.True(item.IsReadOnly, $"{topicPath} ist schreibbar.");
            Assert.Equal(RefreshPriority.Slow, item.Priority);
            Assert.True(HardwareRequirements.IsDetector(item.Id), $"{topicPath} ist kein Typ-Parameter.");
        }

        Assert.Same(ItemEnums.BufferTypes, Catalog.FindByTopicPath("Config/General/BufferType")!.EnumValues);
        Assert.Same(ItemEnums.BufferTypes, Catalog.FindByTopicPath("Config/General/CoolingBufferType")!.EnumValues);
        Assert.Same(ItemEnums.SolarHydraulicsTypes, Catalog.FindByTopicPath("Config/General/SolarHydraulics")!.EnumValues);
        Assert.Same(ItemEnums.PresenceTypes, Catalog.FindByTopicPath("Config/General/HeatPumpSensor")!.EnumValues);
    }

    [Fact]
    public void Die_Baugruppen_Verweisen_Auf_Vorhandene_Items()
    {
        // Das Gate liest und sperrt genau diese Ids - ein Tippfehler würde
        // dazu führen, dass ein Item nie gesperrt wird.
        var ids = Catalog.All.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var requirement in HardwareRequirements.All)
        {
            Assert.True(
                ids.Contains(requirement.DetectorItemId),
                $"{requirement.Key}: Typ-Parameter {requirement.DetectorItemId} fehlt im Katalog.");

            Assert.NotEmpty(requirement.ItemIds);
            Assert.NotEmpty(requirement.PresentValues);

            foreach (var itemId in requirement.ItemIds)
            {
                Assert.True(
                    ids.Contains(itemId),
                    $"{requirement.Key}: Item {itemId} fehlt im Katalog.");
            }
        }

        // Jedes Item gehört höchstens zu einer Baugruppe.
        var gated = HardwareRequirements.All.SelectMany(requirement => requirement.ItemIds).ToList();
        Assert.Equal(gated.Count, gated.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Die_Typ_Parameter_Werte_Stimmen_Mit_Dem_Handbuch()
    {
        Assert.Equal(3, ItemEnums.BufferTypes.Count);
        Assert.Equal("None", ItemEnums.BufferTypes[0]);
        Assert.Equal("OneSensor", ItemEnums.BufferTypes[1]);
        Assert.Equal("TwoSensors", ItemEnums.BufferTypes[11]);

        Assert.Equal(7, ItemEnums.SolarHydraulicsTypes.Count);
        Assert.Equal("None", ItemEnums.SolarHydraulicsTypes[0]);
        Assert.Equal("PumpToBuffer", ItemEnums.SolarHydraulicsTypes[1]);
        Assert.Equal("PumpWarmWaterThenBufferWithHeatExchanger", ItemEnums.SolarHydraulicsTypes[24]);

        Assert.Equal("NotInstalled", ItemEnums.PresenceTypes[0]);
        Assert.Equal("Installed", ItemEnums.PresenceTypes[1]);
    }

    [Fact]
    public void Die_Elf_Zaehler_Items_Sind_Langsam_Und_Kommen_Mit_ServiceHours()
    {
        var zaehler = Catalog.V2.Where(item => item.Group == "Status/Counters").ToList();

        Assert.Equal(11, zaehler.Count);
        Assert.All(zaehler, item => Assert.Equal(RefreshPriority.Slow, item.Priority));

        var service = Catalog.FindByTopicPath("Status/Counters/ServiceHours");
        Assert.NotNull(service);
        Assert.Equal(3366, service!.BacnetObjectId);
        Assert.Equal("h", service.Unit);

        // DP 3365 ist später Config/Service/Interval - kein zweites Status-Topic.
        Assert.DoesNotContain(Catalog.All,
            item => item.Group == "Status/Counters" && item.BacnetObjectId == 3365);
        Assert.DoesNotContain(Catalog.All, item => item.TopicPath == "Status/Counters/ServiceInterval");
    }

    [Fact]
    public void Es_Gibt_Genau_Fuenf_Solar_Items()
    {
        var solar = Catalog.V2.Where(item => item.Group == "Status/Solar").ToList();

        Assert.Equal(5, solar.Count);
        Assert.Equal(28, Catalog.FindByTopicPath("Status/Solar/CollectorTemperature")!.BacnetObjectId);
        Assert.Equal(264, Catalog.FindByTopicPath("Status/Solar/CollectorDeltaTemperature")!.BacnetObjectId);
        Assert.Equal(36, Catalog.FindByTopicPath("Status/Solar/EnergyTotal")!.BacnetObjectId);
        Assert.Equal(3911, Catalog.FindByTopicPath("Status/Solar/EnergyToday")!.BacnetObjectId);
    }

    [Fact]
    public void Das_Command_Topic_Ist_Nur_Mit_Experten_Modus_Schreibbar()
    {
        var item = Catalog.FindByTopicPath("Command/ResetHeatingCurveAdaption");

        Assert.NotNull(item);
        Assert.Equal(678, item!.BacnetObjectId);
        Assert.Equal("Command/ResetHeatingCurveAdaption", item.TopicPath);
        Assert.True(item.IsV2);
        Assert.True(item.RequiresExpertMode);
        Assert.True(item.IsWritable);
        Assert.False(item.IsReadOnly);
        Assert.Equal(0f, item.Min);
        Assert.Equal(1f, item.Max);
        Assert.False(item.IsWritableWithExpertMode(expertModeEnabled: false));
        Assert.True(item.IsWritableWithExpertMode(expertModeEnabled: true));
    }

    [Fact]
    public void Genau_21_V2_Items_Erfordern_Den_Experten_Modus()
    {
        // 20 Konfigurations-Topics (Welle 2c ohne MixerDirectOffset) plus das
        // Command-Topic. Die Typ-Parameter sind read-only und brauchen ihn nicht.
        var experten = Catalog.V2.Where(item => item.RequiresExpertMode).ToList();

        Assert.Equal(21, experten.Count);
        Assert.All(experten, item =>
        {
            Assert.True(item.IsWritable, $"{item.Id} ist nicht schreibbar.");
            Assert.False(item.IsWritableWithExpertMode(expertModeEnabled: false),
                $"{item.Id} ist ohne Experten-Modus schreibbar.");
            Assert.True(item.IsWritableWithExpertMode(expertModeEnabled: true),
                $"{item.Id} ist auch mit Experten-Modus nicht schreibbar.");

            var erlaubt = item.TopicPath.StartsWith("Config/", StringComparison.Ordinal)
                || item.TopicPath.StartsWith("Command/", StringComparison.Ordinal);
            Assert.True(erlaubt, $"{item.TopicPath} liegt weder unter Config/ noch unter Command/.");
        });
    }

    [Fact]
    public void Die_Zwanzig_Config_Items_Tragen_Ihre_Dokumentierten_Objekt_Ids()
    {
        (string TopicPath, int ObjectId)[] erwartet =
        [
            ("Config/General/CoolingEnabled", 3118),
            ("Config/General/PassiveCoolingHydraulics", 572),
            ("Config/EnergyProducer/SwitchingDifferenceStage2", 141),
            ("Config/EnergyProducer/RestartDelayStage2", 847),
            ("Config/EnergyProducer/BivalenceStage2", 803),
            ("Config/EnergyProducer/CondenserPumpPreRunningTime", 3234),
            ("Config/EnergyProducer/CondenserPumpPostRunningTime", 188),
            ("Config/EnergyProducer/FrostProtectionMode", 498),
            ("Config/EnergyProducer/FrostProtectionTemperature", 446),
            ("Config/HeatingSystem/SetPointNormalBelowOutdoor", 862),
            ("Config/Warmwater/Equipment", 117),
            ("Config/Warmwater/PowerSelection", 195),
            ("Config/Warmwater/SwitchOverDelay", 848),
            ("Config/Cooling/PassiveMode", 579),
            ("Config/Cooling/Release", 571),
            ("Config/Ventilation/FanControl", 3092),
            ("Config/Ventilation/SpeedStage1", 3224),
            ("Config/Ventilation/SpeedStage2", 3225),
            ("Config/Ventilation/SpeedStage3", 3226),
            ("Config/Service/Interval", 3365),
        ];

        Assert.Equal(20, erwartet.Length);

        // 20 schreibbare Config-Items plus die vier rein lesenden Typ-Parameter.
        Assert.Equal(24, Catalog.V2.Count(item => item.TopicPath.StartsWith("Config/", StringComparison.Ordinal)));

        foreach (var (topicPath, objectId) in erwartet)
        {
            var item = Catalog.FindByTopicPath(topicPath);

            Assert.NotNull(item);
            Assert.True(item!.IsV2, $"{topicPath} ist kein V2-Item.");
            Assert.Equal(objectId, item.BacnetObjectId);
            Assert.Equal(RefreshPriority.Secondary, item.Priority);
            Assert.True(item.RequiresExpertMode, $"{topicPath} erfordert keinen Experten-Modus.");
            Assert.True(item.IsWritable, $"{topicPath} ist nicht schreibbar.");
        }

        // DP 846 ist V1 (Config/EnergyComsumer/HeatPump/RestartDelay) und wird nicht verdoppelt.
        Assert.Null(Catalog.FindByTopicPath("Config/EnergyProducer/RestartDelayStage1"));
    }

    [Fact]
    public void Die_Auswahl_Parameter_Haben_Ihre_Erlaubten_Werte()
    {
        AssertAllowedValues("Config/General/CoolingEnabled", 0, 1);
        AssertAllowedValues("Config/General/PassiveCoolingHydraulics", 0, 1, 2, 3, 4, 9);
        AssertAllowedValues("Config/EnergyProducer/FrostProtectionMode", 0, 1, 2, 3, 10);
        AssertAllowedValues("Config/Warmwater/Equipment", 0, 1, 2);
        AssertAllowedValues("Config/Cooling/PassiveMode", 0, 1, 2);
        AssertAllowedValues("Config/Ventilation/FanControl", 0, 1, 2);
    }

    [Fact]
    public void FrostProtectionMode_Lehnt_Nicht_Dokumentierte_Werte_Ab()
    {
        var item = Catalog.FindByTopicPath("Config/EnergyProducer/FrostProtectionMode");

        Assert.NotNull(item);
        Assert.True(item!.IsInRange(5f));      // 5 liegt im Bereich 0 bis 10 ...
        Assert.False(item.IsValueAllowed(5f)); // ... ist aber kein erlaubter Wert.
        Assert.True(item.IsValueAllowed(10f));
    }

    [Fact]
    public void Config_Service_Interval_Ist_Ein_Experten_Wert_Ohne_Grenzen()
    {
        var item = Catalog.FindByTopicPath("Config/Service/Interval");

        Assert.NotNull(item);
        Assert.Equal(3365, item!.BacnetObjectId);
        Assert.Equal("h", item.Unit);
        Assert.Null(item.Min);
        Assert.Null(item.Max);
        Assert.Null(item.AllowedValues);
        Assert.True(item.IsV2);
        Assert.True(item.RequiresExpertMode);
        Assert.True(item.IsWritable);
    }

    private static void AssertAllowedValues(string topicPath, params int[] erwartet)
    {
        var item = Catalog.FindByTopicPath(topicPath);

        Assert.NotNull(item);
        Assert.NotNull(item!.AllowedValues);
        Assert.Equal(erwartet.Select(value => (float)value).ToArray(), item.AllowedValues!.ToArray());
    }

    [Fact]
    public void Die_Enum_Luecken_Der_V2_Zustaende_Bleiben_Erhalten()
    {
        // Befund A6: ein Wert, den das Gerät liefert, das Enum aber nicht kennt,
        // wird nicht auf 0 abgebildet. Deshalb sind die Lücken Vertrag und
        // dürfen nicht aufgefüllt werden.
        int[] zoneLuecken = [12, 15, 16, 17, 19, 20];
        foreach (var wert in zoneLuecken)
        {
            Assert.False(ItemEnums.ZoneOverrides.ContainsKey(wert),
                $"ZoneOverrides kennt den Wert {wert} - den liefert der Regler nicht.");
        }

        int[] warmWaterLuecken = [2, 4, 6, 7, 8, 11, 12];
        foreach (var wert in warmWaterLuecken)
        {
            Assert.False(ItemEnums.WarmWaterOverrides.ContainsKey(wert),
                $"WarmWaterOverrides kennt den Wert {wert} - den liefert der Regler nicht.");
        }

        Assert.Equal("None", ItemEnums.ZoneOverrides[0]);
        Assert.Equal("AdditionalHeaterOnly", ItemEnums.ZoneOverrides[22]);
        Assert.Equal("None", ItemEnums.WarmWaterOverrides[0]);
        Assert.Equal("AdditionalHeaterOnly", ItemEnums.WarmWaterOverrides[22]);
    }

    [Fact]
    public void Die_V2_Enum_Items_Nutzen_Die_Neuen_Enum_Werte()
    {
        var zoneOverride = Catalog.Find("status.zone.override");
        Assert.NotNull(zoneOverride);
        Assert.Equal(ItemValueKind.Enum, zoneOverride!.Kind);
        Assert.Same(ItemEnums.ZoneOverrides, zoneOverride.EnumValues);

        var warmWaterState = Catalog.Find("status.warm_water.state");
        Assert.NotNull(warmWaterState);
        Assert.Same(ItemEnums.WarmWaterStates, warmWaterState!.EnumValues);

        // HeatPumpStatusStage2 nutzt das bestehende Enum der Stufe 1.
        var stage2 = Catalog.Find("status.heat_pump.heat_pump_status_stage2");
        Assert.NotNull(stage2);
        Assert.Same(ItemEnums.HeatPumpStatusEnums, stage2!.EnumValues);
    }

    [Fact]
    public void Status_Zone_FlowTemperature_Traegt_Die_Dokumentierte_Objekt_Id()
    {
        var item = Catalog.Find("status.zone.flow_temperature");

        Assert.NotNull(item);
        Assert.Equal("Status/Zone/FlowTemperature", item!.TopicPath);
        Assert.Equal(14, item.BacnetObjectId);
        Assert.Equal(RefreshPriority.Primary, item.Priority);
        Assert.Equal("°C", item.Unit);
        Assert.Null(item.Min);
        Assert.Null(item.Max);
    }


    [Fact]
    public void Die_Statische_V1_Und_V2_Sicht_Entspricht_Dem_Katalog()
    {
        // Catalog.V1/Catalog.V2 sind die handgeschriebene Ergänzung zum
        // generierten Catalog.All - sie dürfen nicht auseinanderlaufen.
        Assert.Equal(Catalog.V1.Count, ComfoBox.Domain.Items.Catalog.V1.Count);
        Assert.Equal(Catalog.V2.Count, ComfoBox.Domain.Items.Catalog.V2.Count);
    }

    [Fact]
    public void Reservierte_Gateway_Topics_Sind_Keine_Items()
    {
        var reserviert = ReservedTopicPaths.ToHashSet(StringComparer.Ordinal);

        foreach (var item in Catalog.All)
        {
            Assert.False(reserviert.Contains(item.TopicPath),
                $"{item.Id} belegt den reservierten Gateway-Pfad {item.TopicPath}.");
        }
    }

    [Fact]
    public void Jedes_Item_Hat_Eine_Bacnet_Objekt_ID()
    {
        foreach (var item in Catalog.All)
        {
            Assert.True(item.BacnetObjectId > 0, $"{item.Id} hat keine gültige BACnet-Objekt-ID.");
        }
    }

    [Fact]
    public void Jedes_Item_Hat_Id_Gruppe_Und_Name()
    {
        foreach (var item in Catalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Id), "Ein Item hat keine Id.");
            Assert.False(string.IsNullOrWhiteSpace(item.Group), $"{item.Id} hat keine Gruppe.");
            Assert.False(string.IsNullOrWhiteSpace(item.Name), $"{item.Id} hat keinen Namen.");
            Assert.Equal($"{item.Group}/{item.Name}", item.TopicPath);
        }
    }

    [Fact]
    public void Ids_Und_Topic_Pfade_Sind_Eindeutig()
    {
        var ids = Catalog.All.Select(i => i.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());

        var paths = Catalog.All.Select(i => i.TopicPath).ToList();
        Assert.Equal(paths.Count, paths.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Item_Laesst_Sich_Ueber_Id_Und_Topic_Pfad_Finden()
    {
        var item = Catalog.Find("zone.set_point_normal");

        Assert.NotNull(item);
        Assert.Equal(62, item!.BacnetObjectId);
        Assert.Equal("Zone", item.Group);
        Assert.Equal("SetPointNormal", item.Name);
        Assert.Same(item, Catalog.FindByTopicPath("Zone/SetPointNormal"));
    }

    [Fact]
    public void Unbekanntes_Item_Wird_Nicht_GEfunden()
    {
        Assert.Null(Catalog.Find("gibt.es.nicht"));
        Assert.Null(Catalog.FindByTopicPath("Zone/GibtEsNicht"));
    }

    [Fact]
    public void Die_Grad_Einheit_Wird_Korrekt_Angegeben()
    {
        // Befund A5: im Altbestand steht die Einheit als "\u00b0C".
        var item = Catalog.Find("zone.set_point_normal");

        Assert.NotNull(item);
        Assert.Equal("°C", item!.Unit);
        Assert.Equal(5f, item.Min);
        Assert.Equal(30f, item.Max);
    }

    [Fact]
    public void Prioritaeten_Werden_Ausgewertet()
    {
        // Befund A15: im Altbestand wurden die Attribute gelesen und verworfen -
        // alle Items bekamen RefreshPriority.None.
        var prios = Catalog.All.GroupBy(i => i.Priority).ToDictionary(g => g.Key, g => g.Count());

        Assert.True(prios[RefreshPriority.Primary] > 0, "Es gibt keine primären Items.");
        Assert.True(prios[RefreshPriority.Secondary] > 0, "Es gibt keine sekundären Items.");
        Assert.True(prios[RefreshPriority.Once] > 0, "Es gibt keine einmaligen Items.");
        Assert.True(prios[RefreshPriority.Primary] + prios[RefreshPriority.Secondary] < Catalog.All.Count,
            "Mindestens ein Item trägt keine Priorität - das ist der A15-Fehler.");
    }

    [Theory]
    [InlineData("zone.operation_mode", RefreshPriority.Primary)]
    [InlineData("zone.party_time", RefreshPriority.Secondary)]
    [InlineData("zone.set_point_normal", RefreshPriority.Primary)]
    [InlineData("states.controller.controller_type", RefreshPriority.Once)]
    [InlineData("states.heat_pump.cooling_flow_temperature", RefreshPriority.Primary)]
    [InlineData("states.counters.heat_pump_operating_hours", RefreshPriority.Secondary)]
    public void Die_Prioritaet_Eines_Items_Stimmt_Mit_Dem_Altbestand_Ueberein(string id, RefreshPriority expected)
    {
        var item = Catalog.Find(id);

        Assert.NotNull(item);
        Assert.Equal(expected, item!.Priority);
    }

    [Fact]
    public void Enum_Items_Haben_Die_Exakten_Enum_Namen()
    {
        var operationMode = Catalog.Find("zone.operation_mode");
        Assert.NotNull(operationMode);
        Assert.Equal(ItemValueKind.Enum, operationMode!.Kind);
        Assert.Equal(
            new Dictionary<int, string>
            {
                [1] = "Manual",
                [2] = "Standby",
                [3] = "OnlyWarmwater",
                [4] = "NormalAndFrost",
                [5] = "NormalAndReduced",
                [6] = "Normal",
                [7] = "Reduced",
            },
            operationMode.EnumValues);

        var heatingCooling = Catalog.Find("zone.heating_cooling_mode");
        Assert.NotNull(heatingCooling);
        Assert.Equal(
            new Dictionary<int, string>
            {
                [1] = "Auto",
                [2] = "OnlyCooling",
                [3] = "OnlyHeating",
                [5] = "Neutral",
            },
            heatingCooling!.EnumValues);
    }

    [Fact]
    public void Enum_Namen_Mit_Umlaut_Und_Luecken_Werden_Korrekt_UEbernommen()
    {
        var status = Catalog.Find("states.heat_pump.heat_pump_status");
        Assert.NotNull(status);
        Assert.Equal(ItemValueKind.Enum, status!.Kind);

        // Die Werte 7 und 8 gibt es nicht - die Lücke muss erhalten bleiben.
        Assert.False(status.EnumValues.ContainsKey(7));
        Assert.False(status.EnumValues.ContainsKey(8));
        Assert.Equal("Wärmequellenstoerung", status.EnumValues[14]);
        Assert.Equal("VerdampferfuehlerDefekt", status.EnumValues[62]);
    }

    [Fact]
    public void Items_Mit_Experten_Modus_Sind_Nur_Mit_Experten_Modus_Schreibbar()
    {
        var expertenItems = Catalog.All.Where(i => i.RequiresExpertMode).ToList();

        Assert.NotEmpty(expertenItems);
        foreach (var item in expertenItems)
        {
            Assert.False(item.IsReadOnly, $"{item.Id} ist Expert-Mode-pflichtig, aber fest read-only.");
            Assert.True(item.IsWritable, $"{item.Id} ist nicht grundsätzlich schreibbar.");
            Assert.False(item.IsWritableWithExpertMode(expertModeEnabled: false),
                $"{item.Id} ist ohne Expert-Modus schreibbar.");
            Assert.True(item.IsWritableWithExpertMode(expertModeEnabled: true),
                $"{item.Id} ist mit Expert-Modus nicht schreibbar.");
        }
    }

    [Fact]
    public void ReadOnly_Items_Sind_Nie_Schreibbar()
    {
        var readOnly = Catalog.All.Where(i => i.IsReadOnly).ToList();

        Assert.NotEmpty(readOnly);
        foreach (var item in readOnly)
        {
            Assert.False(item.IsWritable);
            Assert.False(item.IsWritableWithExpertMode(expertModeEnabled: true));
        }
    }

    [Fact]
    public void Grenzen_Sind_Konsistent()
    {
        foreach (var item in Catalog.All.Where(i => i.Min is not null || i.Max is not null))
        {
            Assert.True(item.Min is null || item.Max is null || item.Min <= item.Max,
                $"{item.Id} hat Min > Max.");
        }
    }

    [Fact]
    public void Wert_Ausserhalb_Der_Grenzen_Wird_Abgewiesen()
    {
        var item = Catalog.Find("zone.set_point_normal");
        Assert.NotNull(item);

        Assert.True(item!.IsInRange(21.5f));
        Assert.True(item.IsInRange(5f));
        Assert.True(item.IsInRange(30f));
        Assert.False(item.IsInRange(4.9f));
        Assert.False(item.IsInRange(30.1f));
    }

    [Fact]
    public void Auswahl_Parameter_Akzeptieren_Nur_Die_Erlaubten_Werte()
    {
        // R2: der Entladeschutz lässt sich ausschliesslich mit 3 einschalten.
        var entladeschutz = Catalog.Find("config.energy_comsumer.warmwater.entladeschutz");
        Assert.NotNull(entladeschutz);
        Assert.True(entladeschutz!.IsInRange(3f));
        Assert.True(entladeschutz.IsValueAllowed(3f));
        Assert.True(entladeschutz.IsValueAllowed(0f));
        Assert.False(entladeschutz.IsValueAllowed(1f));
        Assert.False(entladeschutz.IsValueAllowed(2f));

        // R8: Y1 kennt neben 0/1 auch den PWM-Betrieb (11).
        var stoppedMode = Catalog.Find("config.primary_pump.stopped_mode");
        Assert.NotNull(stoppedMode);
        Assert.True(stoppedMode!.IsValueAllowed(11f));
        Assert.False(stoppedMode.IsValueAllowed(5f));

        // R9: Kühlen kennt 0/1/3 - der Wert 2 liegt im Bereich, ist aber ungültig.
        var cooling = Catalog.Find("config.energy_comsumer.cooling_zone.cooling");
        Assert.NotNull(cooling);
        Assert.True(cooling!.IsInRange(2f));
        Assert.False(cooling.IsValueAllowed(2f));
        Assert.True(cooling.IsValueAllowed(3f));
    }

    [Fact]
    public void Items_Ohne_Werteliste_Akzeptieren_Jeden_Wert_Im_Bereich()
    {
        var item = Catalog.Find("zone.set_point_normal");
        Assert.NotNull(item);

        Assert.Null(item!.AllowedValues);
        Assert.True(item.IsValueAllowed(17.25f));
    }

    [Fact]
    public void Enum_Name_Lasst_Sich_Zum_Zahlenwert_aufloesen()
    {
        var item = Catalog.Find("zone.operation_mode");
        Assert.NotNull(item);

        Assert.True(item!.TryResolveEnumValue("Normal", out var value));
        Assert.Equal(6, value);
        Assert.False(item.TryResolveEnumValue("GibtEsNicht", out _));
    }

    [Fact]
    public void Es_Gibt_Keine_DateValue_Und_Keine_CommandValue()
    {
        // Befund A20: die beiden Legacy-Werttypen sind tot und werden nicht
        // wiederbelebt.
        var verboten = new[] { "DateValue", "CommandValue" };

        var typen = typeof(ItemDefinition).Assembly.GetTypes()
            .Where(t => verboten.Contains(t.Name))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(typen);
    }

    [Fact]
    public void Die_Domain_Hat_Keine_Externen_Abhaengigkeiten()
    {
        // Layerregel: die Domäne ist framework-frei - sie darf **kein**
        // NuGet-Paket referenzieren.
        //
        // `netstandard` ist dabei **kein** fremdes Paket, sondern die
        // .NET-Standard-Fassade des Frameworks. Sie erscheint ausschließlich,
        // wenn der Test mit dem Coverage-Collector instrumentiert wird
        // (coverlet schreibt die instrumentierten Assemblies mit einem
        // Verweis auf netstandard2.0 um). Ohne Coverage referenziert die
        // Domäne nur `System.Runtime` und `System.Collections`.
        var erlaubt = new[] { "System", "netstandard", "ComfoBox.Domain" };

        var fremde = typeof(ItemDefinition).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => !erlaubt.Any(e => n.StartsWith(e, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(fremde);
    }
}
