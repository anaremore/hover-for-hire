using System;

namespace HoverForHire
{
    public enum MissionState { Available, Accepted, Pickup, Transport, Delivered, Failed }
    public enum ContractType { Passengers, Cargo }
    public enum GameMode { FreeFlight, Training, DeliveryShift }

    [Serializable]
    public struct ZoneDefinition
    {
        public string Id;
        public string Name;
        public float X, Y, Z, Radius;

        public float HorizontalDistance(FlightSample sample)
        {
            float x = sample.X - X, z = sample.Z - Z;
            return (float)Math.Sqrt(x * x + z * z);
        }
    }

    /// <summary>A physics observation, independent of Unity and input/assist implementation.</summary>
    public struct FlightSample
    {
        public float X, Y, Z;
        public float GroundSpeed, VerticalSpeed, Heading, Altitude, TiltDegrees;
        public float Acceleration, AngularSpeedDegrees;
        public bool Grounded, Crashed;
    }

    [Serializable]
    public class ContractDefinition
    {
        public string Id;
        public string Title;
        public ContractType Type;
        public ZoneDefinition Pickup;
        public ZoneDefinition Destination;
        public float PayloadKg;
        public float ExpectedSeconds = 180f;
        public float DeadlineSeconds = 540f;
        public float DwellSeconds = 3f;
        public float MaximumGroundSpeed = 0.8f;
        public float MaximumVerticalSpeed = 0.5f;
        public float MaximumTiltDegrees = 8f;
        public int BasePay = 200;
        public int RequiredDeliveries;

        public bool IsStableAt(FlightSample sample, ZoneDefinition zone)
        {
            // Body origin is approximately 1.5 m above skid contact. Grounded alone
            // could be a nearby roof, so require the actual pad's surface elevation.
            float heightAbovePad = sample.Y - zone.Y;
            return !sample.Crashed && sample.Grounded && heightAbovePad >= 0.2f && heightAbovePad <= 3.2f
                && zone.HorizontalDistance(sample) <= zone.Radius
                && sample.GroundSpeed <= MaximumGroundSpeed
                && Math.Abs(sample.VerticalSpeed) <= MaximumVerticalSpeed
                && sample.TiltDegrees <= MaximumTiltDegrees;
        }
    }

    [Serializable]
    public class ChallengeResult
    {
        public string AttemptId;
        public string ContractId;
        public string Title;
        public string Mode;
        public string Grade;
        public string Assists;
        public string CompletedUtc;
        public int Payout;
        public float Seconds;
        public float AccuracyMetres;
        public float TouchdownMetresPerSecond;
        public float ComfortPercent;
        public float CargoConditionPercent;
        public float Score;
        public string Feedback;
    }

    /// <summary>Explicit, deterministic mission lifecycle. A completed attempt pays at most once.</summary>
    public sealed class MissionSession
    {
        public ContractDefinition Contract { get; }
        public MissionState State { get; private set; } = MissionState.Available;
        public string AttemptId { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public float DwellSeconds { get; private set; }
        public float Comfort { get; private set; } = 100f;
        public float CargoCondition { get; private set; } = 100f;
        public float LandingSpeed { get; private set; }
        public string FailureReason { get; private set; } = "";
        public ChallengeResult Result { get; private set; }
        public string AssistSnapshot { get; private set; }
        private bool payoutClaimed;

        public float PayloadKg => State == MissionState.Transport ? Contract.PayloadKg : 0f;
        public float DwellProgress => Contract.DwellSeconds > 0f ? Clamp01(DwellSeconds / Contract.DwellSeconds) : 0f;
        public ZoneDefinition Target => State == MissionState.Transport ? Contract.Destination : Contract.Pickup;

        public MissionSession(ContractDefinition contract)
        {
            Contract = contract ?? throw new ArgumentNullException(nameof(contract));
            AttemptId = Guid.NewGuid().ToString("N");
        }

        public bool Accept(string assists)
        {
            if (State != MissionState.Available) return false;
            AssistSnapshot = assists ?? "Not recorded";
            State = MissionState.Accepted;
            return true;
        }

        public void RecordAssists(string assists)
        {
            if (State == MissionState.Delivered || State == MissionState.Failed || string.IsNullOrWhiteSpace(assists)) return;
            if (string.IsNullOrEmpty(AssistSnapshot)) AssistSnapshot = assists;
            else if (Array.IndexOf(AssistSnapshot.Split(new[] { " → " }, StringSplitOptions.None), assists) < 0)
                AssistSnapshot += " → " + assists;
        }

        public void Tick(float deltaSeconds, FlightSample sample)
        {
            if (deltaSeconds <= 0f || float.IsNaN(deltaSeconds) || float.IsInfinity(deltaSeconds)) return;
            if (State == MissionState.Available || State == MissionState.Delivered || State == MissionState.Failed) return;
            if (sample.Crashed) { Fail("Aircraft damaged. Retry to restart this contract."); return; }
            ElapsedSeconds += deltaSeconds;
            if (ElapsedSeconds > Contract.DeadlineSeconds) { Fail("Service deadline missed. Retry with an earlier approach."); return; }
            if (State == MissionState.Accepted) State = MissionState.Pickup;

            if (State == MissionState.Transport)
            {
                // Smooth flight preserves condition; ordinary takeoff acceleration is free.
                float harshness = Math.Max(0f, sample.Acceleration - 5f) * 0.16f
                    + Math.Max(0f, sample.AngularSpeedDegrees - 35f) * 0.025f
                    + Math.Max(0f, sample.TiltDegrees - 35f) * 0.018f;
                Comfort = Math.Max(0f, Comfort - harshness * deltaSeconds);
                CargoCondition = Math.Max(0f, CargoCondition - Math.Max(0f, sample.Acceleration - 12f) * deltaSeconds * 0.18f);
            }

            if (!Contract.IsStableAt(sample, Target)) { DwellSeconds = 0f; return; }
            DwellSeconds += deltaSeconds;
            if (DwellSeconds < Contract.DwellSeconds) return;
            DwellSeconds = 0f;
            if (State == MissionState.Pickup)
            {
                State = MissionState.Transport;
                LandingSpeed = 0f;
            }
            else if (State == MissionState.Transport) Deliver(sample);
        }

        public void RecordTouchdown(float speed)
        {
            if (State != MissionState.Transport) return;
            LandingSpeed = Math.Max(LandingSpeed, Math.Abs(speed));
            float impact = Math.Max(0f, Math.Abs(speed) - 1.5f);
            Comfort = Math.Max(0f, Comfort - impact * 7f);
            CargoCondition = Math.Max(0f, CargoCondition - impact * impact * 4f);
        }

        public void Fail(string reason)
        {
            if (State == MissionState.Delivered || State == MissionState.Failed || State == MissionState.Available) return;
            State = MissionState.Failed;
            DwellSeconds = 0f;
            FailureReason = reason;
        }

        public bool Retry(string assists)
        {
            if (State == MissionState.Delivered || State == MissionState.Available) return false;
            AttemptId = Guid.NewGuid().ToString("N");
            ElapsedSeconds = DwellSeconds = LandingSpeed = 0f;
            Comfort = CargoCondition = 100f;
            FailureReason = "";
            Result = null;
            payoutClaimed = false;
            AssistSnapshot = assists ?? "Not recorded";
            State = MissionState.Accepted;
            return true;
        }

        public bool TryClaimPayout(out ChallengeResult result)
        {
            result = null;
            if (State != MissionState.Delivered || payoutClaimed) return false;
            payoutClaimed = true;
            result = Result;
            return true;
        }

        private void Deliver(FlightSample sample)
        {
            State = MissionState.Delivered;
            float accuracy = Contract.Destination.HorizontalDistance(sample);
            float timeScore = Clamp01(Contract.ExpectedSeconds / Math.Max(Contract.ExpectedSeconds, ElapsedSeconds));
            float precisionScore = 1f - Clamp01(accuracy / Math.Max(1f, Contract.Destination.Radius));
            float landingScore = 1f - Clamp01(Math.Max(0f, LandingSpeed - 0.5f) / 5f);
            float handlingScore = (Contract.Type == ContractType.Passengers ? Comfort : CargoCondition) / 100f;
            float score = 100f * (timeScore * 0.3f + precisionScore * 0.25f + landingScore * 0.25f + handlingScore * 0.2f);
            Result = new ChallengeResult
            {
                AttemptId = AttemptId, ContractId = Contract.Id, Title = Contract.Title, Mode = "Delivery Shift",
                Grade = GradeFor(score), Score = score, Payout = (int)Math.Round(Contract.BasePay * (0.65f + score / 100f * 0.65f)),
                Seconds = ElapsedSeconds, AccuracyMetres = accuracy, TouchdownMetresPerSecond = LandingSpeed,
                ComfortPercent = Comfort, CargoConditionPercent = CargoCondition,
                Assists = AssistSnapshot, CompletedUtc = DateTime.UtcNow.ToString("O"),
                Feedback = landingScore < 0.65f ? "Reduce descent below 1 m/s before the skids touch."
                    : precisionScore < 0.6f ? "Pause in a low hover over the pad center before descending."
                    : handlingScore < 0.8f ? "Use smaller cyclic corrections and begin braking earlier."
                    : timeScore < 0.8f ? "Plan a direct route, then leave space for a controlled approach."
                    : "Smooth delivery. Keep that controlled approach on the next job."
            };
        }

        public static string GradeFor(float score) => score >= 90f ? "A" : score >= 78f ? "B" : score >= 62f ? "C" : "D";
        public static float Clamp01(float value) => Math.Max(0f, Math.Min(1f, value));
    }
}
