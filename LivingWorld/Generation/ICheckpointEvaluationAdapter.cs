using System.Collections.Generic;
using LivingWorld.Core;

namespace LivingWorld.Generation;

public interface ICheckpointEvaluationAdapter
{
    void Evaluate(World world, bool allowEmergencyFallback, IReadOnlyList<string>? regenerationReasons);
}
