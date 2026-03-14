namespace LivingWorld.Life;

public sealed class EvolutionaryLineage
{
    public int Id { get; }
    public int SpeciesId { get; set; }
    public int? ParentLineageId { get; set; }
    public int RootAncestorLineageId { get; set; }
    public int? OriginRegionId { get; set; }
    public int OriginYear { get; set; }
    public int? ExtinctionYear { get; set; }
    public int? ExtinctionMonth { get; set; }
    public LineageStage Stage { get; set; }
    public bool IsExtinct { get; set; }
    public string EcologyNiche { get; set; }
    public TrophicRole TrophicRole { get; set; }
    public string TraitProfileSummary { get; set; }
    public string HabitatAdaptationSummary { get; set; }
    public string AdaptationPressureSummary { get; set; }
    public SentienceCapabilityState SentienceCapability { get; set; }
    public int SentienceMilestoneYear { get; set; } = -1;
    public int CurrentPopulationRegions { get; set; }
    public int CurrentPopulationCount { get; set; }
    public int MutationEventCount { get; set; }
    public int MajorMutationEventCount { get; set; }
    public int SpeciationCount { get; set; }
    public int LocalExtinctionCount { get; set; }
    public int GlobalExtinctionCount { get; set; }
    public int MaxObservedDivergenceMilestone { get; set; }
    public int AncestryDepth { get; set; }
    public HashSet<int> DescendantLineageIds { get; } = [];
    public HashSet<int> AdaptedBiomeIds { get; } = [];

    public EvolutionaryLineage(int id, int speciesId, string ecologyNiche, TrophicRole trophicRole)
    {
        Id = id;
        SpeciesId = speciesId;
        RootAncestorLineageId = id;
        Stage = LineageStage.Primitive;
        EcologyNiche = ecologyNiche;
        TrophicRole = trophicRole;
        TraitProfileSummary = "baseline";
        HabitatAdaptationSummary = "ancestral";
        AdaptationPressureSummary = "mixed";
        SentienceCapability = SentienceCapabilityState.None;
    }
}
