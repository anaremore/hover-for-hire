using System;

namespace HoverForHire
{
    public enum TrainingState { Active, Complete, Failed }

    /// <summary>
    /// Outcome-based drills using the same observations as normal flight. Drills 1–7 teach basic handling;
    /// 8–12 teach the realism skills: crosswind, power management, settling with power, autorotation, confined areas.
    /// </summary>
    public sealed class TrainingSession
    {
        public static readonly string[] Names = { "Takeoff", "Hover", "Yaw control", "Forward flight", "Braking", "Approach", "Precision landing",
            "Crosswind landing", "Heavy lift", "Settling with power", "Autorotation", "Confined area" };
        public const int Crosswind = 7, HeavyLift = 8, SettlingWithPower = 9, Autorotation = 10, ConfinedArea = 11;
        /// <summary>Set by the autorotation drill once the engine should fail; the director applies it to the aircraft.</summary>
        public bool RequestEngineFailure { get; private set; }
        public int Index { get; }
        public TrainingState State { get; private set; } = TrainingState.Active;
        public int Stage { get; private set; }
        public float ElapsedSeconds { get; private set; }
        public float StableSeconds { get; private set; }
        public float Progress { get; private set; }
        public float PositionError { get; private set; }
        public float HeadingError { get; private set; }
        public float TouchdownSpeed { get; private set; }
        public float TargetX { get; private set; }
        public float TargetY { get; private set; }
        public float TargetZ { get; private set; }
        public string Objective { get; private set; }
        public string Feedback { get; private set; }
        public string Assists { get; private set; }
        public ChallengeResult Result { get; private set; }
        private readonly ZoneDefinition home, reference;
        private readonly float forwardX, forwardZ, targetHeading;
        private float errorIntegral, headingIntegral, measuredSeconds, brakeX, brakeZ, brakingDistance;
        private float peakTorque, lowRotorSeconds, rotorOutsideSeconds, entryAltitude, lowestAfterEntry = float.MaxValue;
        private bool claimed;

        /// <param name="target">Landing reference for drills away from home (the confined-area pad); home otherwise.</param>
        public TrainingSession(int index, ZoneDefinition home, float heading, string assists, ZoneDefinition? target = null)
        {
            Index = Math.Max(0, Math.Min(Names.Length - 1, index));
            this.home = home;
            reference = target ?? home;
            forwardX = (float)Math.Sin(heading * Math.PI / 180.0);
            forwardZ = (float)Math.Cos(heading * Math.PI / 180.0);
            targetHeading = (heading + 90f) % 360f;
            Assists = assists ?? "Not recorded";
            SetTarget(0f, 10f);
            switch (Index)
            {
                case 0: Objective = "Raise collective gently. Climb to 8–12 m and hold for 4 seconds."; break;
                case 1: Objective = "Hover at 8–12 m, within 6 m of pad center, for 12 continuous seconds."; break;
                case 2: Objective = $"Lift to 8–15 m. Use pedals to face {targetHeading:000}° and hold for 5 seconds."; break;
                case 3: SetTarget(110f, 15f); Objective = "Climb above 6 m, fly 100 m forward at 8–22 m/s, then hold steady for 3 seconds."; break;
                case 4: SetTarget(100f, 15f); Objective = "Climb above 6 m and accelerate to 12 m/s to begin the braking drill."; break;
                case 5: SetTarget(100f, 18f); Objective = "Climb above 12 m and depart at least 80 m from the pad, then return to land."; break;
                case 6: SetTarget(30f, 10f); Objective = "Climb above 5 m and move 20 m from the pad, then return for a center landing."; break;
                case Crosswind: Objective = "Crosswind: hover at 8–12 m within 6 m of pad center for 8 seconds. Lean into the wind."; break;
                case HeavyLift: SetTarget(0f, 25f); Objective = "Maximum load: lift off gently and climb to 25 m. Keep torque under 110% and rotor RPM above 95%."; break;
                case SettlingWithPower: Objective = "Slow to a hover, then lower collective until you sink at 5–7 m/s with no forward speed. Recognise the settling."; break;
                case Autorotation: Objective = "Cruise straight ahead. Be ready: the engine will fail."; break;
                default:
                    Stage = 1;
                    Objective = "Confined area: land on the pad inside the cutting within 3 m of center. Keep the rotor clear of the slopes.";
                    break;
            }
            Feedback = "Use small inputs. Collective holds its setting when released.";
        }

        public void RecordAssists(string assists)
        {
            if (State != TrainingState.Active || string.IsNullOrWhiteSpace(assists)) return;
            if (Array.IndexOf(Assists.Split(new[] { " → " }, StringSplitOptions.None), assists) < 0) Assists += " → " + assists;
        }

        public void RecordTouchdown(float speed)
        {
            bool landingPhase = Stage > 0 || Index == Autorotation && RequestEngineFailure;
            if (landingPhase && State == TrainingState.Active) TouchdownSpeed = Math.Max(TouchdownSpeed, Math.Abs(speed));
        }

        public void Fail(string reason)
        {
            if (State != TrainingState.Active) return;
            State = TrainingState.Failed;
            Feedback = reason;
            StableSeconds = 0f;
        }

        public void Tick(float dt, FlightSample sample)
        {
            if (State != TrainingState.Active || dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            if (sample.Crashed) { Fail("Aircraft damaged. Retry, then reduce descent and use smaller corrections."); return; }
            ElapsedSeconds += dt;
            if (ElapsedSeconds > 300f) { Fail("Drill paused after 5 minutes. Retry and focus on the displayed target."); return; }
            PositionError = reference.HorizontalDistance(sample);
            HeadingError = Math.Abs(DeltaAngle(sample.Heading, targetHeading));
            float bodyHeight = sample.Y - reference.Y;
            // Match the modeled 1.5 m origin-to-skid clearance and the HUD's skid AGL units.
            float altitude = bodyHeight - 1.5f;
            if (!sample.Grounded && altitude > 3f && Index <= 2)
            {
                measuredSeconds += dt;
                errorIntegral += PositionError * dt;
                headingIntegral += HeadingError * dt;
            }

            bool stable = false;
            float required = 4f;
            switch (Index)
            {
                case 0:
                    stable = !sample.Grounded && altitude >= 8f && altitude <= 12f && sample.GroundSpeed <= 2f
                        && Math.Abs(sample.VerticalSpeed) <= 1f && PositionError <= 8f;
                    Feedback = HoverFeedback(sample, altitude, 8f);
                    break;
                case 1:
                    required = 12f;
                    stable = !sample.Grounded && altitude >= 8f && altitude <= 12f && PositionError <= 6f
                        && sample.GroundSpeed <= 2f && Math.Abs(sample.VerticalSpeed) <= 1f;
                    Feedback = HoverFeedback(sample, altitude, 6f);
                    break;
                case 2:
                    required = 5f;
                    stable = !sample.Grounded && altitude >= 8f && altitude <= 15f && PositionError <= 9f
                        && sample.GroundSpeed <= 2.5f && HeadingError <= 10f && Math.Abs(sample.VerticalSpeed) <= 1.2f;
                    Feedback = altitude < 8f ? "Climb to 8–15 m before turning."
                        : PositionError > 9f ? "Correct drift back toward the pad, then resume your turn."
                        : HeadingError > 10f ? $"Heading error {HeadingError:0}°. Ease opposite pedal in before reaching the target."
                        : "Hold the heading and altitude with small corrections.";
                    break;
                case 3:
                    required = 3f;
                    float along = (sample.X - home.X) * forwardX + (sample.Z - home.Z) * forwardZ;
                    float cross = Math.Abs((sample.X - home.X) * forwardZ - (sample.Z - home.Z) * forwardX);
                    stable = !sample.Grounded && along >= 100f && cross <= 30f && altitude >= 6f && altitude <= 35f
                        && sample.GroundSpeed >= 8f && sample.GroundSpeed <= 22f && Math.Abs(sample.VerticalSpeed) <= 2f;
                    Feedback = altitude < 6f ? "Gain 6 m of height before accelerating."
                        : along < 100f ? $"{Math.Max(0f, 100f - along):0} m to go. Small forward cyclic; add collective to hold height."
                        : sample.GroundSpeed > 22f ? "Ease aft cyclic to reduce speed below 22 m/s."
                        : cross > 30f ? "Turn gently toward the outbound marker."
                        : "Hold 8–22 m/s and a steady altitude for 3 seconds.";
                    break;
                case 4:
                    if (Stage == 0 && !sample.Grounded && altitude >= 6f && sample.GroundSpeed >= 12f)
                    {
                        Stage = 1;
                        brakeX = sample.X; brakeZ = sample.Z;
                        Objective = "Brake with aft cyclic. Hold a hover below 2 m/s at 6–25 m for 4 seconds.";
                        TargetX = sample.X; TargetZ = sample.Z; TargetY = sample.Y;
                    }
                    if (Stage == 1)
                    {
                        float x = sample.X - brakeX, z = sample.Z - brakeZ;
                        brakingDistance = Math.Max(brakingDistance, (float)Math.Sqrt(x * x + z * z));
                    }
                    stable = Stage == 1 && !sample.Grounded && altitude >= 6f && altitude <= 25f
                        && sample.GroundSpeed <= 2f && Math.Abs(sample.VerticalSpeed) <= 1f;
                    Feedback = Stage == 0 ? "Build 12 m/s of forward speed with room ahead."
                        : sample.GroundSpeed > 2f ? $"{sample.GroundSpeed:0.0} m/s. Apply aft cyclic, then level before you drift backward."
                        : "Center cyclic as speed falls. Adjust collective to arrest the climb or descent.";
                    break;
                case Crosswind:
                    if (Stage == 0)
                    {
                        required = 8f;
                        stable = !sample.Grounded && altitude >= 8f && altitude <= 12f && PositionError <= 6f
                            && sample.GroundSpeed <= 2f && Math.Abs(sample.VerticalSpeed) <= 1f;
                        Feedback = PositionError > 6f ? $"Drifting {PositionError:0.0} m downwind. Lean into the wind with cyclic." : HoverFeedback(sample, altitude, 6f);
                    }
                    else
                    {
                        required = 3f;
                        stable = sample.Grounded && bodyHeight >= 0.2f && bodyHeight <= 3.2f && PositionError <= 4f
                            && sample.GroundSpeed <= 0.6f && Math.Abs(sample.VerticalSpeed) <= 0.4f && sample.TiltDegrees <= 7f && TouchdownSpeed <= 1.5f;
                        if (sample.Grounded && TouchdownSpeed > 1.5f) { Fail($"Touchdown {TouchdownSpeed:0.0} m/s. Keep the drift stopped and settle below 1.5 m/s."); return; }
                        Feedback = sample.Grounded ? "Hold the cyclic into the wind while the landing is measured."
                            : PositionError > 4f ? $"Pad center is {PositionError:0.0} m away. Hold into the wind as you descend." : "Descend slowly, still leaning into the wind.";
                    }
                    break;
                case HeavyLift:
                    required = 5f;
                    peakTorque = Math.Max(peakTorque, sample.TorqueFraction);
                    if (!sample.Grounded && sample.RotorSpeed01 < 0.95f) lowRotorSeconds += dt;
                    if (lowRotorSeconds > 3f) { Fail("Rotor RPM drooped too long. Raise collective more slowly and let ground effect help the lift-off."); return; }
                    stable = !sample.Grounded && sample.Altitude >= 20f && sample.Altitude <= 30f && sample.GroundSpeed <= 3f && Math.Abs(sample.VerticalSpeed) <= 1.5f;
                    Feedback = sample.RotorSpeed01 < 0.95f ? $"Rotor RPM {sample.RotorSpeed01 * 100f:0}%. Lower collective slightly to let it recover."
                        : sample.TorqueFraction > 1f ? $"Torque {sample.TorqueFraction * 100f:0}%. Ease the collective."
                        : sample.Altitude < 20f ? "Climb steadily to 25 m; small collective increases." : sample.Altitude > 30f ? "Level off near 25 m." : "Hold 25 m with the heavy load.";
                    break;
                case SettlingWithPower:
                    required = 2f;
                    if (Stage == 0)
                    {
                        if (sample.VortexRing >= 0.5f)
                        {
                            Stage = 1;
                            entryAltitude = sample.Altitude;
                            Objective = "You are settling with power. Recover: forward cyclic to fly out of your own downwash, then collective.";
                        }
                        else if (sample.Altitude < 40f) { Fail("Too low to practise safely. Retry and start the descent higher."); return; }
                        Feedback = sample.HorizontalAirspeed > 4f ? "Stop the forward drift first." : $"Descending {-sample.VerticalSpeed:0.0} m/s. Lower collective until the sink reaches 5–7 m/s.";
                    }
                    if (Stage == 1)
                    {
                        lowestAfterEntry = Math.Min(lowestAfterEntry, sample.Altitude);
                        if (sample.Altitude < 20f) { Fail("Recovery started too late. Begin forward cyclic as soon as the sink accelerates."); return; }
                        stable = sample.VortexRing < 0.05f && sample.HorizontalAirspeed >= 10f && sample.VerticalSpeed >= -2f;
                        Feedback = sample.HorizontalAirspeed < 10f ? "Forward cyclic: fly out into clean air." : "Now add collective to arrest the descent.";
                    }
                    break;
                case Autorotation:
                    required = 2f;
                    if (!RequestEngineFailure && ElapsedSeconds >= 3f)
                    {
                        RequestEngineFailure = true;
                        Objective = "ENGINE FAILURE. Lower collective at once, hold 20–25 m/s, keep rotor RPM 90–110%, flare near 30 m, cushion with collective.";
                    }
                    if (RequestEngineFailure && !sample.Grounded && (sample.RotorSpeed01 < 0.9f || sample.RotorSpeed01 > 1.1f)) rotorOutsideSeconds += dt;
                    stable = RequestEngineFailure && sample.Grounded && sample.GroundSpeed <= 1.5f && Math.Abs(sample.VerticalSpeed) <= 0.5f && sample.TiltDegrees <= 10f;
                    if (stable) Stage = 1;
                    Feedback = !RequestEngineFailure ? "Hold straight and level." : sample.Grounded ? "Hold level while the aircraft stops."
                        : sample.RotorSpeed01 < 0.9f ? $"Rotor RPM {sample.RotorSpeed01 * 100f:0}%: lower collective!"
                        : sample.RotorSpeed01 > 1.1f ? $"Rotor RPM {sample.RotorSpeed01 * 100f:0}%: raise collective slightly."
                        : sample.Altitude > 35f ? $"Glide at 20–25 m/s. {sample.Altitude:0} m to the flare."
                        : sample.Altitude > 5f ? "Flare: aft cyclic to slow down and slow the descent." : "Cushion: level the aircraft and raise collective.";
                    break;
                case ConfinedArea:
                    required = 3f;
                    stable = sample.Grounded && bodyHeight >= 0.2f && bodyHeight <= 3.2f && PositionError <= 3f && sample.GroundSpeed <= 0.6f
                        && Math.Abs(sample.VerticalSpeed) <= 0.4f && sample.TiltDegrees <= 7f && TouchdownSpeed <= 1.2f;
                    if (sample.Grounded && TouchdownSpeed > 1.2f) { Fail($"Touchdown {TouchdownSpeed:0.0} m/s. Come to a high hover over the pad first, then descend vertically."); return; }
                    Feedback = sample.Grounded ? "Hold still while the landing is measured."
                        : PositionError > 25f ? $"{PositionError:0} m to the pad. Approach high and slow."
                        : PositionError > 3f ? $"Pad center {PositionError:0.0} m away. Stop above it before descending into the cutting."
                        : "Vertical descent: watch the rotor clearance to the slopes.";
                    break;
                default:
                    float departure = Index == 5 ? 80f : 20f;
                    float minimumHeight = Index == 5 ? 12f : 5f;
                    if (Stage == 0 && !sample.Grounded && PositionError >= departure && altitude >= minimumHeight)
                    {
                        Stage = 1;
                        SetTarget(0f, 0f);
                        Objective = Index == 5 ? "Return to the pad. Touch down below 1.8 m/s, then remain stable for 3 seconds."
                            : "Land within 2.5 m of pad center, below 1 m/s descent. Hold stable for 3 seconds.";
                    }
                    required = 3f;
                    float radius = Index == 5 ? Math.Min(home.Radius, 8f) : 2.5f;
                    float maxImpact = Index == 5 ? 1.8f : 1f;
                    stable = Stage == 1 && sample.Grounded && bodyHeight >= 0.2f && bodyHeight <= 3.2f
                        && PositionError <= radius && sample.GroundSpeed <= 0.6f && Math.Abs(sample.VerticalSpeed) <= 0.4f
                        && sample.TiltDegrees <= 7f && TouchdownSpeed <= maxImpact;
                    if (Stage == 1 && sample.Grounded && TouchdownSpeed > maxImpact)
                    {
                        Fail($"Touchdown {TouchdownSpeed:0.0} m/s. Retry and reduce descent below {maxImpact:0.0} m/s before contact.");
                        return;
                    }
                    Feedback = Stage == 0 ? $"Depart {departure:0} m and climb above {minimumHeight:0} m to begin the return."
                        : sample.GroundSpeed > 5f && PositionError < 50f ? "Brake now. Arrive over the pad at walking speed."
                        : PositionError > radius ? $"Pad center is {PositionError:0.0} m away. Use small cyclic corrections."
                        : !sample.Grounded ? $"Centered. Reduce descent below {maxImpact:0.0} m/s and lower gently."
                        : "Keep collective low and remain steady while the landing is measured.";
                    break;
            }

            StableSeconds = stable ? StableSeconds + dt : 0f;
            Progress = MissionSession.Clamp01(StableSeconds / required);
            if (StableSeconds < required) return;
            if (Index == Crosswind && Stage == 0)
            {
                Stage = 1;
                StableSeconds = 0f;
                Progress = 0f;
                Objective = "Now land in the crosswind: touch down within 4 m of center below 1.5 m/s and hold for 3 seconds.";
                return;
            }
            Complete(sample);
        }

        public bool TryClaimResult(out ChallengeResult result)
        {
            result = null;
            if (State != TrainingState.Complete || claimed) return false;
            claimed = true;
            result = Result;
            return true;
        }

        private void Complete(FlightSample sample)
        {
            State = TrainingState.Complete;
            float meanError = measuredSeconds > 0f ? errorIntegral / measuredSeconds : PositionError;
            float meanHeading = measuredSeconds > 0f ? headingIntegral / measuredSeconds : HeadingError;
            float score;
            if (Index <= 1) score = 100f - Math.Min(30f, meanError * 3f) - Math.Abs(sample.VerticalSpeed) * 5f;
            else if (Index == 2) score = 100f - Math.Min(25f, meanHeading * 0.4f) - Math.Min(15f, meanError);
            else if (Index == 3) score = 100f - Math.Abs(sample.VerticalSpeed) * 8f - Math.Abs(sample.GroundSpeed - 14f);
            else if (Index == 4) score = 100f - Math.Min(35f, brakingDistance * 0.2f) - Math.Abs(sample.VerticalSpeed) * 5f;
            else if (Index == HeavyLift) score = 100f - Math.Min(40f, Math.Max(0f, peakTorque - 1f) * 150f) - Math.Min(30f, lowRotorSeconds * 10f);
            else if (Index == SettlingWithPower) score = 100f - Math.Min(40f, Math.Max(0f, entryAltitude - lowestAfterEntry) * 0.5f);
            else if (Index == Autorotation) score = 100f - Math.Min(40f, TouchdownSpeed * 8f) - Math.Min(30f, rotorOutsideSeconds * 3f);
            else score = 100f - Math.Min(25f, PositionError * 3f) - Math.Min(25f, TouchdownSpeed * 10f);
            // Time is deliberately a minor factor: controlled flight is the training goal.
            score = Math.Max(0f, score - Math.Min(5f, Math.Max(0f, ElapsedSeconds - 90f) / 30f));
            Feedback = Index <= 1 ? $"Mean position error {meanError:0.0} m. Anticipate drift with smaller, earlier corrections."
                : Index == 2 ? $"Mean heading error {meanHeading:0}°. Begin stopping the yaw before reaching your heading."
                : Index == 3 ? "Steady forward flight. Practice the braking drill before faster approaches."
                : Index == 4 ? $"Braking distance {brakingDistance:0} m. Remember that distance when planning an approach."
                : Index == HeavyLift ? $"Peak torque {peakTorque * 100f:0}%; rotor below 95% for {lowRotorSeconds:0.0} s. Smooth, patient collective saves power."
                : Index == SettlingWithPower ? $"Recovered after losing {Math.Max(0f, entryAltitude - lowestAfterEntry):0} m. Airspeed, not collective, is the way out."
                : Index == Autorotation ? $"Touchdown {TouchdownSpeed:0.0} m/s; rotor outside 90–110% for {rotorOutsideSeconds:0.0} s. Flare earlier for a softer arrival."
                : $"Landing offset {PositionError:0.0} m; impact {TouchdownSpeed:0.0} m/s. Settle over the center before lowering.";
            Result = new ChallengeResult
            {
                AttemptId = Guid.NewGuid().ToString("N"), ContractId = "training-" + Index,
                Title = Names[Index], Mode = "Training", Score = score, Grade = MissionSession.GradeFor(score),
                Seconds = ElapsedSeconds, Payout = 0, AccuracyMetres = Index <= 2 ? meanError : PositionError,
                TouchdownMetresPerSecond = TouchdownSpeed, Assists = Assists,
                CompletedUtc = DateTime.UtcNow.ToString("O"), Feedback = Feedback
            };
        }

        private string HoverFeedback(FlightSample sample, float altitude, float radius)
        {
            if (altitude < 8f) return "Raise collective gradually to reach 8–12 m; ease it down as the climb slows.";
            if (altitude > 12f) return "Reduce collective a little, then restore it before descending below 8 m.";
            if (PositionError > radius) return $"Pad offset {PositionError:0.0} m. Nudge cyclic toward center, then brake the drift.";
            if (sample.GroundSpeed > 2f) return "Apply a small cyclic correction against your drift, then center it.";
            if (Math.Abs(sample.VerticalSpeed) > 1f) return "Use a small collective correction to settle the vertical speed.";
            return $"Steady: {StableSeconds:0.0} seconds. Keep looking at the horizon and pad.";
        }

        private void SetTarget(float distance, float height)
        {
            TargetX = home.X + forwardX * distance;
            TargetY = home.Y + height + 1.5f;
            TargetZ = home.Z + forwardZ * distance;
        }

        private static float DeltaAngle(float a, float b)
        {
            float difference = (b - a) % 360f;
            return difference > 180f ? difference - 360f : difference < -180f ? difference + 360f : difference;
        }
    }
}
