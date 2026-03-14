using System.Collections.Generic;
using LivingWorld.Core;

namespace LivingWorld.Generation;

public sealed class LegacyPlayerEntryOutcomeEvaluatorAdapter : ICandidateOutcomeEvaluator
{
    public bool ShouldSurfaceFocalSelection(World world, WorldGenerationSettings settings, bool allowEmergencyFallback, out List<string> rejectionReasons)
        => PlayerEntryOutcomeEvaluator.ShouldSurfaceFocalSelection(world, settings, allowEmergencyFallback, out rejectionReasons);
}
