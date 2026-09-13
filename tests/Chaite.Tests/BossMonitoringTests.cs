using System;
using System.Linq;
using Chaite.Core;
using Mono.Cecil;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void BossMonitoringDoesNotOwnControls()
        {
            var controller = new EncounterController(3);
            var observation = new EncounterObservation { StartAuthorized = true, PlayerLife = 400 };
            var armed = controller.ArmMonitoring(observation);
            Equal(SessionState.Monitoring, armed.Current);
            Equal(AudioCue.MonitorArmed, armed.Cue);
            False(armed.ApplyControls);
            False(controller.IsControlling);
            True(controller.IsSessionActive);
            observation.PlayerLife = 250;
            for (var i = 0; i < 2000; i++)
            {
                var update = controller.Update(observation);
                False(update.ApplyControls);
                Equal(AudioCue.None, update.Cue);
                Equal(SessionState.Monitoring, update.Current);
            }
            Equal(AudioCue.None, controller.ArmMonitoring(observation).Cue);
            controller.Cancel();
            False(controller.IsSessionActive);
            controller.ReturnToIdle();
            Equal(AudioCue.MonitorArmed, controller.ArmMonitoring(observation).Cue);
        }

        private static void BossMonitoringRejectsMidFightAndDeadArming()
        {
            var controller = new EncounterController(3);
            var observation = new EncounterObservation { StartAuthorized = true, Flags = EncounterFlags.Boss };
            False(controller.ArmMonitoring(observation).ApplyControls);
            False(controller.IsSessionActive);
            observation.Flags = EncounterFlags.None;
            observation.PlayerDead = true;
            False(controller.ArmMonitoring(observation).ApplyControls);
            False(controller.IsSessionActive);
        }

        private static void BossMonitoringTransitionsOnceAndResetsLifeAccounting()
        {
            var controller = new EncounterController(3);
            controller.ArmMonitoring(new EncounterObservation { StartAuthorized = true, PlayerLife = 400 });
            var arrived = new EncounterObservation { StartAuthorized = true, Flags = EncounterFlags.Boss,
                PlayerLife = 250, ActiveBossKeys = new[] { 4 }, ActiveBossTypes = new[] { 370 } };
            var activation = controller.Activate(arrived);
            Equal(SessionState.EngagedAlive, activation.Current);
            Equal(AudioCue.TryMinnie, activation.Cue);
            True(activation.ApplyControls);
            Equal(AudioCue.None, controller.Update(arrived).Cue);
            Equal(AudioCue.None, controller.Activate(arrived).Cue);
            arrived.PlayerLife = 249;
            Equal(AudioCue.Man, controller.Update(arrived).Cue);
        }

        private static void BossMonitoringProductionHasNoSummonOrSurvivalPath()
        {
            using (var assembly = AssemblyDefinition.ReadAssembly(typeof(Chaite.Plugin.Runtime).Assembly.Location))
            {
                var runtime = FindCecilType(assembly, "Chaite.Plugin.Runtime");
                foreach (var method in runtime.Methods.Where(m => m.HasBody))
                    foreach (var instruction in method.Body.Instructions)
                    {
                        var call = instruction.Operand as MethodReference;
                        if (call == null) continue;
                        False(call.Name == "ExecuteBossStart" || call.Name == "FindBossStartPlan" || call.Name == "PlanSurvival",
                            "Production monitor must not retain auto-summon/survival calls: " + method.Name);
                        if (method.Name == "ArmBossMonitor" || method.Name == "StopBossMonitor")
                            False(call.Name == "ApplyPlan" || call.Name == "SetSelectedItem" || call.Name == "ClearCombatControls",
                                "Passive monitor must not write native controls/selection: " + method.Name);
                    }
            }
            using (var assembly = AssemblyDefinition.ReadAssembly(typeof(CombatPlanner).Assembly.Location))
            {
                var planner = FindCecilType(assembly, "Chaite.Core.CombatPlanner");
                var formula = FindCecilMethod(planner, "PlanFormula");
                foreach (var instruction in formula.Body.Instructions)
                {
                    var call = instruction.Operand as MethodReference;
                    if (call == null) continue;
                    False(call.Name == "FindBestCandidate" || call.Name == "Evaluate" || call.Name == "PlanSurvival" ||
                        call.Name == "TrySelectReadyMobilityRoute", "Formula path must not run old scoring or mobility selection");
                }
            }
        }
    }
}
