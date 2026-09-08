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
        [Min(60f)] public float ShiftDurationSeconds = 900f;
        public string AssistSnapshot = "";
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
        public event Action<string> FeedbackEvent;
        public event Action<ChallengeResult> ResultRecorded;

        private readonly List<ContractDefinition> contracts = new List<ContractDefinition>();
        private ProgressionStore store;
        private HelicopterController boundAircraft;
        private Vector3 lastVelocity;
        private bool hasVelocity, suppressReset, modeStarted;
        private int nextContract;
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
                    case HoverForHire.MissionState.Available: return "Accept job: " + CurrentMission.Contract.Title;
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
                    return CurrentTraining == null ? "Seven short drills, with immediate retries."
                        : CurrentTraining.State == TrainingState.Complete ? $"DRILL COMPLETE · {CurrentTraining.Result.Grade} · {CurrentTraining.Feedback}"
                        : CurrentTraining.State == TrainingState.Failed ? "RETRY · " + CurrentTraining.Feedback : CurrentTraining.Feedback;
                if (ShiftFinished) return "Start another shift or return to free flight. Completed earnings are saved.";
                if (CurrentMission == null) return "No landing zones configured.";
                if (CurrentMission.State == HoverForHire.MissionState.Available)
                    return $"${CurrentMission.Contract.BasePay} base · {CurrentMission.Contract.PayloadKg:0} kg · target {CurrentMission.Contract.ExpectedSeconds:0}s · {UnlockedContractCount}/{contracts.Count} contracts unlocked";
                if (CurrentMission.State == HoverForHire.MissionState.Failed) return "Retry restarts this contract at its pickup with no payload.";
                if (CurrentMission.State == HoverForHire.MissionState.Delivered) return CurrentMission.Result.Feedback;
                if (CurrentMission.DwellSeconds > 0f)
                    return $"{(CurrentMission.State == HoverForHire.MissionState.Transport ? "Unloading" : "Loading")} · hold steady {CurrentMission.DwellSeconds:0.0}/{CurrentMission.Contract.DwellSeconds:0.0}s";
                return $"Land inside the pad · speed ≤ {CurrentMission.Contract.MaximumGroundSpeed:0.0} m/s · level · hold {CurrentMission.Contract.DwellSeconds:0}s";
            }
        }

        public int UnlockedContractCount
        {
            get { int count = 0; foreach (ContractDefinition contract in contracts) if (CompletedDeliveries >= contract.RequiredDeliveries) count++; return count; }
        }

        private LandingZone HomeZone => Zones != null && Zones.Length > 0 ? Zones[0] : null;

        private void Awake()
        {
            store = new ProgressionStore(Path.Combine(Application.persistentDataPath, "progression.json"));
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
                Acceleration = acceleration, AngularSpeedDegrees = Aircraft.Body.angularVelocity.magnitude * Mathf.Rad2Deg
            };
            string assists = ActiveAssists();
            if (Mode == GameMode.Training && CurrentTraining != null)
            {
                CurrentTraining.RecordAssists(assists);
                CurrentTraining.Tick(deltaSeconds, sample);
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
            Earnings = DeliveriesThisShift = nextContract = 0;
            shiftScoreTotal = 0f;
            BuildContracts();
            OfferNextJob();
            ResetAircraft(HomeZone);
            FeedbackEvent?.Invoke("15 minute shift · accept your first job");
        }

        public void StartTraining(int index)
        {
            BeginMode(GameMode.Training);
            ResetAircraft(HomeZone);
            if (HomeZone == null || Aircraft == null) return;
            CurrentTraining = new TrainingSession(index, HomeZone.Definition, 0f, ActiveAssists());
            FeedbackEvent?.Invoke("Training · " + TrainingName);
        }

        public void AcceptNextJob()
        {
            if (Mode != GameMode.DeliveryShift || ShiftFinished) return;
            if (CurrentMission == null || CurrentMission.State == HoverForHire.MissionState.Delivered) OfferNextJob();
            if (CurrentMission != null && CurrentMission.Accept(ActiveAssists())) FeedbackEvent?.Invoke("Job accepted · " + CurrentMission.Contract.Title);
        }

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
            if (CurrentMission == null) { OfferNextJob(); ResetAircraft(HomeZone); return; }
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
            : Aircraft != null && Aircraft.Assists != null ? Aircraft.Assists.Summary : "Not recorded";

        private void RecordResult(ChallengeResult result)
        {
            if (Progression == null) Progression = new ProgressionData();
            if (!Progression.Apply(result)) return;
            LastResult = result;
            if (result.Mode == "Delivery Shift")
            {
                Earnings += result.Payout;
                DeliveriesThisShift++;
                shiftScoreTotal += result.Score;
            }
            if (store != null && !store.Save(Progression))
            {
                SaveWarning = "Progress is held for this session but could not be saved: " + store.LastError;
                Debug.LogWarning(SaveWarning);
            }
            else SaveWarning = "";
            FeedbackEvent?.Invoke(result.Mode == "Training" ? "Drill complete · grade " + result.Grade : $"Delivered · +${result.Payout} · grade {result.Grade}");
            ResultRecorded?.Invoke(result);
        }

        private void OfferNextJob()
        {
            if (contracts.Count == 0) BuildContracts();
            for (int tried = 0; tried < contracts.Count; tried++)
            {
                ContractDefinition contract = contracts[nextContract++ % contracts.Count];
                if (CompletedDeliveries < contract.RequiredDeliveries) continue;
                CurrentMission = new MissionSession(contract);
                return;
            }
            CurrentMission = null;
        }

        private void BuildContracts()
        {
            contracts.Clear();
            if (Zones == null || Zones.Length < 3) return;
            AddContract("town-commute", "Town connection", ContractType.Passengers, 0, 1, 160f, 190, 0);
            AddContract("dock-parcel", "Ferry provisions", ContractType.Cargo, 1, 2, 230f, 250, 0);
            AddContract("yard-spares", "Workshop spares", ContractType.Cargo, 2, 3, 300f, 330, 2);
            AddContract("clinic-transfer", "Clinic staff transfer", ContractType.Passengers, 1, 4, 160f, 320, 2);
            AddContract("orchard-crates", "Orchard produce", ContractType.Cargo, 5, 3, 330f, 390, 2);
            AddContract("ridge-crew", "Ridge survey crew", ContractType.Passengers, 3, 6, 240f, 430, 4);
            AddContract("summit-stores", "Summit supplies", ContractType.Cargo, 3, 7, 350f, 490, 4);
            AddContract("light-keeper", "Lighthouse relief", ContractType.Passengers, 2, 8, 160f, 440, 4);
            AddContract("cove-samples", "East cove samples", ContractType.Cargo, 9, 4, 180f, 440, 4);
        }

        private void AddContract(string id, string title, ContractType type, int pickup, int destination, float kg, int pay, int required)
        {
            if (pickup >= Zones.Length || destination >= Zones.Length || Zones[pickup] == null || Zones[destination] == null) return;
            float distance = Vector3.Distance(Zones[pickup].transform.position, Zones[destination].transform.position);
            float expected = 150f + distance / 10f;
            contracts.Add(new ContractDefinition
            {
                Id = id, Title = title, Type = type, Pickup = Zones[pickup].Definition, Destination = Zones[destination].Definition,
                PayloadKg = kg, BasePay = pay, RequiredDeliveries = required, ExpectedSeconds = expected,
                DeadlineSeconds = expected * (required == 0 ? 3f : 2f), DwellSeconds = required >= 4 ? 4f : 3f,
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
