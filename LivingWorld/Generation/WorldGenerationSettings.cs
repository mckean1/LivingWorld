using LivingWorld.Core;

namespace LivingWorld.Generation;

public sealed class WorldGenerationSettings
{
    public StartupWorldAgePreset StartupWorldAgePreset { get; init; } = StartupWorldAgePreset.StandardWorld;

    public int RegionCount { get; init; } = 36;

    public int InitialSpeciesCount { get; init; } = 7;

    public int InitialPolityCount { get; init; } = 0;

    public int ContinentWidth { get; init; } = 6;

    public int ContinentHeight { get; init; } = 6;

    public int MinimumStartingPolityRegionSpacing { get; init; } = 1;

    public int HomelandSupportRadius { get; init; } = 1;

    public int MinimumAccessibleHomelandSupportSpecies { get; init; } = 2;

    public bool StartPolitiesWithHomeSettlements { get; init; } = true;

    public int StartingSettlementAgeYears { get; init; } = 0;

    public int PhaseAMinimumBootstrapMonths { get; init; } = 18;

    public int PhaseAMaximumBootstrapMonths { get; init; } = 60;

    public int PhaseBMinimumBootstrapYears { get; init; } = 180;

    public int PhaseBMaximumBootstrapYears { get; init; } = 900;

    public int PhaseCMinimumBootstrapYears { get; init; } = 120;

    public int PhaseCMaximumBootstrapYears { get; init; } = 480;

    public double MinimumPhaseAOccupiedRegionPercentage { get; init; } = 0.78;

    public double MinimumPhaseAProducerCoverage { get; init; } = 0.88;

    public double MinimumPhaseAConsumerCoverage { get; init; } = 0.52;

    public double MinimumPhaseAPredatorCoverage { get; init; } = 0.14;

    public int MinimumPhaseBMatureLineageCount { get; init; } = 3;

    public int MinimumPhaseBSpeciationCount { get; init; } = 2;

    public int MinimumPhaseBExtinctLineageCount { get; init; } = 1;

    public int MinimumPhaseBAncestryDepth { get; init; } = 1;

    public int MinimumPhaseBMatureRegionalDivergenceCount { get; init; } = 4;

    public int MinimumPhaseBSentienceCapableLineageCount { get; init; } = 1;

    public int MinimumPhaseBStableRegionCount { get; init; } = 12;

    public double PhaseBMatureRegionalDivergenceThreshold { get; init; } = 1.60;

    public int SentientActivationMinimumPopulation { get; init; } = 70;

    public double SentientActivationMinimumSupport { get; init; } = 0.40;

    public int BootstrapSentienceCapableLineageTarget { get; init; } = 3;

    public int SentientActivationMaximumIndependentGroupsPerLineage { get; init; } = 3;

    public int SentientTrajectoryMinimumRegionalSeparation { get; init; } = 2;

    public double SocialNeutralGrowthPoint { get; init; } = 0.46;

    public double SocialMaximumAnnualGrowthRate { get; init; } = 0.16;

    public double SocialMaximumAnnualDeclineRate { get; init; } = 0.26;

    public double PersistentGroupCohesionThreshold { get; init; } = 0.35;

    public int PersistentGroupContinuityYears { get; init; } = 4;

    public int SocietyFormationContinuityYears { get; init; } = 6;

    public double SocietyFormationIdentityThreshold { get; init; } = 0.28;

    public int SettlementIntentReturnThreshold { get; init; } = 2;

    public double SettlementFoundingPressureThreshold { get; init; } = 0.48;

    public int PolityFormationMinimumPopulation { get; init; } = 135;

    public int PolityFormationMinimumKnowledgeCount { get; init; } = 4;

    public double PolityFormationComplexityThreshold { get; init; } = 0.46;

    public int MinimumPhaseCSentientGroupCount { get; init; } = 2;

    public int MinimumPhaseCPersistentSocietyCount { get; init; } = 2;

    public int MinimumPhaseCSettlementCount { get; init; } = 2;

    public int MinimumPhaseCViableSettlementCount { get; init; } = 2;

    public int MinimumPhaseCPolityCount { get; init; } = 2;

    public int MinimumPhaseCViableFocalCandidateCount { get; init; } = 2;

    public double MinimumPhaseCAveragePolityAge { get; init; } = 5.0;

    public double MinimumPhaseCHistoricalEventDensity { get; init; } = 0.18;

    public int ReadinessEvaluationIntervalYears { get; init; } = 20;

    public int MinimumViablePlayerEntryCandidates { get; init; } = 2;

    public int CandidateMinimumPopulation { get; init; } = 90;

    public int CandidateMinimumPolityAgeYears { get; init; } = 3;

    public double CandidateMinimumViabilityScore { get; init; } = 0.60;

    public double EmergencyCandidateMinimumViabilityScore { get; init; } = 0.42;

    public double CandidateMinimumSettlementViability { get; init; } = 0.42;

    public double CandidateMaximumCollapseSeverity { get; init; } = 0.88;

    public double CandidateDiversitySpeciesBonus { get; init; } = 0.08;

    public double CandidateDiversityLineageBonus { get; init; } = 0.07;

    public double CandidateDiversityRegionBonus { get; init; } = 0.07;

    public double CandidateDiversityBiomeBonus { get; init; } = 0.06;

    public double CandidateDiversitySubsistenceBonus { get; init; } = 0.05;

    public double CandidateDiversitySettlementProfileBonus { get; init; } = 0.05;

    public double CandidateDiversityConditionBonus { get; init; } = 0.04;

    public double CandidateScoreWeightSurvivalStrength { get; init; } = 0.24;

    public double CandidateScoreWeightContinuityDepth { get; init; } = 0.18;

    public double CandidateScoreWeightSpatialIdentity { get; init; } = 0.18;

    public double CandidateScoreWeightAgencyAndInternalOrganization { get; init; } = 0.16;

    public double CandidateScoreWeightExternalEntanglement { get; init; } = 0.10;

    public double CandidateScoreWeightStrategicOpportunity { get; init; } = 0.14;

    public double CandidateScoreWeightFragilityPenalty { get; init; } = 0.42;

    public int CandidatePoolSeedCount { get; init; } = 1;

    public int CandidatePoolSoftCapPerMaturityBand { get; init; } = 2;

    public int CandidatePoolSoftCapPerArchetypeBucket { get; init; } = 2;

    public int CandidatePoolSoftCapPerHomeBucket { get; init; } = 2;

    public int CandidatePoolSoftCapPerStabilityMode { get; init; } = 2;

    public double CandidateNearDuplicateSimilarityThreshold { get; init; } = 0.72;

    public int MaxStartupRegenerationAttempts { get; init; } = 4;

    public double MinimumBiologicalReadinessFloor { get; init; } = 0.52;

    public int MinimumHealthyCandidateCount { get; init; } = 2;

    public double MinimumHealthyCandidateScore { get; init; } = 0.64;

    public int MaximumEmergencyFallbackCandidatesToSurface { get; init; } = 1;

    public bool AllowSingleFallbackCandidateSelection { get; init; } = false;

    public bool AllowPhaseCFallbackCivilizationalSeeding { get; init; } = false;
}
