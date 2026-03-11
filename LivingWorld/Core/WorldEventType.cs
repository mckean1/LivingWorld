namespace LivingWorld.Core;

public static class WorldEventType
{
    public const string Migration = "migration";
    public const string MigrationPressure = "migration_pressure";
    public const string StarvationRisk = "starvation_risk";
    public const string SettlementFounded = "settlement_founded";
    public const string SettlementConsolidated = "settlement_consolidated";
    public const string SettlementStabilized = "settlement_stabilized";
    public const string KnowledgeDiscovered = "knowledge_discovered";
    public const string LearnedAdvancement = "learned_advancement";
    public const string Famine = "famine";
    public const string FoodStress = "food_stress";
    public const string FoodStabilized = "food_stabilized";
    public const string Harvest = "harvest";
    public const string CultivationExpanded = "cultivation_expanded";
    public const string PopulationChanged = "population_changed";
    public const string SchismRisk = "schism_risk";
    public const string Fragmentation = "fragmentation";
    public const string PolityFounded = "polity_founded";
    public const string LocalTension = "local_tension";
    public const string StageChanged = "stage_changed";
    public const string PolityCollapsed = "polity_collapsed";
    public const string FocusHandoffFragmentation = "focus_handoff_fragmentation";
    public const string FocusHandoffCollapse = "focus_handoff_collapse";
    public const string FocusLineageContinued = "focus_lineage_continued";
    public const string FocusLineageExtinctFallback = "focus_lineage_extinct_fallback";
    public const string TradeTransfer = "trade_transfer";
    public const string TradeLinkStarted = "trade_link_started";
    public const string TradeRelief = "trade_relief";
    public const string TradeDependency = "trade_dependency";
    public const string TradeLinkCollapsed = "trade_link_collapsed";
    public const string SpeciesPopulationEstablished = "species_population_established";
    public const string LocalSpeciesExtinction = "local_species_extinction";
    public const string GlobalSpeciesExtinction = "global_species_extinction";
    public const string PredatorPressure = "predator_pressure";
    public const string PreyCollapse = "prey_collapse";
    public const string HuntingSuccess = "hunting_success";
    public const string HuntingDisaster = "hunting_disaster";
    public const string DangerousPreyKilledHunters = "dangerous_prey_killed_hunters";
    public const string ToxicFoodDiscovered = "toxic_food_discovered";
    public const string EdibleSpeciesDiscovered = "edible_species_discovered";
    public const string OverhuntingPressure = "overhunting_pressure";
    public const string EcosystemCollapse = "ecosystem_collapse";
    public const string LegendaryHunt = "legendary_hunt";
    public const string WorldEvent = "world_event";
}
