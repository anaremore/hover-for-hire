using UnityEngine;

namespace HoverForHire
{
    /// <summary>Air mass motion sampled by the flight model. Absent means calm air.</summary>
    public interface IWindSource
    {
        /// <summary>Wind velocity (m/s, world axes) at a world position for the current physics step.</summary>
        Vector3 WindAt(Vector3 worldPosition);

        /// <summary>Fast buffeting in −1..1 per axis, already scaled by gust strength. Zero in smooth air.</summary>
        Vector3 TurbulenceAt(Vector3 worldPosition);
    }
}
