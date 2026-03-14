using LivingWorld.Advancement;
using LivingWorld.Core;
using LivingWorld.Economy;
using LivingWorld.Life;
using LivingWorld.Map;
using LivingWorld.Societies;
using LivingWorld.Systems;
using Xunit;

namespace LivingWorld.Tests;

public sealed class MaterialEconomySystemTests
{
    [Fact]
    public void Settlement_InitializesMaterialStockpiles_ForEveryMaterialType()
    {
        Settlement settlement = new(1, 7, 0, "Stonefen");

        foreach (MaterialType materialType in Enum.GetValues<MaterialType>())
        {
            Assert.True(settlement.MaterialStockpiles.ContainsKey(materialType));
            Assert.Equal(0.0, settlement.GetMaterialStockpile(materialType));
            Assert.Equal(MaterialPressureState.Stable, settlement.MaterialPressureStates[materialType]);
        }
    }

    [Fact]
    public void MonthlyMaterialUpdate_ExtractsAndProducesMaterials_FromRegionalAbundance()
    {
        World world = CreateWorld();
        Region region = world.Regions[0];
        region.WoodAbundance = 0.90;
        region.StoneAbundance = 0.84;
        region.ClayAbundance = 0.88;
        region.FiberAbundance = 0.82;
        region.SaltAbundance = 0.74;
        region.CopperOreAbundance = 0.62;

        Polity polity = CreatePolityWithCapabilities(world, 7, "River Clan", 0);
        Settlement settlement = polity.Settlements[0];
        settlement.FoodRequired = 36;

        MaterialEconomySystem system = new();
        system.UpdateMonthlyMaterials(world);

        Assert.True(settlement.GetMaterialStockpile(MaterialType.Wood) > 0);
        Assert.True(settlement.GetMaterialStockpile(MaterialType.SimpleTools) > 0);
        Assert.True(settlement.GetMaterialStockpile(MaterialType.Pottery) > 0);
        Assert.True(settlement.GetMaterialStockpile(MaterialType.PreservedFood) > 0);
        Assert.True(settlement.ToolProductionTier >= 1);
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.MaterialDiscovered);
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.ProductionStarted);
    }

    [Fact]
    public void MaterialRedistribution_MovesCriticalSurplus_WithinSamePolity()
    {
        World world = CreateWorld();
        world.Regions.Add(new Region(1, "Hill Reach"));
        world.Regions[0].AddConnection(1);
        world.Regions[1].AddConnection(0);

        Polity polity = CreatePolityWithCapabilities(world, 7, "River Clan", 0);
        Settlement sender = polity.Settlements[0];
        Settlement receiver = polity.AddSettlement(1, "Hill Camp");
        sender.FoodRequired = 40;
        receiver.FoodRequired = 40;
        sender.SetMaterialTargetReserve(MaterialType.SimpleTools, 10);
        receiver.SetMaterialTargetReserve(MaterialType.SimpleTools, 10);
        sender.AddMaterial(MaterialType.SimpleTools, 28);
        receiver.AddMaterial(MaterialType.SimpleTools, 1);
        sender.CalculateMaterialPressure(MaterialType.SimpleTools);
        receiver.CalculateMaterialPressure(MaterialType.SimpleTools);

        MaterialEconomySystem system = new();
        system.UpdateMonthlyMaterials(world);

        Assert.True(receiver.GetMaterialStockpile(MaterialType.SimpleTools) > 1);
        Assert.True(polity.MaterialMovedThisYear[MaterialType.SimpleTools] > 0);
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.MaterialConvoySent);
    }

    [Fact]
    public void FoodConsumption_UsesPreservedFood_ToReduceShortage()
    {
        World world = CreateWorld();
        Polity polity = CreatePolityWithCapabilities(world, 7, "River Clan", 0);
        Settlement settlement = polity.Settlements[0];
        polity.FoodStores = 6;
        polity.FoodNeededThisMonth = 14;
        settlement.AddMaterial(MaterialType.PreservedFood, 5);

        FoodSystem system = new();
        system.ConsumeFood(world);

        Assert.Equal(11, polity.FoodConsumedThisMonth, 3);
        Assert.Equal(3, polity.FoodShortageThisMonth, 3);
        Assert.Equal(0, settlement.GetMaterialStockpile(MaterialType.PreservedFood), 3);
    }

    [Fact]
    public void RepeatedProduction_BecomesSettlementSpecialization()
    {
        World world = CreateWorld();
        Region region = world.Regions[0];
        region.ClayAbundance = 0.92;
        region.WoodAbundance = 0.48;
        region.StoneAbundance = 0.40;
        region.FiberAbundance = 0.32;

        Polity polity = CreatePolityWithCapabilities(world, 7, "River Clan", 0);
        Settlement settlement = polity.Settlements[0];
        settlement.FoodRequired = 30;
        settlement.EstablishedMonths = 36;

        MaterialEconomySystem system = new();
        RunMaterialMonths(world, polity, system, months: 8);

        Assert.Contains(SettlementSpecializationTag.PotteryTradition, settlement.SpecializationTags);
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.SettlementSpecialized);
    }

    [Fact]
    public void YoungSettlement_DoesNotEmitIdentityMilestoneTooEarly()
    {
        World world = CreateWorld();
        Region region = world.Regions[0];
        region.ClayAbundance = 0.94;
        region.SaltAbundance = 0.82;

        Polity polity = CreatePolityWithCapabilities(world, 7, "River Clan", 0);
        Settlement settlement = polity.Settlements[0];
        settlement.FoodRequired = 30;
        settlement.EstablishedMonths = 6;

        MaterialEconomySystem system = new();
        RunMaterialMonths(world, polity, system, months: 8);

        Assert.DoesNotContain(world.Events, evt => evt.Type == WorldEventType.SettlementSpecialized && evt.SettlementId == settlement.Id);
        Assert.DoesNotContain(world.Events, evt => evt.Type == WorldEventType.TradeGoodEstablished && evt.SettlementId == settlement.Id);
    }

    [Fact]
    public void BriefFluctuation_DoesNotEmitVisibleIdentityMilestone()
    {
        World world = CreateWorld();
        Region region = world.Regions[0];
        region.ClayAbundance = 0.96;
        region.SaltAbundance = 0.88;

        Polity polity = CreatePolityWithCapabilities(world, 7, "River Clan", 0);
        Settlement settlement = polity.Settlements[0];
        settlement.FoodRequired = 36;
        settlement.EstablishedMonths = 48;

        MaterialEconomySystem system = new();
        RunMaterialMonths(world, polity, system, months: 2);

        Assert.DoesNotContain(world.Events, evt => evt.Type == WorldEventType.SettlementSpecialized && evt.SettlementId == settlement.Id);
        Assert.DoesNotContain(world.Events, evt => evt.Type == WorldEventType.TradeGoodEstablished && evt.SettlementId == settlement.Id);
    }

    [Fact]
    public void MultipleMaterialShortages_KeepStructuredDetail_ButEmitSingleGroupedCrisis()
    {
        World world = CreateWorld();
        Region region = world.Regions[0];
        region.WoodAbundance = 0.0;
        region.StoneAbundance = 0.0;
        region.ClayAbundance = 0.0;
        region.FiberAbundance = 0.0;
        region.SaltAbundance = 0.0;
        region.CopperOreAbundance = 0.0;
        region.IronOreAbundance = 0.0;

        Polity polity = CreatePolityWithCapabilities(world, 7, "River Clan", 0);
        Settlement settlement = polity.Settlements[0];
        settlement.FoodRequired = 48;
        polity.FoodStores = 0;
        polity.FoodNeededThisMonth = 48;

        MaterialEconomySystem system = new();
        system.UpdateMonthlyMaterials(world);

        Assert.True(world.Events.Count(evt => evt.Type == WorldEventType.MaterialConvoyFailed) >= 3);
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.MaterialCrisisStarted && evt.SettlementId == settlement.Id);
        Assert.Equal(1, world.Events.Count(evt => evt.Type == WorldEventType.MaterialCrisisStarted && evt.SettlementId == settlement.Id));
    }

    [Fact]
    public void MonthlyMaterialUpdate_BuildsNeedValueAndOpportunitySignals()
    {
        World world = CreateWorld();
        Region region = world.Regions[0];
        region.ClayAbundance = 0.92;
        region.FiberAbundance = 0.35;
        region.SaltAbundance = 0.74;

        Polity polity = CreatePolityWithCapabilities(world, 7, "River Clan", 0);
        Settlement settlement = polity.Settlements[0];
        settlement.FoodRequired = 42;
        polity.FoodStores = 10;
        polity.FoodNeededThisMonth = 42;

        MaterialEconomySystem system = new();
        system.UpdateMonthlyMaterials(world);

        Assert.True(settlement.MaterialNeedPressures[MaterialType.PreservedFood] > 0.50);
        Assert.True(settlement.MaterialValueScores[MaterialType.PreservedFood] > settlement.MaterialValueScores[MaterialType.Wood]);
        Assert.True(settlement.MaterialOpportunityScores[MaterialType.Pottery] > settlement.MaterialOpportunityScores[MaterialType.Textiles]);
    }

    [Fact]
    public void DemandAndOpportunityShiftProductionTowardSupportedGoods()
    {
        World world = CreateWorld();
        Region region = world.Regions[0];
        region.ClayAbundance = 0.95;
        region.FiberAbundance = 0.28;
        region.SaltAbundance = 0.82;

        Polity polity = CreatePolityWithCapabilities(world, 7, "River Clan", 0);
        Settlement settlement = polity.Settlements[0];
        settlement.FoodRequired = 40;
        polity.FoodStores = 90;
        polity.FoodNeededThisMonth = 40;

        MaterialEconomySystem system = new();
        for (int month = 0; month < 4; month++)
        {
            system.UpdateMonthlyMaterials(world);
        }

        Assert.True(settlement.MaterialProducedThisYear[MaterialType.Pottery] > settlement.MaterialProducedThisYear[MaterialType.Textiles]);
        Assert.True(settlement.MaterialProductionFocusScores[MaterialType.Pottery] > settlement.MaterialProductionFocusScores[MaterialType.Textiles]);
    }

    [Fact]
    public void SingleMonthPressureSpike_DoesNotImmediatelyThrashProductionFocus()
    {
        World world = CreateWorld();
        Region region = world.Regions[0];
        region.ClayAbundance = 0.94;
        region.FiberAbundance = 0.20;

        Polity polity = CreatePolityWithCapabilities(world, 7, "River Clan", 0);
        Settlement settlement = polity.Settlements[0];
        settlement.FoodRequired = 34;
        settlement.DominantProductionFocusMaterial = MaterialType.SimpleTools;

        MaterialEconomySystem system = new();
        system.UpdateMonthlyMaterials(world);

        Assert.Equal(MaterialType.SimpleTools, settlement.DominantProductionFocusMaterial);
        Assert.DoesNotContain(world.Events, evt => evt.Type == WorldEventType.ProductionFocusShifted);
    }

    [Fact]
    public void EconomyTransitions_EmitHighValueAndTradeGoodSignals()
    {
        World world = CreateWorld();
        Region region = world.Regions[0];
        region.ClayAbundance = 0.96;
        region.SaltAbundance = 0.88;

        Polity polity = CreatePolityWithCapabilities(world, 7, "River Clan", 0);
        Settlement settlement = polity.Settlements[0];
        settlement.FoodRequired = 48;
        settlement.EstablishedMonths = 48;
        polity.FoodStores = 8;
        polity.FoodNeededThisMonth = 48;

        MaterialEconomySystem system = new();
        RunMaterialMonths(world, polity, system, months: 30);

        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.MaterialHighlyValued && evt.SettlementId == settlement.Id);
        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.TradeGoodEstablished && evt.SettlementId == settlement.Id);
    }

    [Fact]
    public void MatureSettlement_CanStillEarnIdentityMilestoneInYearZero()
    {
        World world = CreateWorld(startingYear: 0, startingMonth: 1);
        Region region = world.Regions[0];
        region.ClayAbundance = 0.96;
        region.SaltAbundance = 0.88;

        Polity polity = CreatePolityWithCapabilities(world, 7, "River Clan", 0);
        Settlement settlement = polity.Settlements[0];
        settlement.FoodRequired = 42;
        settlement.EstablishedMonths = 60;

        MaterialEconomySystem system = new();
        RunMaterialMonths(world, polity, system, months: 10);

        Assert.Contains(world.Events, evt =>
            evt.SettlementId == settlement.Id
            && evt.Type is WorldEventType.SettlementSpecialized or WorldEventType.TradeGoodEstablished
            && evt.Year == 0);
    }

    [Fact]
    public void DurableIdentityEvent_EventuallyAppearsAfterSustainedProof()
    {
        World world = CreateWorld();
        Region region = world.Regions[0];
        region.WoodAbundance = 0.96;
        region.SaltAbundance = 0.70;

        Polity polity = CreatePolityWithCapabilities(world, 7, "River Clan", 0);
        Settlement settlement = polity.Settlements[0];
        settlement.FoodRequired = 30;
        settlement.EstablishedMonths = 60;

        MaterialEconomySystem system = new();
        RunMaterialMonths(world, polity, system, months: 18);

        Assert.Contains(world.Events, evt =>
            evt.Type == WorldEventType.SettlementSpecialized
            && evt.SettlementId == settlement.Id
            && evt.Metadata.TryGetValue("identityPersistenceMonths", out string? persistence)
            && int.Parse(persistence) >= 6);
    }

    [Fact]
    public void RelatedIdentityEvents_DoNotStackBackToBackWithoutLongerEscalation()
    {
        World world = CreateWorld();
        Region region = world.Regions[0];
        region.ClayAbundance = 0.96;
        region.SaltAbundance = 0.88;

        Polity polity = CreatePolityWithCapabilities(world, 7, "River Clan", 0);
        Settlement settlement = polity.Settlements[0];
        settlement.FoodRequired = 40;
        settlement.EstablishedMonths = 48;
        polity.FoodStores = 20;
        polity.FoodNeededThisMonth = 40;

        MaterialEconomySystem system = new();
        RunMaterialMonths(world, polity, system, months: 10);

        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.SettlementSpecialized && evt.SettlementId == settlement.Id);
        Assert.DoesNotContain(world.Events, evt => evt.Type == WorldEventType.TradeGoodEstablished && evt.SettlementId == settlement.Id);

        RunMaterialMonths(world, polity, system, months: 12);

        Assert.Contains(world.Events, evt => evt.Type == WorldEventType.TradeGoodEstablished && evt.SettlementId == settlement.Id);
    }

    private static World CreateWorld(int startingYear = 120, int startingMonth = 6)
    {
        World world = new(new WorldTime(startingYear, startingMonth));
        world.Regions.Add(new Region(0, "Green Barrow")
        {
            Fertility = 0.78,
            WaterAvailability = 0.72,
            WoodAbundance = 0.60,
            StoneAbundance = 0.54,
            ClayAbundance = 0.58,
            FiberAbundance = 0.56,
            SaltAbundance = 0.30,
            CopperOreAbundance = 0.18,
            IronOreAbundance = 0.10
        });
        world.Species.Add(new Species(1, "Humans", 0.8, 0.7) { IsSapient = true });
        return world;
    }

    private static Polity CreatePolityWithCapabilities(World world, int polityId, string name, int regionId)
    {
        Polity polity = new(polityId, name, 1, regionId, 120)
        {
            FoodStores = 180,
            FoodNeededThisMonth = 30
        };
        polity.EstablishFirstSettlement(regionId, $"{name} Hearth");
        polity.LearnAdvancement(AdvancementId.Fire);
        polity.LearnAdvancement(AdvancementId.StoneTools);
        polity.LearnAdvancement(AdvancementId.SeasonalPlanning);
        polity.LearnAdvancement(AdvancementId.FoodStorage);
        polity.LearnAdvancement(AdvancementId.BasicConstruction);
        polity.LearnAdvancement(AdvancementId.CraftSpecialization);
        polity.LearnAdvancement(AdvancementId.Agriculture);
        world.Polities.Add(polity);
        return polity;
    }

    private static void RunMaterialMonths(World world, Polity polity, MaterialEconomySystem system, int months)
    {
        for (int month = 0; month < months; month++)
        {
            polity.AdvanceSettlementMonths();
            system.UpdateMonthlyMaterials(world);
            world.Time.AdvanceOneMonth();
        }
    }
}
