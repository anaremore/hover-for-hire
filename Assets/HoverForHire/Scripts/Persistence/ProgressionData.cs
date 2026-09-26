using System;
using System.Collections.Generic;

namespace HoverForHire
{
    /// <summary>A route's best delivery, for the logbook.</summary>
    [Serializable]
    public sealed class RouteRecord
    {
        public string ContractId;
        public string Title;
        public string BestGrade;
        public float BestSeconds;
        public float BestScore;
        public int Completions;
    }

    /// <summary>A training drill's best completion, with the assists and realism it was flown with.</summary>
    [Serializable]
    public sealed class DrillRecord
    {
        public int Drill;
        public string BestGrade;
        public string Assists;
        public string Realism;
        public float BestScore;
        public int Completions;
    }

    [Serializable]
    public sealed class ProgressionData
    {
        /// <summary>Version 2 added the logbook (flight time, landings, personal bests) and liveries.</summary>
        public const int CurrentVersion = 2;
        public const int MaximumResults = 100;

        public int Version = CurrentVersion;
        public int CompletedDeliveries;
        public int TotalEarnings;
        public int CompletedTrainingMask;
        public List<string> AppliedAttemptIds = new List<string>();
        public List<ChallengeResult> Results = new List<ChallengeResult>();
        public float FlightSeconds;
        public int Landings;
        public List<RouteRecord> RouteBests = new List<RouteRecord>();
        public List<DrillRecord> DrillBests = new List<DrillRecord>();
        public int SpentOnLiveries;
        public int OwnedLiveryMask = 1;
        public int SelectedLivery;

        /// <summary>Earnings not yet spent on liveries.</summary>
        public int Balance => Math.Max(0, TotalEarnings - SpentOnLiveries);

        public bool Apply(ChallengeResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.AttemptId) || result.Payout < 0
                || AppliedAttemptIds.Contains(result.AttemptId)) return false;
            AppliedAttemptIds.Add(result.AttemptId);
            TotalEarnings += result.Payout;
            if (result.Mode == "Delivery Shift") CompletedDeliveries++;
            if (TrainingDrill(result, out int drill)) CompletedTrainingMask |= 1 << drill;
            RecordBest(result);
            Results.Add(result);
            if (Results.Count > MaximumResults) Results.RemoveAt(0);
            return true;
        }

        /// <summary>Bring an older save up to date. Version 1 bests are rebuilt from its retained results.</summary>
        public bool Migrate()
        {
            if (Version != 1) return false;
            if (RouteBests == null) RouteBests = new List<RouteRecord>();
            if (DrillBests == null) DrillBests = new List<DrillRecord>();
            if (Results != null) foreach (ChallengeResult result in Results) RecordBest(result);
            OwnedLiveryMask |= 1;
            Version = CurrentVersion;
            return true;
        }

        public bool IsValid => Version == CurrentVersion && CompletedDeliveries >= 0 && TotalEarnings >= 0
            && AppliedAttemptIds != null && Results != null && RouteBests != null && DrillBests != null
            && FlightSeconds >= 0f && !float.IsNaN(FlightSeconds) && Landings >= 0
            && SpentOnLiveries >= 0 && SpentOnLiveries <= TotalEarnings && (OwnedLiveryMask & 1) != 0 && OwnsLivery(SelectedLivery);

        // ---- Liveries: bought with earnings; paint only, never performance ----

        public bool OwnsLivery(int index) => index >= 0 && index < 31 && (OwnedLiveryMask & (1 << index)) != 0;

        public bool BuyLivery(int index, int price)
        {
            if (index < 0 || index >= 31 || OwnsLivery(index) || price < 0 || Balance < price) return false;
            SpentOnLiveries += price;
            OwnedLiveryMask |= 1 << index;
            return true;
        }

        public bool SelectLivery(int index)
        {
            if (!OwnsLivery(index)) return false;
            SelectedLivery = index;
            return true;
        }

        // ---- Logbook ----

        public RouteRecord RouteBest(string contractId) => RouteBests.Find(record => record.ContractId == contractId);
        public DrillRecord DrillBest(int drill) => DrillBests.Find(record => record.Drill == drill);

        private void RecordBest(ChallengeResult result)
        {
            if (result == null) return;
            if (result.Mode == "Delivery Shift" && !string.IsNullOrEmpty(result.ContractId))
            {
                RouteRecord route = RouteBest(result.ContractId);
                if (route == null)
                {
                    route = new RouteRecord { ContractId = result.ContractId, Title = result.Title, BestSeconds = float.MaxValue, BestScore = -1f };
                    RouteBests.Add(route);
                }
                route.Completions++;
                if (result.Seconds > 0f && result.Seconds < route.BestSeconds) route.BestSeconds = result.Seconds;
                if (result.Score > route.BestScore) { route.BestScore = result.Score; route.BestGrade = result.Grade; }
            }
            else if (TrainingDrill(result, out int drill))
            {
                DrillRecord record = DrillBest(drill);
                if (record == null)
                {
                    record = new DrillRecord { Drill = drill, BestScore = -1f };
                    DrillBests.Add(record);
                }
                record.Completions++;
                if (result.Score > record.BestScore)
                {
                    record.BestScore = result.Score;
                    record.BestGrade = result.Grade;
                    record.Assists = result.Assists;
                    record.Realism = result.Realism;
                }
            }
        }

        private static bool TrainingDrill(ChallengeResult result, out int drill)
        {
            drill = -1;
            return result.Mode == "Training" && result.ContractId != null && result.ContractId.StartsWith("training-", StringComparison.Ordinal)
                && int.TryParse(result.ContractId.Substring(9), out drill) && drill >= 0 && drill < TrainingSession.Names.Length;
        }
    }
}
