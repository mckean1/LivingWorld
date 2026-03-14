namespace LivingWorld.Core;

public sealed record StartupWorldAgeConfiguration(
    StartupWorldAgePreset Preset,
    int MinPrehistoryYears,
    int TargetPrehistoryYears,
    int MaxPrehistoryYears,
    double ReadinessStrictness,
    int CandidateCountTarget)
{
    public static StartupWorldAgeConfiguration ForPreset(StartupWorldAgePreset preset)
        => preset switch
        {
            StartupWorldAgePreset.YoungWorld => new StartupWorldAgeConfiguration(
                preset,
                MinPrehistoryYears: 450,
                TargetPrehistoryYears: 700,
                MaxPrehistoryYears: 950,
                ReadinessStrictness: 0.92,
                CandidateCountTarget: 3),
            StartupWorldAgePreset.AncientWorld => new StartupWorldAgeConfiguration(
                preset,
                MinPrehistoryYears: 950,
                TargetPrehistoryYears: 1400,
                MaxPrehistoryYears: 1850,
                ReadinessStrictness: 1.06,
                CandidateCountTarget: 6),
            _ => new StartupWorldAgeConfiguration(
                StartupWorldAgePreset.StandardWorld,
                MinPrehistoryYears: 700,
                TargetPrehistoryYears: 1000,
                MaxPrehistoryYears: 1400,
                ReadinessStrictness: 1.00,
                CandidateCountTarget: 5)
        };
}
