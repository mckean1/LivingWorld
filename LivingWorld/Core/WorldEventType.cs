namespace LivingWorld.Core;

public static class WorldEventType
{
    public const string Migration = "migration";
    public const string SettlementFounded = "settlement_founded";
    public const string SettlementConsolidated = "settlement_consolidated";
    public const string KnowledgeDiscovered = "knowledge_discovered";
    public const string Famine = "famine";
    public const string FoodStress = "food_stress";
    public const string Harvest = "harvest";
    public const string PopulationChanged = "population_changed";
    public const string Fragmentation = "fragmentation";
    public const string StageChanged = "stage_changed";
    public const string PolityCollapsed = "polity_collapsed";
    public const string WorldEvent = "world_event";
}
