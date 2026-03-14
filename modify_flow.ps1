 = [System.Collections.Generic.List[string]](Get-Content SIMULATION_FLOW.md)
 = .FindIndex({  -like 'Pass 1 through Pass 4*' })
if ( -lt 0) { throw 'start not found' }
 = @(
'These passes now run inside a clearer runtime scaffold: the simulation begins in a BootstrapWorldFrame, flows through the shared PrehistoryRunning pipeline (where the familiar biological/evolutionary/social descriptors now appear as user-facing subphase text), pauses in ReadinessCheckpoint states when evaluator logic inspects post-tick facts, and resolves into FocalSelection, ActivePlay, or GenerationFailure through explicit PrehistoryCheckpointOutcome results (ContinuePrehistory, EnterFocalSelection, ForceEnterFocalSelection, GenerationFailure).',
'Canonical events now retain explicit origin metadata, World stores a live-chronicle boundary marker, and renderer state is cleared before the watch loop begins so prehistory remains structured history instead of live chronicle noise.',
'The renderer also sanitizes the chronicle buffer itself to full historical lines, so summary fragments and stale selection text cannot survive the startup handoff.',
'That startup path now also tracks organic-vs-fallback diagnostics inside PrehistoryEvaluationSnapshot, including readiness reports, candidate diagnostics, rejection reasons, observer snapshots, and candidate-pool summaries, so reruns can precisely describe whether a start was healthy or rescued.',
'The latest startup-richness pass still tightens Phase B divergence texture and current-polity candidate differentiation so partners that make strong focal starts stay sharper than accidental byproducts.',
'The startup UI pass remains a second renderer contract: StartupProgressRenderer owns the console while PrehistoryRuntimePhase is still BootstrapWorldFrame, PrehistoryRunning, or ReadinessCheckpoint, and ChronicleWatchRenderer takes over only after the explicit FocalSelection or ActivePlay handoff, keeping the live chronicle clean.'
)
for ( = 0;  -lt .Count; ++) {
    [ + ] = []
}
Set-Content SIMULATION_FLOW.md -Value 
