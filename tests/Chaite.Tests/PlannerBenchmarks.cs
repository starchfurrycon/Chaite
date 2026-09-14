using Chaite.Core;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace Chaite.Tests
{
    internal static partial class Program
    {
#if DEBUG
        private const double PlannerP95Milliseconds = 18d;
        private const double PlannerMeanMilliseconds = 10d;
#else
        private const double PlannerP95Milliseconds = 12d;
        private const double PlannerMeanMilliseconds = 8d;
#endif

        // These are offline Core planner costs, not end-to-end input, capture, game-render or audio latency.
        // Workload construction / state evolution / percentile sorting are outside timed samples.
        private static void PlannerRichWorkloadBenchmark()
        {
            Console.WriteLine("BENCH environment: CLR " + Environment.Version + ", " +
                (Environment.Is64BitProcess ? "x64" : "x86") + ", " + Environment.ProcessorCount +
                " logical CPUs; Core planning only, no game capture/render/input latency");
            var coldScenario = RichScenario(4);
            var started = Stopwatch.GetTimestamp();
            var coldPlanner = new CombatPlanner(new PlannerSettings());
            var coldPlan = coldPlanner.Plan(coldScenario);
            var coldMs = Milliseconds(Stopwatch.GetTimestamp() - started);
            AssertFinitePlan(coldPlan);
            Console.WriteLine("BENCH cold planner construction + first 200-projectile plan: " +
                coldMs.ToString("F3", CultureInfo.InvariantCulture) + " ms (includes first-use JIT; informational)");

            var allocation = CreateAllocationCounter(out var allocationScope);
            MeasureRichScenario("eye + 200 projectiles", RichScenario(4), allocation, allocationScope);
            var fishron = RichScenario(370);
            fishron.Difficulty.Expert = true;
            AddExactDash(fishron);
            MeasureRichScenario("expert Fishron phases + 200 projectiles", fishron, allocation, allocationScope);
            var mayhem = RichScenario(125, 126, 134, 127);
            mayhem.Difficulty.Master = true;
            mayhem.Difficulty.Expert = true;
            MeasureRichScenario("4 mechanical boss NPCs + 200 projectiles", mayhem, allocation, allocationScope);
            var mixed = RichScenario(50, 4, 370);
            MeasureRichScenario("mixed boss families + 200 projectiles", mixed, allocation, allocationScope);
            var flight = RichScenario(657);
            flight.Player.Jump = OrdinaryJump();
            flight.Player.Flight = DemonFlight();
            flight.Player.Gravity = .4f;
            flight.Player.MaxFallSpeed = 10f;
            flight.Player.BaseRunSpeed = 3.2f;
            flight.Player.SprintAcceleration = .032f;
            flight.Player.RunSlowdown = .2f;
            flight.Player.CanSprintInAir = true;
            flight.Mobility.HasFiniteFlightResource = true;
            MeasureRichScenario("Demon native ticks + Queen + 200 projectiles", flight, allocation, allocationScope);

            MeasureDestroyerPressure("Destroyer P1 HeadExit + 80 segments + 200 projectiles",
                false, allocation, allocationScope, 384, 80, 200);
            MeasureDestroyerPressure("Destroyer P1 RecoverAnchor + 80 segments + 200 projectiles",
                true, allocation, allocationScope, 384, 80, 200);
            MeasureDestroyerPressure("Destroyer P1 HeadExit 1000-threat stress",
                false, allocation, allocationScope, 128, 80, 920);
        }

        private static void MeasureDestroyerPressure(string name, bool recovery,
            Func<long> allocation, string allocationScope, int samples, int segmentCount, int projectileCount)
        {
            var warmup = Math.Min(48, Math.Max(16, samples / 4));
            var snapshot = DestroyerPressureScenario(segmentCount, projectileCount);
            var planner = new CombatPlanner(new PlannerSettings());
            for (var i = 0; i < warmup; i++)
            {
                PrepareDestroyerPressurePhase(snapshot, planner, recovery);
                var warm = planner.Plan(snapshot);
                True(warm.PhaseId.Contains(recovery ? "recover-anchor" : "head-exit"), warm.PhaseId);
            }

            var timings = new long[samples];
            double checksum = 0d;
            long sum = 0;
            long allocated = 0;
            var gen0 = 0;
            var gen1 = 0;
            var gen2 = 0;
            for (var i = 0; i < samples; i++)
            {
                AdvanceDestroyerPressure(snapshot, i);
                PrepareDestroyerPressurePhase(snapshot, planner, recovery);
                var allocatedBefore = allocation == null ? 0 : allocation();
                var gen0Before = GC.CollectionCount(0);
                var gen1Before = GC.CollectionCount(1);
                var gen2Before = GC.CollectionCount(2);
                var start = Stopwatch.GetTimestamp();
                var result = planner.Plan(snapshot);
                var elapsed = Stopwatch.GetTimestamp() - start;
                gen0 += GC.CollectionCount(0) - gen0Before;
                gen1 += GC.CollectionCount(1) - gen1Before;
                gen2 += GC.CollectionCount(2) - gen2Before;
                if (allocation != null) allocated += allocation() - allocatedBefore;
                timings[i] = elapsed;
                sum += elapsed;
                checksum += result.RiskScore + result.Horizontal;
                True(result.PhaseId.Contains(recovery ? "recover-anchor" : "head-exit"), result.PhaseId);
            }

            Array.Sort(timings);
            var p50 = Milliseconds(timings[(int)Math.Ceiling(samples * .50) - 1]);
            var p95 = Milliseconds(timings[(int)Math.Ceiling(samples * .95) - 1]);
            var p99 = Milliseconds(timings[(int)Math.Ceiling(samples * .99) - 1]);
            var maximum = Milliseconds(timings[samples - 1]);
            var average = Milliseconds(sum) / samples;
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "BENCH {0}: warm n={1}, p50={2:F3}, p95={3:F3}, p99={4:F3}, max={5:F3}, mean={6:F3} ms; GC={7}/{8}/{9}; {10}",
                name, samples, p50, p95, p99, maximum, average, gen0, gen1, gen2,
                allocation == null ? "allocation counter unavailable" :
                    (allocated / (double)samples).ToString("F1", CultureInfo.InvariantCulture) + " B/plan (" + allocationScope + ")"));
            True(!double.IsNaN(checksum) && !double.IsInfinity(checksum), name + " returned a non-finite plan");
#if !DEBUG
            True(p95 < PlannerP95Milliseconds, name + " p95 exceeded " +
                PlannerP95Milliseconds + " ms offline planner regression ceiling: " + p95);
            True(average < PlannerMeanMilliseconds, name + " mean exceeded " +
                PlannerMeanMilliseconds + " ms offline planner regression ceiling: " + average);
#endif
        }

        private static CombatSnapshot DestroyerPressureScenario(int segmentCount, int projectileCount)
        {
            var snapshot = DestroyerP1Snapshot();
            for (var i = 0; i < segmentCount; i++)
            {
                snapshot.Threats.Add(new ThreatSnapshot
                {
                    Kind = ThreatKind.NpcContact,
                    Geometry = ThreatGeometry.Body,
                    Type = i == segmentCount - 1 ? 136 : 135,
                    Position = new Vec2(300f + i * 37 % 1500, 420f + i * 53 % 500),
                    Velocity = new Vec2((i % 7 - 3) * 1.7f, (i % 5 - 2) * 1.3f),
                    Width = 34,
                    Height = 34,
                    Damage = 80,
                    TimeLeft = int.MaxValue
                });
            }
            for (var i = 0; i < projectileCount; i++)
            {
                snapshot.Threats.Add(new ThreatSnapshot
                {
                    Kind = ThreatKind.Projectile,
                    Geometry = ThreatGeometry.Body,
                    Type = i % 2 == 0 ? 100 : 101,
                    Position = new Vec2(350f + i * 83 % 1450, 360f + i * 67 % 570),
                    Velocity = new Vec2((i % 9 - 4) * 2.3f, (i % 7 - 3) * 1.6f),
                    Width = 10,
                    Height = 10,
                    Damage = 44,
                    TimeLeft = 240
                });
            }
            return snapshot;
        }

        private static void PrepareDestroyerPressurePhase(CombatSnapshot snapshot, CombatPlanner planner, bool recovery)
        {
            planner.Reset();
            snapshot.Player.OnGround = true;
            snapshot.Player.Position = new Vec2(790f, 958f);
            snapshot.Player.Velocity = new Vec2(0f, 0f);
            SetDestroyerResource(snapshot, 100f, 7);
            SetDestroyerHead(snapshot, 2600f, 200f, 0f, 0f);
            planner.Plan(snapshot); // Acquire the observed support.

            SetDestroyerHead(snapshot, 500f, 1200f, 8f, -12f);
            if (!recovery) return;
            planner.Plan(snapshot); // Enter HeadExit.
            snapshot.Player.OnGround = false;
            snapshot.Player.Position = new Vec2(100f, 700f);
            snapshot.Player.Velocity = new Vec2(0f, 0f);
            SetDestroyerResource(snapshot, 20f, 0);
            SetDestroyerHead(snapshot, 2600f, 200f, 0f, 0f);
            planner.Plan(snapshot); // Exhaustion transitions into RecoverAnchor.
        }

        private static void AdvanceDestroyerPressure(CombatSnapshot snapshot, int tick)
        {
            for (var i = 0; i < snapshot.Threats.Count; i++)
            {
                var threat = snapshot.Threats[i];
                var lane = (i * 29 + tick * 11) % 1600;
                threat.Position.X = 250f + lane;
                threat.Position.Y = 330f + (i * 47 + tick * 7) % 620;
                snapshot.Threats[i] = threat;
            }
        }

        private static void MeasureRichScenario(string name, CombatSnapshot snapshot, Func<long> allocation, string allocationScope)
        {
            const int warmup = 80;
            const int samples = 384;
            var planner = new CombatPlanner(new PlannerSettings());
            for (var i = 0; i < warmup; i++)
            {
                AdvanceBenchmarkScenario(snapshot, i);
                planner.Plan(snapshot);
            }

            var timings = new long[samples];
            var gen0Before = GC.CollectionCount(0);
            var gen1Before = GC.CollectionCount(1);
            var gen2Before = GC.CollectionCount(2);
            var allocatedBefore = allocation == null ? 0 : allocation();
            double checksum = 0;
            long sum = 0;
            for (var i = 0; i < samples; i++)
            {
                AdvanceBenchmarkScenario(snapshot, i + warmup);
                var start = Stopwatch.GetTimestamp();
                var result = planner.Plan(snapshot);
                var elapsed = Stopwatch.GetTimestamp() - start;
                timings[i] = elapsed;
                sum += elapsed;
                checksum += result.RiskScore + result.Horizontal;
            }
            var allocated = allocation == null ? 0 : allocation() - allocatedBefore;
            var gen0 = GC.CollectionCount(0) - gen0Before;
            var gen1 = GC.CollectionCount(1) - gen1Before;
            var gen2 = GC.CollectionCount(2) - gen2Before;
            Array.Sort(timings);
            var p50 = Milliseconds(timings[(int)Math.Ceiling(samples * .50) - 1]);
            var p95 = Milliseconds(timings[(int)Math.Ceiling(samples * .95) - 1]);
            var p99 = Milliseconds(timings[(int)Math.Ceiling(samples * .99) - 1]);
            var maximum = Milliseconds(timings[samples - 1]);
            var average = Milliseconds(sum) / samples;
            Console.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "BENCH {0}: warm n={1}, p50={2:F3}, p95={3:F3}, p99={4:F3}, max={5:F3}, mean={6:F3} ms; GC={7}/{8}/{9}; {10}",
                name, samples, p50, p95, p99, maximum, average, gen0, gen1, gen2,
                allocation == null ? "allocation counter unavailable" :
                    (allocated / (double)samples).ToString("F1", CultureInfo.InvariantCulture) + " B/plan (" + allocationScope + ")"));
            True(!double.IsNaN(checksum) && !double.IsInfinity(checksum), name + " returned a non-finite plan");
            // A wide regression ceiling, not a no-lag guarantee. Single OS scheduling stalls affect max,
            // which is reported but deliberately never used as a flaky pass/fail threshold.
#if !DEBUG
            True(p95 < PlannerP95Milliseconds, name + " p95 exceeded " +
                PlannerP95Milliseconds + " ms offline planner regression ceiling: " + p95);
            True(average < PlannerMeanMilliseconds, name + " mean exceeded " +
                PlannerMeanMilliseconds + " ms offline planner regression ceiling: " + average);
#endif
        }

        private static CombatSnapshot RichScenario(params int[] bosses)
        {
            var snapshot = CombatScenario(bosses);
            snapshot.Player.OnGround = false;
            snapshot.Player.Velocity = new Vec2(5, -1);
            for (var i = 0; i < 200; i++)
            {
                // Deterministic dense arena field: all threats remain within the local combat region;
                // near-player lanes and high-velocity crossings are included, not 200 distant no-ops.
                snapshot.Threats.Add(new ThreatSnapshot
                {
                    Kind = ThreatKind.Projectile,
                    Position = new Vec2(700 + (i * 97 % 1600), 300 + (i * 71 % 1000)),
                    Velocity = new Vec2((i % 9 - 4) * 2.8f, (i % 7 - 3) * 1.9f),
                    Width = 12 + i % 4 * 6,
                    Height = 12 + i % 3 * 8,
                    Damage = 45 + i % 90,
                    TimeLeft = 60 + i % 180,
                    Type = i % 8 == 0 ? 455 : i % 7 == 0 ? 919 : i % 5 == 0 ? 961 : 1
                });
            }
            return snapshot;
        }

        private static void AdvanceBenchmarkScenario(CombatSnapshot snapshot, int tick)
        {
            snapshot.Player.Position.X = 1500 + (tick % 128 - 64) * 1.5f;
            snapshot.Player.Position.Y = 800 + (tick % 48 - 24) * .6f;
            snapshot.Player.Velocity.X = tick % 256 < 128 ? 5f : -5f;
            snapshot.Mobility.FlightResourceFraction = (128 - tick % 128) / 128f;
            snapshot.Player.WingTime = 100 * snapshot.Mobility.FlightResourceFraction;
            if (snapshot.Player.Flight.Known)
            {
                snapshot.Player.Flight.WingTime = 142 - tick % 143;
                snapshot.Player.Flight.RocketTime = 0;
                snapshot.Player.WingTime = snapshot.Player.Flight.WingTime;
                snapshot.Player.RocketTime = snapshot.Player.Flight.RocketTime;
                snapshot.Mobility.FlightResourceFraction = FlightMotion.ResourceFraction(in snapshot.Player.Flight);
            }
            for (var i = 0; i < snapshot.Targets.Count; i++)
            {
                var target = snapshot.Targets[i];
                target.Life = tick % 192 < 64 ? 2800 : tick % 192 < 128 ? 1200 : 250;
                target.Ai0 = tick / 48 % 4;
                target.Ai1 = tick / 24 % 3;
                target.Velocity = tick % 30 < 10 ? new Vec2(-14, 3) : new Vec2(-2, -1);
                snapshot.Targets[i] = target;
            }
            for (var i = 0; i < snapshot.Threats.Count; i++)
            {
                var threat = snapshot.Threats[i];
                threat.Position += threat.Velocity;
                if (threat.Position.X < 650) threat.Position.X += 1700;
                if (threat.Position.X > 2350) threat.Position.X -= 1700;
                if (threat.Position.Y < 250) threat.Position.Y += 1100;
                if (threat.Position.Y > 1350) threat.Position.Y -= 1100;
                snapshot.Threats[i] = threat;
            }
        }

        private static Func<long> CreateAllocationCounter(out string scope)
        {
            var method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Static | BindingFlags.Public);
            if (method != null)
            {
                scope = "current-thread allocation";
                return (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
            }
            try
            {
                AppDomain.MonitoringIsEnabled = true;
                var domain = AppDomain.CurrentDomain;
                // .NET Framework fallback counts allocations in this test AppDomain, including
                // other threads if any exist. No process-wide heap-size/GC-delta approximation.
                Func<long> getter = () => domain.MonitoringTotalAllocatedMemorySize;
                getter();
                scope = "AppDomain allocation; includes other threads";
                return getter;
            }
            catch (NotSupportedException)
            {
                scope = "unavailable";
                return null;
            }
        }

        private static double Milliseconds(long ticks) => ticks * 1000d / Stopwatch.Frequency;
    }
}
