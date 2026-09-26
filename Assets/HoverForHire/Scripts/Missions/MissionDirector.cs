using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace HoverForHire
{
    /// <summary>Unity adapter for mission/training observations, payload, respawn, and local progression.</summary>
    public sealed class MissionDirector : MonoBehaviour
    {
        public HelicopterController Aircraft;
        public LandingZone[] Zones;
        [Tooltip("Optional alternate local save path, set before Awake. Empty uses the normal per-user progression file.")]
        public string ProgressionPathOverride;
        [Min(60f)] public float ShiftDurationSeconds = 900f;
        public string AssistSnapshot = "";
        /// <summary>The player's chosen realism. Drills layer their required effects on a copy of it.</summary>
        public RealismSettings PlayerRealism = new RealismSettings();
        /// <summary>Display units for objectives and feedback; the HUD sets it from the pilot's settings.</summary>
        public UnitSystem Units = UnitSystem.Metric;
        /// <summary>Shared island wind, configured from realism (null in fixtures without wind).</summary>
        public WindField Wind;
        [Tooltip("Prevailing wind direction (meteorological, degrees) when realism enables wind.")]
        public float PrevailingWindFrom = 240f;
        public GameMode Mode { get; private set; } = GameMode.FreeFlight;
        public MissionSession CurrentMission { get; private set; }
        public TrainingSession CurrentTraining { get; private set; }
        public ProgressionData Progression { get; private set; }
        public ChallengeResult LastResult { get; private set; }
        public int Earnings { get; private set; }
        public int DeliveriesThisShift { get; private set; }
        public float RemainingSeconds { get; private set; }
        public string SaveWarning { get; private set; } = "";
        public bool ShiftFinished => Mode == GameMode.DeliveryShift && RemainingSeconds <= 0f;
        public int CompletedDeliveries => Progression?.CompletedDeliveries ?? 0;
        public int LifetimeEarnings => Progression?.TotalEarnings ?? 0;
        public ContractDefinition CurrentContract => CurrentMission?.Contract;
        public MissionState MissionState => CurrentMission?.State ?? HoverForHire.MissionState.Available;
        public string Grade => LastResult?.Grade ?? "—";
        public float Payload => Aircraft != null ? Aircraft.PayloadKg : 0f;
        public float DwellProgress => CurrentMission?.DwellProgress ?? CurrentTraining?.Progress ?? 0f;
        public float ComfortPercent => CurrentMission?.Comfort ?? 100f;
        public float CargoConditionPercent => CurrentMission?.CargoCondition ?? 100f;
        public int TrainingIndex => CurrentTraining?.Index ?? 0;
        public string TrainingName => CurrentTraining != null ? TrainingSession.Names[CurrentTraining.Index] : "";
        public string TrainingFeedback => CurrentTraining?.Feedback ?? "";
        public float TrainingProgress => CurrentTraining?.Progress ?? 0f;
        public IReadOnlyList<ContractDefinition> Contracts => contracts;
        /// <summary>Up to three jobs to choose from between deliveries; the first starts where the aircraft is, when possible.</summary>
        public IReadOnlyList<ContractDefinition> Offers => offers;
        public int SelectedOffer { get; private set; }
        /// <summary>Certifications earned from completed drills.</summary>
        public Certification EarnedCertifications => Certifications.Earned(Progression?.CompletedTrainingMask ?? 0);
        public event Action<string> FeedbackEvent;
        public event Action<ChallengeResult> ResultRecorded;

        private readonly List<ContractDefinition> contracts = new List<ContractDefinition>();
        private readonly List<ContractDefinition> offers = new List<ContractDefinition>();
        private ProgressionStore store;
        private HelicopterController boundAircraft;
        private Vector3 lastVelocity;
        private bool hasVelocity, suppressReset, modeStarted, savePending;
        private int offersDealt;
        private float shiftScoreTotal;

        public LandingZone TargetZone
        {
            get
            {
                if (Mode == GameMode.Training)
                    return CurrentTraining != null && CurrentTraining.Index >= 5 && CurrentTraining.Stage > 0 ? HomeZone : null;
                if (Mode != GameMode.DeliveryShift || CurrentMission == null || ShiftFinished) return null;
                if (CurrentMission.State == HoverForHire.MissionState.Delivered || CurrentMission.State == HoverForHire.MissionState.Failed) return null;
                return FindZone(CurrentMission.Target.Id);
            }
        }

        public Vector3 ObjectivePosition => Mode == GameMode.Training && CurrentTraining != null
            ? new Vector3(CurrentTraining.TargetX, CurrentTraining.TargetY, CurrentTraining.TargetZ)
            : TargetZone != null ? TargetZone.transform.position : Aircraft != null ? Aircraft.transform.position : Vector3.zero;

        public string CurrentObjective
        {
            get
            {
                if (Aircraft == null) return "Waiting for the helicopter.";
                if (Mode == GameMode.FreeFlight) return Aircraft.Crashed ? "Aircraft damaged. Retry to return to home base." : "Explore Port Meridian. Practice a controlled landing on any marked pad.";
                if (Mode == GameMode.Training) return CurrentTraining?.Objective ?? "Choose a training drill.";
                if (ShiftFinished) return $"Shift complete · {DeliveriesThisShift} deliveries · ${Earnings} · grade {ShiftGrade}";
                if (CurrentMission == null) return "No contract available. Landing locations are missing.";
                switch (CurrentMission.State)
                {
                    case HoverForHire.MissionState.Available:
                        return offers.Count > 1 ? $"Choose a job. Enter takes offer {SelectedOffer + 1}; the Flight Desk shows every offer."
                            : "Accept job: " + CurrentMission.Contract.Title;
                    case HoverForHire.MissionState.Accepted:
                    case HoverForHire.MissionState.Pickup: return "Pick up " + ServiceDescription(CurrentMission.Contract) + " at " + CurrentMission.Contract.Pickup.Name;
                    case HoverForHire.MissionState.Transport: return "Deliver " + ServiceDescription(CurrentMission.Contract) + " to " + CurrentMission.Contract.Destination.Name;
                    case HoverForHire.MissionState.Delivered: return $"Delivered · +${CurrentMission.Result.Payout} · Grade {CurrentMission.Result.Grade}. Accept the next job.";
                    default: return CurrentMission.FailureReason;
                }
            }
        }

        public string ShiftGrade => DeliveriesThisShift > 0 ? MissionSession.GradeFor(shiftScoreTotal / DeliveriesThisShift) : "—";

        public string StatusText
        {
            get
            {
                if (Mode == GameMode.FreeFlight) return "No timer · no payload · reset returns to home base";
                if (Mode == GameMode.Training)
                    return CurrentTraining == null ? $"{TrainingSession.Names.Length} short drills, with immediate retries."
                        : CurrentTraining.State == TrainingState.Complete ? $"DRILL COMPLETE · {CurrentTraining.Result.Grade} · {CurrentTraining.Feedback}"
                        : CurrentTraining.State == TrainingState.Failed ? "RETRY · " + CurrentTraining.Feedback : CurrentTraining.Feedback;
                if (ShiftFinished) return "Start another shift or return to free flight. Completed earnings are saved.";
                if (CurrentMission == null) return "No landing zones configured.";
                if (CurrentMission.State == HoverForHire.MissionState.Available) return OfferList();
                if (CurrentMission.State == HoverForHire.MissionState.Failed) return "Retry restarts this contract at its pickup with no payload.";
                if (CurrentMission.State == HoverForHire.MissionState.Delivered) return CurrentMission.Result.Feedback;
                if (CurrentMission.DwellSeconds > 0f)
                    return $"{(CurrentMission.State == HoverForHire.MissionState.Transport ? "Unloading" : "Loading")} · hold steady {CurrentMission.DwellSeconds:0.0}/{CurrentMission.Contract.DwellSeconds:0.0}s";
                return $"Land inside the pad · speed ≤ {UnitFormat.FormatDriftSpeed(CurrentMission.Contract.MaximumGroundSpeed, Units)} · level · hold {CurrentMission.Contract.DwellSeconds:0}s";
            }
        }

        public int UnlockedContractCount
        {
            get { int count = 0; foreach (ContractDefinition contract in contracts) if (IsAvailable(contract)) count++; return count; }
        }

        /// <summary>A contract is offered once enough deliveries are complete and its certifications are earned.</summary>
        public bool IsAvailable(ContractDefinition contract)
            => contract != null && CompletedDeliveries >= contract.RequiredDeliveries
                && Certifications.Satisfies(Progression?.CompletedTrainingMask ?? 0, contract.RequiredCertification);

        /// <summary>One offer on a line: number, title, payload, par and pay.</summary>
        public string OfferSummary(int index)
        {
            if (index < 0 || index >= offers.Count) return "";
            ContractDefinition offer = offers[index];
            return $"{(index == SelectedOffer ? "›" : " ")} {index + 1}  {offer.Title} · {offer.PayloadKg:0} kg · par {ClockText(offer.ExpectedSeconds)} · ${offer.BasePay}";
        }

        /// <summary>Route, load, par, deadline, pay and wind at the destination, for the Flight Desk.</summary>
        public string OfferDetail(int index)
        {
            if (index < 0 || index >= offers.Count) return "";
            ContractDefinition offer = offers[index];
            string wind = "";
            if (Wind != null)
            {
                Vector3 destination = new Vector3(offer.Destination.X, offer.Destination.Y + 10f, offer.Destination.Z);
                Vector3 air = Wind.WindAt(destination);
                float speed = new Vector2(air.x, air.z).magnitude;
                if (speed > 0.5f) wind = " · wind " + UnitFormat.FormatSpeed(speed, Units);
            }
            string cargo = offer.Type == ContractType.Passengers ? "passengers" : "cargo";
            return $"{offer.Title} · {offer.Pickup.Name} → {offer.Destination.Name} · {offer.PayloadKg:0} kg {cargo} · " +
                $"{UnitFormat.FormatDistance(offer.DistanceMetres, Units)} · par {ClockText(offer.ExpectedSeconds)} · limit {ClockText(offer.DeadlineSeconds)} · ${offer.BasePay}{wind}";
        }

        /// <summary>Jobs still locked behind certifications, with what earns the first missing one.</summary>
        public string LockedHint
        {
            get
            {
                int locked = 0;
                Certification missing = Certification.None;
                foreach (ContractDefinition contract in contracts)
                {
                    if (CompletedDeliveries < contract.RequiredDeliveries || IsAvailable(contract)) continue;
                    locked++;
                    missing |= contract.RequiredCertification & ~EarnedCertifications;
                }
                if (locked == 0) return "";
                foreach (Certification certification in Certifications.All)
                    if ((missing & certification) != 0)
                        return $"{locked} more {(locked == 1 ? "job needs" : "jobs need")} certification · {Certifications.Requirement(certification)}";
                return "";
            }
        }

        private string OfferList()
        {
            string list = "";
            for (int i = 0; i < offers.Count; i++) list += (i > 0 ? "\n" : "") + OfferSummary(i);
            return list;
        }

        private static string ClockText(float seconds) => $"{Mathf.FloorToInt(Mathf.Max(0f, seconds) / 60f)}:{Mathf.FloorToInt(Mathf.Max(0f, seconds) % 60f):00}";

        private LandingZone HomeZone => Zones != null && Zones.Length > 0 ? Zones[0] : null;

        private void Awake()
        {
            store = new ProgressionStore(string.IsNullOrWhiteSpace(ProgressionPathOverride)
                ? Path.Combine(Application.persistentDataPath, "progression.json") : ProgressionPathOverride);
            Progression = store.Load();
            if (store.RecoveredBackup) SaveWarning = "Recovered progression from the previous save.";
            else if (!string.IsNullOrEmpty(store.LastError)) SaveWarning = "Progression could not be loaded: " + store.LastError;
        }

        private void Start()
        {
            BindAircraft();
            if (!modeStarted) StartFreeFlight();
        }

        private void FixedUpdate() => Tick(Time.fixedDeltaTime);

        public void Tick(float deltaSeconds)
        {
            if (Aircraft == null || Aircraft.Body == null || deltaSeconds <= 0f) return;
            BindAircraft();
            Vector3 velocity = Aircraft.Body.linearVelocity;
            Vector3 position = Aircraft.Body.position;
            float acceleration = hasVelocity ? (velocity - lastVelocity).magnitude / deltaSeconds : 0f;
            lastVelocity = velocity; hasVelocity = true;
            var sample = new FlightSample
            {
                X = position.x, Y = position.y, Z = position.z, Grounded = Aircraft.Grounded, Crashed = Aircraft.Crashed,
                GroundSpeed = Aircraft.GroundSpeed, VerticalSpeed = Aircraft.VerticalSpeed, Heading = Aircraft.Heading,
                Altitude = Aircraft.AltitudeAGL, TiltDegrees = Vector3.Angle(Aircraft.Body.rotation * Vector3.up, Vector3.up),
                Acceleration = acceleration, AngularSpeedDegrees = Aircraft.Body.angularVelocity.magnitude * Mathf.Rad2Deg,
                HorizontalAirspeed = Aircraft.HorizontalAirspeed, RotorSpeed01 = Aircraft.RotorSpeed01,
                TorqueFraction = Aircraft.TorqueFraction, VortexRing = Aircraft.VortexRingSeverity, EngineFailed = Aircraft.EngineFailed
            };
            string assists = ActiveAssists();
            // Logbook: airborne time in every mode.
            if (Progression != null && !Aircraft.Grounded && !Aircraft.Crashed) Progression.FlightSeconds += deltaSeconds;
            if (Mode == GameMode.Training && CurrentTraining != null)
            {
                CurrentTraining.RecordAssists(assists);
                CurrentTraining.Tick(deltaSeconds, sample);
                if (CurrentTraining.RequestEngineFailure && !Aircraft.EngineFailed && !Aircraft.Crashed) Aircraft.FailEngine();
                if (CurrentTraining.TryClaimResult(out ChallengeResult result)) RecordResult(result);
            }
            else if (Mode == GameMode.DeliveryShift && !ShiftFinished)
            {
                RemainingSeconds = Mathf.Max(0f, RemainingSeconds - deltaSeconds);
                if (ShiftFinished)
                {
                    CurrentMission?.Fail("Shift ended before delivery.");
                    Aircraft.SetPayload(0f);
                    FeedbackEvent?.Invoke($"Shift complete · ${Earnings} · grade {ShiftGrade}");
                    return;
                }
                if (CurrentMission == null) return;
                HoverForHire.MissionState before = CurrentMission.State;
                CurrentMission.RecordAssists(assists);
                CurrentMission.Tick(deltaSeconds, sample);
                if (Mathf.Abs(Aircraft.PayloadKg - CurrentMission.PayloadKg) > 0.01f) Aircraft.SetPayload(CurrentMission.PayloadKg);
                if (before != CurrentMission.State && CurrentMission.State == HoverForHire.MissionState.Transport)
                    FeedbackEvent?.Invoke("Loaded. Fly to " + CurrentMission.Contract.Destination.Name);
                if (CurrentMission.TryClaimPayout(out ChallengeResult result)) RecordResult(result);
            }
        }

        public void StartFreeFlight()
        {
            BeginMode(GameMode.FreeFlight);
            ResetAircraft(HomeZone);
            FeedbackEvent?.Invoke("Free flight · the island is yours");
        }

        public void StartShift()
        {
            BeginMode(GameMode.DeliveryShift);
            RemainingSeconds = ShiftDurationSeconds;
            Earnings = DeliveriesThisShift = offersDealt = 0;
            shiftScoreTotal = 0f;
            BuildContracts();
            DealOffers(HomeZone != null ? HomeZone.Definition.Id : null);
            ResetAircraft(HomeZone);
            FeedbackEvent?.Invoke("15 minute shift · accept your first job");
        }

        public void StartTraining(int index)
        {
            BeginMode(GameMode.Training);
            if (HomeZone == null || Aircraft == null) return;
            index = Mathf.Clamp(index, 0, TrainingSession.Names.Length - 1);
            // Each realism drill forces on the effect it teaches, on top of the player's own settings.
            RealismSettings drill = PlayerRealism.Clone();
            WindStrength windStrength = drill.Wind;
            float gust = drill.Gustiness, windFrom = PrevailingWindFrom;
            LandingZone target = null;
            switch (index)
            {
                case TrainingSession.Crosswind: windStrength = WindStrength.Moderate; gust = Mathf.Max(gust, 0.3f); windFrom = 90f; break;
                case TrainingSession.HeavyLift: drill.PowerLimits = true; break;
                case TrainingSession.SettlingWithPower: drill.VortexRingState = true; break;
                case TrainingSession.Autorotation: drill.PowerLimits = true; drill.EngineFailures = FailureMode.Off; break;
                case TrainingSession.ConfinedArea: target = Zones.Length > 6 ? Zones[6] : HomeZone; break;
            }
            ApplyRealism(drill, windStrength, gust, windFrom);
            switch (index)
            {
                case TrainingSession.SettlingWithPower:
                    // High enough to enter the ring and fly out of it with room to spare.
                    ResetAirborne(HomeZone.transform.position + Vector3.up * 220f, 0f, 0f);
                    break;
                case TrainingSession.Autorotation:
                    // Gliding west toward the airport's open northern apron, clear of the terminal and hangar.
                    Vector3 start = HomeZone.transform.position + new Vector3(520f, 0f, 40f);
                    start.y = Mathf.Max(start.y, IslandWorld.MeshHeight(start.x, start.z)) + 180f;
                    ResetAirborne(start, 270f, 25f);
                    break;
                case TrainingSession.ConfinedArea:
                    ResetAirborne(target.transform.position + new Vector3(0f, 60f, -150f), 0f, 0f);
                    break;
                default:
                    ResetAircraft(HomeZone);
                    break;
            }
            if (index == TrainingSession.HeavyLift) Aircraft.SetPayload(Aircraft.Tuning.MaximumPayloadKg);
            CurrentTraining = new TrainingSession(index, HomeZone.Definition, 0f, ActiveAssists(), target != null ? target.Definition : (ZoneDefinition?)null, Units);
            FeedbackEvent?.Invoke("Training · " + TrainingName);
        }

        /// <summary>Apply the player's realism (and its wind) to the aircraft, e.g. after changing settings.</summary>
        public void ApplyPlayerRealism() => ApplyRealism(PlayerRealism.Clone(), PlayerRealism.Wind, PlayerRealism.Gustiness, PrevailingWindFrom);

        private void ApplyRealism(RealismSettings effective, WindStrength wind, float gustiness, float fromDegrees)
        {
            effective.Sanitize();
            if (Aircraft != null) Aircraft.Realism = effective;
            Wind?.Configure(wind, gustiness, fromDegrees);
        }

        /// <summary>Drill start in flight: level, heading given in degrees, forward speed in m/s, collective primed to hold height.</summary>
        private void ResetAirborne(Vector3 position, float heading, float speed)
        {
            suppressReset = true;
            try
            {
                Quaternion rotation = Quaternion.Euler(0f, heading, 0f);
                Aircraft.SetPayload(0f);
                Aircraft.ResetAt(position, rotation, rotation * Vector3.forward * speed);
                float collective = Aircraft.HoverCollective * (speed > 5f ? 0.95f : 1f);
                if (Aircraft.InputSource is FlightInput input) input.ResetCommand(collective);
                Aircraft.PrimeCollective(collective);
                hasVelocity = false;
            }
            finally { suppressReset = false; }
        }

        /// <summary>Accept the selected offer; after a delivery, first deal fresh offers from where the aircraft landed.</summary>
        public void AcceptNextJob()
        {
            if (Mode != GameMode.DeliveryShift || ShiftFinished) return;
            if (CurrentMission == null || CurrentMission.State == HoverForHire.MissionState.Delivered) DealOffers(LastPadId);
            if (CurrentMission != null && CurrentMission.Accept(ActiveAssists())) FeedbackEvent?.Invoke("Job accepted · " + CurrentMission.Contract.Title);
        }

        /// <summary>Accept one of the current offers by its index.</summary>
        public void AcceptOffer(int index)
        {
            if (Mode != GameMode.DeliveryShift || ShiftFinished) return;
            if (CurrentMission == null || CurrentMission.State == HoverForHire.MissionState.Delivered) DealOffers(LastPadId);
            if (SelectOffer(index)) AcceptNextJob();
        }

        /// <summary>Highlight another offer before accepting; its pickup becomes the HUD target.</summary>
        public bool SelectOffer(int index)
        {
            if (index < 0 || index >= offers.Count || CurrentMission == null || CurrentMission.State != HoverForHire.MissionState.Available) return false;
            SelectedOffer = index;
            CurrentMission = new MissionSession(offers[index]) { Units = Units };
            return true;
        }

        public void BrowseNextJob()
        {
            if (Mode != GameMode.DeliveryShift || ShiftFinished) return;
            if (CurrentMission == null || CurrentMission.State == HoverForHire.MissionState.Delivered) DealOffers(LastPadId);
            else if (CurrentMission.State == HoverForHire.MissionState.Available && offers.Count > 0) SelectOffer((SelectedOffer + 1) % offers.Count);
        }

        /// <summary>Where the aircraft finished its last job, or home.</summary>
        private string LastPadId => CurrentMission != null && CurrentMission.State == HoverForHire.MissionState.Delivered
            ? CurrentMission.Contract.Destination.Id : HomeZone != null ? HomeZone.Definition.Id : null;

        public void Interact()
        {
            if (Mode == GameMode.DeliveryShift)
            {
                if (CurrentMission?.State == HoverForHire.MissionState.Failed) Retry();
                else AcceptNextJob();
            }
            else if (Mode == GameMode.Training && CurrentTraining != null && CurrentTraining.State == TrainingState.Complete)
                StartTraining((CurrentTraining.Index + 1) % TrainingSession.Names.Length);
        }

        public void Retry()
        {
            if (Mode == GameMode.Training) { StartTraining(TrainingIndex); return; }
            if (Mode == GameMode.FreeFlight) { ResetAircraft(HomeZone); return; }
            if (ShiftFinished) { StartShift(); return; }
            if (CurrentMission == null) { DealOffers(HomeZone != null ? HomeZone.Definition.Id : null); ResetAircraft(HomeZone); return; }
            if (CurrentMission.State == HoverForHire.MissionState.Delivered)
            {
                // A completed contract remains completed; resetting cannot reopen its payout.
                ResetAircraft(FindZone(CurrentMission.Contract.Destination.Id));
                return;
            }
            CurrentMission.Retry(ActiveAssists());
            ResetAircraft(FindZone(CurrentMission.Contract.Pickup.Id));
            FeedbackEvent?.Invoke("Contract reset · payload cleared · shift timer continues");
        }

        private void BeginMode(GameMode mode)
        {
            modeStarted = true;
            BindAircraft();
            if (mode != GameMode.Training) ApplyPlayerRealism();
            CurrentMission?.Fail("Flight mode changed.");
            CurrentTraining?.Fail("Flight mode changed.");
            CurrentMission = null; CurrentTraining = null; LastResult = null;
            Mode = mode; RemainingSeconds = 0f;
            if (Aircraft != null) Aircraft.SetPayload(0f);
        }

        private void BindAircraft()
        {
            if (boundAircraft == Aircraft) return;
            if (boundAircraft != null)
            {
                boundAircraft.CrashedEvent -= OnCrash;
                boundAircraft.Touchdown -= OnTouchdown;
                boundAircraft.ResetPerformed -= OnExternalReset;
            }
            boundAircraft = Aircraft;
            if (boundAircraft != null)
            {
                boundAircraft.CrashedEvent += OnCrash;
                boundAircraft.Touchdown += OnTouchdown;
                boundAircraft.ResetPerformed += OnExternalReset;
            }
        }

        private void OnCrash()
        {
            CurrentMission?.Fail("Aircraft damaged. Retry to restart the contract.");
            CurrentTraining?.Fail("Aircraft damaged. Retry and reduce touchdown speed.");
            if (Aircraft != null) Aircraft.SetPayload(0f);
            FeedbackEvent?.Invoke("Aircraft damaged · retry when ready");
        }

        private void OnTouchdown(float speed)
        {
            CurrentMission?.RecordTouchdown(speed);
            CurrentTraining?.RecordTouchdown(speed);
            // Logbook: a touchdown below the hard-landing limit is a landing.
            if (Progression != null && Aircraft != null && !Aircraft.Crashed && Aircraft.Tuning != null && speed < Aircraft.Tuning.CrashVerticalSpeed)
            {
                Progression.Landings++;
                savePending = true;
            }
        }

        /// <summary>Save pending logbook and livery changes now (the Flight Desk calls this after purchases).</summary>
        public void SaveNow()
        {
            savePending = true;
            SaveProgression();
        }

        private void OnExternalReset()
        {
            hasVelocity = false;
            if (suppressReset) return;
            CurrentMission?.Fail("Aircraft reset. Retry to restart the contract with an empty cabin.");
            CurrentTraining?.Fail("Aircraft reset. Retry to restart this drill.");
            if (Aircraft != null) Aircraft.SetPayload(0f);
        }

        private void ResetAircraft(LandingZone zone)
        {
            if (Aircraft == null || zone == null) return;
            suppressReset = true;
            try
            {
                if (Aircraft.InputSource is FlightInput input) input.ResetCommand();
                Aircraft.SetPayload(0f);
                Aircraft.ResetAt(zone.transform.position + Vector3.up * 1.55f, Quaternion.identity);
                hasVelocity = false;
            }
            finally { suppressReset = false; }
        }

        private string ActiveAssists() => !string.IsNullOrWhiteSpace(AssistSnapshot) ? AssistSnapshot
            : Aircraft != null && Aircraft.Assists != null ? Aircraft.Assists.Summary + (Aircraft.HoverHold.Engaged ? " HOLD" : "") : "Not recorded";

        private void RecordResult(ChallengeResult result)
        {
            if (Progression == null) Progression = new ProgressionData();
            if (Aircraft != null && Aircraft.Realism != null) result.Realism = Aircraft.Realism.Summary;
            if (!Progression.Apply(result)) return;
            LastResult = result;
            if (result.Mode == "Delivery Shift")
            {
                Earnings += result.Payout;
                DeliveriesThisShift++;
                shiftScoreTotal += result.Score;
            }
            savePending = true;
            SaveProgression();
            FeedbackEvent?.Invoke(result.Mode == "Training" ? "Drill complete · grade " + result.Grade : $"Delivered · +${result.Payout} · grade {result.Grade}");
            ResultRecorded?.Invoke(result);
        }

        private void SaveProgression()
        {
            if (!savePending || store == null || Progression == null) return;
            if (!store.Save(Progression))
            {
                SaveWarning = "Progress is held for this session but could not be saved: " + store.LastError;
                Debug.LogWarning(SaveWarning);
            }
            else { SaveWarning = ""; savePending = false; }
        }

        private void OnApplicationPause(bool paused) { if (paused) SaveProgression(); }
        private void OnApplicationQuit() => SaveProgression();

        /// <summary>
        /// Deal up to three distinct offers from the available contracts. The first starts at <paramref name="padId"/>
        /// when any does, so a new job never begins with an empty repositioning flight. Seeded by progress, so the
        /// same pilot record always sees the same offers.
        /// </summary>
        private void DealOffers(string padId)
        {
            if (contracts.Count == 0) BuildContracts();
            offers.Clear();
            SelectedOffer = 0;
            var available = new List<ContractDefinition>();
            foreach (ContractDefinition contract in contracts) if (IsAvailable(contract)) available.Add(contract);
            offers.AddRange(OfferBoard.Deal(available, padId, unchecked(CompletedDeliveries * 7919 + offersDealt * 104729 + 17)));
            offersDealt++;
            CurrentMission = offers.Count > 0 ? new MissionSession(offers[0]) { Units = Units } : null;
        }

        private void BuildContracts()
        {
            contracts.Clear();
            if (Zones == null || Zones.Length < 3) return;
            AddContract("town-commute", "Town connection", ContractType.Passengers, 0, 1, 160f, 190, 0);
            AddContract("dock-parcel", "Ferry provisions", ContractType.Cargo, 1, 2, 230f, 250, 0);
            AddContract("yard-spares", "Workshop spares", ContractType.Cargo, 2, 3, 300f, 330, 2);
            AddContract("clinic-transfer", "Clinic staff transfer", ContractType.Passengers, 1, 4, 160f, 320, 2, Certification.Rooftop);
            AddContract("orchard-crates", "Orchard produce", ContractType.Cargo, 5, 3, 330f, 390, 2);
            AddContract("ridge-crew", "Ridge survey crew", ContractType.Passengers, 3, 6, 240f, 430, 4, Certification.Mountain);
            AddContract("summit-stores", "Summit supplies", ContractType.Cargo, 3, 7, 350f, 490, 4, Certification.Mountain);
            AddContract("light-keeper", "Lighthouse relief", ContractType.Passengers, 2, 8, 160f, 440, 4, Certification.Coastal);
            AddContract("cove-samples", "East cove samples", ContractType.Cargo, 9, 4, 180f, 440, 4, Certification.Coastal | Certification.Rooftop);
            AddContract("summit-medevac", "Summit medical evacuation", ContractType.Passengers, 7, 4, 180f, 640, 4,
                Certification.Emergency | Certification.Mountain | Certification.Rooftop);
        }

        /// <summary>Par for a job: a minute for takeoff, loading and landing, plus cruise at 25 m/s.</summary>
        public static float ParSeconds(float distanceMetres) => 60f + Mathf.Max(0f, distanceMetres) / 25f;

        private void AddContract(string id, string title, ContractType type, int pickup, int destination, float kg, int pay, int required,
            Certification certification = Certification.None)
        {
            if (pickup >= Zones.Length || destination >= Zones.Length || Zones[pickup] == null || Zones[destination] == null) return;
            float distance = Vector3.Distance(Zones[pickup].transform.position, Zones[destination].transform.position);
            float par = ParSeconds(distance);
            contracts.Add(new ContractDefinition
            {
                Id = id, Title = title, Type = type, Pickup = Zones[pickup].Definition, Destination = Zones[destination].Definition,
                PayloadKg = kg, BasePay = pay, RequiredDeliveries = required, RequiredCertification = certification, DistanceMetres = distance,
                ExpectedSeconds = par, DeadlineSeconds = par * (required == 0 ? 3f : 2f), DwellSeconds = required >= 4 ? 4f : 3f,
                MaximumGroundSpeed = required >= 4 ? 0.5f : 0.8f, MaximumTiltDegrees = required >= 4 ? 6f : 8f
            });
        }

        private LandingZone FindZone(string id)
        {
            if (Zones != null) foreach (LandingZone zone in Zones) if (zone != null && zone.Definition.Id == id) return zone;
            return null;
        }

        private static string ServiceDescription(ContractDefinition contract) => contract.Type == ContractType.Passengers
            ? "passengers (" + contract.PayloadKg.ToString("0") + " kg)" : "cargo (" + contract.PayloadKg.ToString("0") + " kg)";

        private void OnDestroy()
        {
            if (boundAircraft == null) return;
            boundAircraft.CrashedEvent -= OnCrash;
            boundAircraft.Touchdown -= OnTouchdown;
            boundAircraft.ResetPerformed -= OnExternalReset;
        }
    }
}
