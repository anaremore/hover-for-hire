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
        [Tooltip("Stick sets bank and pitch (up to the commanded-attitude limit) instead of a rotation rate. Uses the rate loop.")]
        public bool AttitudeCommand;

        public void SetPreset(AssistPreset preset)
        {
            RateStabilization = preset != AssistPreset.Unassisted;
            AutoLevel = preset == AssistPreset.Beginner;
            YawStabilization = preset != AssistPreset.Unassisted;
            TorqueCompensation = preset != AssistPreset.Unassisted;
        }

        /// <summary>The preset these flags match exactly, or null for a custom combination.</summary>
        public AssistPreset? MatchingPreset
        {
            get
            {
                foreach (AssistPreset preset in new[] { AssistPreset.Beginner, AssistPreset.Standard, AssistPreset.Unassisted })
                {
                    var reference = new AssistSettings();
                    reference.SetPreset(preset);
                    if (reference.Weights == Weights) return preset;
                }
                return null;
            }
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
                if (AttitudeCommand) result += "ATTITUDE ";
                return result.Length == 0 ? "UNASSISTED" : result.TrimEnd();
            }
        }

        internal Vector4 Weights => new Vector4(RateStabilization ? 1f : 0f,
            AutoLevel ? 1f : 0f, YawStabilization ? 1f : 0f, TorqueCompensation ? 1f : 0f);
    }
}
