using System;
using System.Collections.Generic;

namespace HoverForHire
{
    /// <summary>
    /// Skill endorsements earned by completing training drills. Contracts to demanding pads require them, so the
    /// same helicopter reaches harder work by the pilot getting better, never by the aircraft getting stronger.
    /// </summary>
    [Flags]
    public enum Certification
    {
        None = 0,
        /// <summary>Rooftop clinic: precision landing.</summary>
        Rooftop = 1,
        /// <summary>Ridge and summit pads: confined-area landing and heavy lift.</summary>
        Mountain = 2,
        /// <summary>Lighthouse and east cove: crosswind landing.</summary>
        Coastal = 4,
        /// <summary>Emergency medical work: settling-with-power recovery and autorotation.</summary>
        Emergency = 8
    }

    public static class Certifications
    {
        public static readonly Certification[] All = { Certification.Rooftop, Certification.Mountain, Certification.Coastal, Certification.Emergency };

        /// <summary>Training drills (indices into <see cref="TrainingSession.Names"/>) that earn a certification.</summary>
        public static int[] RequiredDrills(Certification certification)
        {
            switch (certification)
            {
                case Certification.Rooftop: return new[] { 6 };
                case Certification.Mountain: return new[] { TrainingSession.ConfinedArea, TrainingSession.HeavyLift };
                case Certification.Coastal: return new[] { TrainingSession.Crosswind };
                case Certification.Emergency: return new[] { TrainingSession.SettlingWithPower, TrainingSession.Autorotation };
                default: return Array.Empty<int>();
            }
        }

        public static string Name(Certification certification)
        {
            switch (certification)
            {
                case Certification.Rooftop: return "Rooftop";
                case Certification.Mountain: return "Mountain";
                case Certification.Coastal: return "Coastal";
                case Certification.Emergency: return "Emergency";
                default: return "None";
            }
        }

        /// <summary>Every certification this set of completed drills has earned.</summary>
        public static Certification Earned(int completedTrainingMask)
        {
            Certification earned = Certification.None;
            foreach (Certification certification in All)
            {
                bool complete = true;
                foreach (int drill in RequiredDrills(certification)) complete &= (completedTrainingMask & (1 << drill)) != 0;
                if (complete) earned |= certification;
            }
            return earned;
        }

        /// <summary>True when every certification in <paramref name="required"/> has been earned.</summary>
        public static bool Satisfies(int completedTrainingMask, Certification required) => (Earned(completedTrainingMask) & required) == required;

        /// <summary>Readable requirement, e.g. "Mountain: Confined area and Heavy lift drills".</summary>
        public static string Requirement(Certification certification)
        {
            var drills = new List<string>();
            foreach (int drill in RequiredDrills(certification)) drills.Add(TrainingSession.Names[drill]);
            return $"{Name(certification)}: {string.Join(" and ", drills)} {(drills.Count > 1 ? "drills" : "drill")}";
        }

        /// <summary>Names of the certifications in a set, e.g. "Mountain + Rooftop".</summary>
        public static string Describe(Certification set)
        {
            var names = new List<string>();
            foreach (Certification certification in All) if ((set & certification) != 0) names.Add(Name(certification));
            return names.Count == 0 ? "None" : string.Join(" + ", names);
        }
    }
}
