using System;
using System.Collections.Generic;

namespace HoverForHire
{
    [Serializable]
    public sealed class ProgressionData
    {
        public int Version = 1;
        public int CompletedDeliveries;
        public int TotalEarnings;
        public int CompletedTrainingMask;
        public List<string> AppliedAttemptIds = new List<string>();
        public List<ChallengeResult> Results = new List<ChallengeResult>();

        public bool Apply(ChallengeResult result)
        {
            if (result == null || string.IsNullOrWhiteSpace(result.AttemptId) || result.Payout < 0
                || AppliedAttemptIds.Contains(result.AttemptId)) return false;
            AppliedAttemptIds.Add(result.AttemptId);
            TotalEarnings += result.Payout;
            if (result.Mode == "Delivery Shift") CompletedDeliveries++;
            if (result.Mode == "Training" && result.ContractId != null && result.ContractId.StartsWith("training-", StringComparison.Ordinal)
                && int.TryParse(result.ContractId.Substring(9), out int drill) && drill >= 0 && drill < 7)
                CompletedTrainingMask |= 1 << drill;
            Results.Add(result);
            if (Results.Count > 100) Results.RemoveAt(0);
            return true;
        }

        public bool IsValid => Version == 1 && CompletedDeliveries >= 0 && TotalEarnings >= 0
            && AppliedAttemptIds != null && Results != null;
    }
}
