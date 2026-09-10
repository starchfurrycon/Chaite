namespace Chaite.Core
{
    public sealed class PlannerSettings
    {
        public int HorizonTicks { get; set; } = 48;
        public int SimulationStepTicks { get; set; } = 3;
        public float ProjectileSafetyMargin { get; set; } = 18f;
        public float ContactSafetyMargin { get; set; } = 28f;
        public float CollisionPenalty { get; set; } = 100000f;
        public float DamagePenalty { get; set; } = 5000f;
        public float NearMissPenalty { get; set; } = 900f;
        public float MovementChangePenalty { get; set; } = 4f;
        public float IdealRangedDistance { get; set; } = 520f;
        public float IdealMeleeDistance { get; set; } = 105f;
        public float HealAtLifeFraction { get; set; } = 0.55f;
        public float ManaAtFraction { get; set; } = 0.20f;
        public float PatternDeviationPenalty { get; set; } = 38f;
        public float VerticalPatternDeviationPenalty { get; set; } = 16f;
        public float EmergencyRiskThreshold { get; set; } = 3500f;
        public int ImmediateThreatTicks { get; set; } = 10;
        public int EmergencyHysteresisTicks { get; set; } = 8;
        public int RecoveryTicks { get; set; } = 24;
        public int StablePatternTicks { get; set; } = 24;
        public int DirectionHysteresisTicks { get; set; } = 20;
        public int StuckTicksBeforeRecovery { get; set; } = 18;
        public float StuckDistancePixels { get; set; } = 0.75f;
        public float ArenaEdgeMarginPixels { get; set; } = 112f;
        public float ArenaVerticalMarginPixels { get; set; } = 80f;
        public int MobilityActionCooldownTicks { get; set; } = 30;
        public float FlightReserveFraction { get; set; } = .18f;
        public float FlightResumeFraction { get; set; } = .70f;
        public float PatternSafeRiskThreshold { get; set; } = 1800f;
        // Diagnostic switches allow tests to compare mathematically identical paths.
        public bool CacheThreatPrediction { get; set; } = true;
        public bool EnableScorePruning { get; set; } = true;
    }
}
