using System;
using UnityEngine;

namespace HoverForHire
{
    public enum AssistPreset { Beginner, Standard, Unassisted }

    [Serializable]
    public sealed class AssistSettings
    {
        public bool RateStabilization = true;
        public bool AutoLevel = true;
        public bool YawStabilization = true;
        public bool TorqueCompensation = true;

        public void SetPreset(AssistPreset preset)
        {
            RateStabilization = preset != AssistPreset.Unassisted;
            AutoLevel = preset == AssistPreset.Beginner;
            YawStabilization = preset != AssistPreset.Unassisted;
            TorqueCompensation = preset != AssistPreset.Unassisted;
        }

        public string Summary
        {
            get
            {
                string result = "";
                if (RateStabilization) result += "RATE ";
                if (AutoLevel) result += "LEVEL ";
                if (YawStabilization) result += "YAW ";
                if (TorqueCompensation) result += "TORQUE ";
                return result.Length == 0 ? "UNASSISTED" : result.TrimEnd();
            }
        }

        internal Vector4 Weights => new Vector4(RateStabilization ? 1f : 0f,
            AutoLevel ? 1f : 0f, YawStabilization ? 1f : 0f, TorqueCompensation ? 1f : 0f);
    }
}
