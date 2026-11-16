using ComfoBox.Domain.Items;
using ComfoBox.Gateway.Bacnet;

namespace ComfoBox.Gateway.Tests;

/// <summary>
/// Tests für den Poll-Plan: welche Priorität wird in welchem Zyklus gelesen?
/// </summary>
public sealed class PollPlanTests
{
    [Fact]
    public void Der_Poll_Plan_Liest_Primaere_Items_Zuerst()
    {
        var primaer = new List<ItemDefinition> { Item(1), Item(2) };
        var sekundaer = new List<ItemDefinition> { Item(3) };
        var ohne = new List<ItemDefinition> { Item(4) };

        var zyklus = PollPlan.ForCycle(primaer, sekundaer, ohne, [], cycle: 0, secondaryFactor: 5, slowFactor: 60);

        Assert.Equal([1, 2, 4, 3], zyklus.Select(i => i.BacnetObjectId));
    }

    [Fact]
    public void Sekundaere_Items_Werden_Nur_Jeden_Fuenften_Zyklus_Gelesen()
    {
        var primaer = new List<ItemDefinition> { Item(1) };
        var sekundaer = new List<ItemDefinition> { Item(3) };

        for (var cycle = 0; cycle < 10; cycle++)
        {
            var zyklus = PollPlan.ForCycle(primaer, sekundaer, [], [], cycle, secondaryFactor: 5, slowFactor: 60);
            var enthaeltSekundaere = zyklus.Any(i => i.BacnetObjectId == 3);

            Assert.Equal(cycle % 5 == 0, enthaeltSekundaere);
        }
    }

    [Fact]
    public void Slow_Ist_Nur_Im_Ersten_Und_Dann_Jedem_Nten()
    {
        var primaer = new List<ItemDefinition> { Item(1) };
        var sekundaer = new List<ItemDefinition> { Item(3) };
        var ohne = new List<ItemDefinition> { Item(4) };
        var langsam = new List<ItemDefinition> { Item(5) };

        for (var cycle = 0; cycle < 15; cycle++)
        {
            var zyklus = PollPlan.ForCycle(primaer, sekundaer, ohne, langsam, cycle, secondaryFactor: 5, slowFactor: 3);
            var enthaeltLangsame = zyklus.Any(i => i.BacnetObjectId == 5);

            Assert.Equal(cycle % 3 == 0, enthaeltLangsame);
        }

        // Reihenfolge im ersten Zyklus: primär, ohne Priorität, langsam, sekundär.
        var erster = PollPlan.ForCycle(primaer, sekundaer, ohne, langsam, cycle: 0, secondaryFactor: 5, slowFactor: 3);
        Assert.Equal([1, 4, 5, 3], erster.Select(i => i.BacnetObjectId));
    }

    [Fact]
    public void SlowFactor_Eins_Liest_Jeden_Zyklus()
    {
        var langsam = new List<ItemDefinition> { Item(5) };

        for (var cycle = 0; cycle < 5; cycle++)
        {
            var zyklus = PollPlan.ForCycle([], [], [], langsam, cycle, secondaryFactor: 5, slowFactor: 1);

            Assert.Equal([5], zyklus.Select(i => i.BacnetObjectId));
        }
    }

    [Fact]
    public void SlowFactor_Kleiner_Eins_Wird_Abgewiesen()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PollPlan.ForCycle([], [], [], [], cycle: 0, secondaryFactor: 5, slowFactor: 0));
    }

    private static ItemDefinition Item(int objectId) => new()
    {
        Id = $"item.{objectId}",
        Group = "Zone",
        Name = objectId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        BacnetObjectId = objectId,
        Kind = ItemValueKind.Analog,
    };
}
