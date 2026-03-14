using System.Collections.Generic;
using LivingWorld.Core;

namespace LivingWorld.Generation;

public interface ICandidateOutcomeEvaluator
{
    bool ShouldSurfaceFocalSelection(World world, WorldGenerationSettings settings, bool allowEmergencyFallback, out List<string> rejectionReasons);
}
