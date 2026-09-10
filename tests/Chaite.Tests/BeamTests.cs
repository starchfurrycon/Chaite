using Chaite.Core;
using System;

namespace Chaite.Tests
{
    internal static partial class Program
    {
        private static void RunBeamRegressions()
        {
            Run(nameof(MoonLordBeamUsesLongFiniteSegment), MoonLordBeamUsesLongFiniteSegment);
            Run(nameof(DeathrayIncludesNativeUnscaledSourceBody), DeathrayIncludesNativeUnscaledSourceBody);
            Run(nameof(BeamWarmupAndExpiryMatchNativeThresholds), BeamWarmupAndExpiryMatchNativeThresholds);
            Run(nameof(SunDanceKeepsItsThreeTaperedWidths), SunDanceKeepsItsThreeTaperedWidths);
            Run(nameof(BeamVelocityIsDirectionNotTranslation), BeamVelocityIsDirectionNotTranslation);
            Run(nameof(RotatingBeamSweepCatchesBetweenSamples), RotatingBeamSweepCatchesBetweenSamples);
            Run(nameof(BeamSweepsContainMovingRotatingAndTaperedSamples), BeamSweepsContainMovingRotatingAndTaperedSamples);
            Run(nameof(ShortDeathrayDoesNotAssumeFutureWallProtection), ShortDeathrayDoesNotAssumeFutureWallProtection);
            Run(nameof(PlannerKeepsFarOriginBeamThatCrossesPlayer), PlannerKeepsFarOriginBeamThatCrossesPlayer);
            Run(nameof(BeamCacheAndPruningMatchReference), BeamCacheAndPruningMatchReference);
            Run(nameof(BeamRichWorkloadBenchmark), BeamRichWorkloadBenchmark);
        }

        private static ThreatSnapshot Deathray()
        {
            return new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile, Geometry = ThreatGeometry.MoonLordDeathray, Type = 455,
                Width = 36, Height = 36, BeamOrigin = new Vec2(0, 0), BeamDirection = new Vec2(1, 0),
                BeamAge = 90, BeamLength = 2400, BeamScale = 1, BeamScaleLimit = 1, TimeLeft = 500, Damage = 150
            };
        }

        private static ThreatSnapshot SunDance()
        {
            return new ThreatSnapshot
            {
                Kind = ThreatKind.Projectile, Geometry = ThreatGeometry.EmpressSunDance, Type = 923,
                Width = 30, Height = 30, BeamAge = 90, BeamScale = 1, BeamAngle = 0,
                BeamBaseAngle = -.3490659f * (40f / 130f), TimeLeft = 90, Damage = 150
            };
        }

        private static void MoonLordBeamUsesLongFiniteSegment()
        {
            var ray = Deathray();
            var sample = BeamGeometry.AtTime(ray, 0);
            True(BeamGeometry.Intersects(new RectF(1800, -10, 20, 20), sample));
            False(BeamGeometry.Intersects(new RectF(1800, 40, 20, 20), sample));
            False(BeamGeometry.Intersects(new RectF(2450, -10, 20, 20), sample));
            False(BeamGeometry.Intersects(new RectF(-50, -10, 20, 20), sample));
            Equal(18f, sample.First.HalfWidth);
        }

        private static void BeamWarmupAndExpiryMatchNativeThresholds()
        {
            var ray = Deathray();
            ray.BeamAge = 19;
            // The ray cannot damage yet, but native projectile body overlap still
            // returns true before the age-20 branch is reached.
            Equal(1, BeamGeometry.AtTime(ray, 0).Count);
            False(BeamGeometry.Intersects(new RectF(1000, -5, 10, 10), BeamGeometry.AtTime(ray, 0)));
            Equal(2, BeamGeometry.AtTime(ray, 1).Count);
            ray.BeamAge = 179;
            True(BeamGeometry.AtTime(ray, 0).Active);
            False(BeamGeometry.AtTime(ray, 1).Active);
            var sun = SunDance();
            sun.BeamAge = 60;
            False(BeamGeometry.AtTime(sun, 0).Active);
            True(BeamGeometry.AtTime(sun, 1).Active);
            sun.BeamAge = 179;
            False(BeamGeometry.AtTime(sun, 1).Active);
        }

        private static void DeathrayIncludesNativeUnscaledSourceBody()
        {
            var ray = Deathray();
            ray.BeamOrigin = new Vec2(5000, 5000);
            var behindOrigin = new RectF(4979, 5001, 20, 42);
            True(BeamGeometry.Intersects(behindOrigin, BeamGeometry.AtTime(ray, 0)));
            ray.BeamScale = .4f;
            True(BeamGeometry.Intersects(behindOrigin, BeamGeometry.AtTime(ray, 0)));
            ray.BeamAge = 10;
            ray.BeamScale = 0;
            True(BeamGeometry.Intersects(behindOrigin, BeamGeometry.AtTime(ray, 0)));
            False(BeamGeometry.Intersects(new RectF(4950, 5001, 20, 42), BeamGeometry.AtTime(ray, 0)));
            ray.BeamSourceVelocity = new Vec2(20, 0);
            True(BeamGeometry.Intersects(new RectF(5035, 5001, 2, 2), BeamGeometry.Sweep(ray, 0, 3)));
            ray.BeamAge = 180;
            False(BeamGeometry.AtTime(ray, 0).Active);
            var sun = SunDance();
            sun.BeamOrigin = new Vec2(5000, 5000);
            False(BeamGeometry.Intersects(behindOrigin, BeamGeometry.AtTime(sun, 0)));
        }

        private static void SunDanceKeepsItsThreeTaperedWidths()
        {
            var sample = BeamGeometry.AtTime(SunDance(), 0);
            Equal(3, sample.Count);
            True(BeamGeometry.Intersects(new RectF(400, 30, 2, 2), sample));
            True(BeamGeometry.Intersects(new RectF(620, 17, 2, 2), sample));
            True(BeamGeometry.Intersects(new RectF(750, -1, 2, 2), sample));
            False(BeamGeometry.Intersects(new RectF(750, 17, 2, 2), sample));
            Equal(35f, sample.First.HalfWidth);
            Equal(21f, sample.Second.HalfWidth);
            Equal(3.5f, sample.Third.HalfWidth);
        }

        private static void BeamVelocityIsDirectionNotTranslation()
        {
            var ray = Deathray();
            ray.Velocity = new Vec2(1, 0);
            var later = BeamGeometry.AtTime(ray, 5);
            Equal(1200f, later.First.Center.X);
            ray.BeamSourceVelocity = new Vec2(4, 2);
            later = BeamGeometry.AtTime(ray, 5);
            Equal(1220f, later.First.Center.X);
            Equal(10f, later.First.Center.Y);
        }

        private static void RotatingBeamSweepCatchesBetweenSamples()
        {
            var ray = Deathray();
            ray.BeamDirection = new Vec2((float)Math.Cos(-.03), (float)Math.Sin(-.03));
            ray.BeamAngularVelocity = .02f;
            var player = new RectF(1000, -2, 8, 4);
            False(BeamGeometry.Intersects(player, BeamGeometry.AtTime(ray, 0)));
            False(BeamGeometry.Intersects(player, BeamGeometry.AtTime(ray, 3)));
            True(BeamGeometry.Intersects(player, BeamGeometry.Sweep(ray, 0, 3)));
        }

        private static void ShortDeathrayDoesNotAssumeFutureWallProtection()
        {
            var ray = Deathray();
            ray.BeamLength = 1000;
            Equal(500f, BeamGeometry.AtTime(ray, 0).First.HalfLength);
            Equal(1025f, BeamGeometry.AtTime(ray, 1).First.HalfLength);
        }

        private static void BeamSweepsContainMovingRotatingAndTaperedSamples()
        {
            var random = new Random(455923);
            for (var scene = 0; scene < 160; scene++)
            {
                var beam = scene % 2 == 0 ? Deathray() : SunDance();
                beam.BeamAge = random.Next(19, 177);
                beam.BeamOrigin = new Vec2(random.Next(1000, 5000), random.Next(1000, 5000));
                beam.BeamSourceVelocity = new Vec2(random.Next(-18, 19), random.Next(-18, 19));
                var angle = (float)(random.NextDouble() * Math.PI * 2);
                beam.BeamDirection = new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle));
                beam.BeamAngularVelocity = scene % 10 == 0 ? 2.2f : (float)(random.NextDouble() * .04 - .02);
                beam.BeamLength = random.Next(80, 2401);
                beam.BeamBaseAngle = angle;
                beam.BeamAngle = angle + .3490659f * Math.Max(0f, Math.Min(1f, (beam.BeamAge - 50f) / 130f));
                beam.BeamScaleLimit = scene % 4 == 0 ? .4f : 1f;
                beam.BeamScale = beam.Geometry == ThreatGeometry.MoonLordDeathray
                    ? Math.Max(0f, Math.Min(beam.BeamScaleLimit, (float)Math.Sin(beam.BeamAge * 3.141593f / 180f) * 10f * beam.BeamScaleLimit))
                    : Math.Min(1f, beam.BeamAge / 20f) * Math.Min(1f, (180f - beam.BeamAge) / 60f);
                for (var start = 0; start < 12; start += 3)
                {
                    var sweep = BeamGeometry.Sweep(beam, start, start + 3);
                    for (var sampleIndex = 0; sampleIndex <= 12; sampleIndex++)
                    {
                        var tick = start + sampleIndex * .25f;
                        // Native lifetime is measured in integer AI updates: tick 179
                        // is its last visible state; no state exists between 179 and 180.
                        if (beam.BeamAge + tick > 179) continue;
                        var sample = BeamGeometry.AtTime(beam, tick);
                        for (var lobeIndex = 0; lobeIndex < sample.Count; lobeIndex++)
                        {
                            var lobe = lobeIndex == 0 ? sample.First : lobeIndex == 1 ? sample.Second : sample.Third;
                            var normal = new Vec2(-lobe.Axis.Y, lobe.Axis.X);
                            for (var longitudinal = -1; longitudinal <= 1; longitudinal++)
                                for (var transverse = -1; transverse <= 1; transverse++)
                                {
                                    var point = lobe.Center + lobe.Axis * (lobe.HalfLength * longitudinal) + normal * (lobe.HalfWidth * transverse);
                                    if (!BeamGeometry.Intersects(new RectF(point.X - .03f, point.Y - .03f, .06f, .06f), sweep))
                                        throw new InvalidOperationException("Beam sweep missed its own sample: scene=" + scene + ", tick=" + tick + ", lobe=" + lobeIndex);
                                }
                        }
                    }
                }
            }
        }

        private static void PlannerKeepsFarOriginBeamThatCrossesPlayer()
        {
            var scene = CombatScenario(398);
            var ray = Deathray();
            ray.BeamOrigin = new Vec2(0, scene.Player.Center.Y);
            ray.Position = new Vec2(-18, scene.Player.Center.Y - 18);
            scene.Threats.Add(ray);
            var planner = new CombatPlanner(new PlannerSettings());
            var plan = planner.Plan(scene);
            Equal(1, planner.LastRelevantThreatCount);
            Equal(TacticalMode.EmergencyEvade, plan.TacticalMode);
        }

        private static void BeamCacheAndPruningMatchReference()
        {
            var random = new Random(9123);
            for (var sceneIndex = 0; sceneIndex < 24; sceneIndex++)
            {
                var scene = CombatScenario(398, 636);
                scene.Player.OnGround = false;
                for (var i = 0; i < 8; i++)
                {
                    var beam = i % 2 == 0 ? Deathray() : SunDance();
                    var angle = (float)(random.NextDouble() * Math.PI * 2);
                    beam.BeamOrigin = scene.Player.Center + new Vec2(random.Next(-900, 901), random.Next(-600, 601));
                    beam.BeamDirection = new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle));
                    beam.BeamAngle = angle;
                    beam.BeamBaseAngle = angle - .3490659f * (40f / 130f);
                    beam.BeamAngularVelocity = (float)(random.NextDouble() * .03 - .015);
                    beam.BeamSourceVelocity = new Vec2(random.Next(-4, 5), random.Next(-4, 5));
                    scene.Threats.Add(beam);
                }
                var optimized = new CombatPlanner(new PlannerSettings());
                var reference = new CombatPlanner(new PlannerSettings { CacheThreatPrediction = false, EnableScorePruning = false });
                for (var frame = 0; frame < 4; frame++)
                {
                    scene.Player.Position.X += 3;
                    AssertPlansIdentical(reference.Plan(scene), optimized.Plan(scene), sceneIndex, frame);
                }
            }
        }

        private static void BeamRichWorkloadBenchmark()
        {
            var scene = RichScenario(398, 636);
            for (var i = 0; i < 18; i++)
            {
                var beam = i < 3 ? Deathray() : SunDance();
                var angle = i * .3490659f;
                beam.BeamOrigin = new Vec2(2100, 650);
                beam.BeamDirection = new Vec2((float)Math.Cos(angle), (float)Math.Sin(angle));
                beam.BeamAngularVelocity = i < 3 ? .0116355f : 0;
                beam.BeamBaseAngle = angle;
                beam.BeamAngle = angle + .3490659f * (40f / 130f);
                scene.Threats.Add(beam);
            }
            var allocation = CreateAllocationCounter(out var scope);
            MeasureRichScenario("200 projectiles + 18 rotating/tapered beams", scene, allocation, scope);
        }
    }
}
