using System.Globalization;
using System.Text;
using UnityEngine;

namespace HoverForHire
{
    /// <summary>One physics-step observation for the flight recorder. Pure data; no Unity object references.</summary>
    public struct FlightRecordSample
    {
        public float Time;
        public string Mode, MissionState, Assists;
        public bool Grounded, Crashed;
        public Vector3 Position, Velocity, AirVelocityLocal, Wind, AngularRatesDegrees;
        public float PitchDegrees, RollDegrees, HeadingDegrees, SideslipDegrees, AltitudeAGL;
        public PilotCommand Raw, Assisted;
        public float LiftNewtons, MassKg, RotorRpm, HoverCollective;
    }

    /// <summary>CSV layout for recorded flights: invariant culture, one header, one row per physics step.</summary>
    public static class FlightRecordFormat
    {
        public const string Header =
            "time_s,mode,mission_state,assists,grounded,crashed," +
            "pos_x_m,pos_y_m,pos_z_m,vel_x_mps,vel_y_mps,vel_z_mps," +
            "air_right_mps,air_up_mps,air_fwd_mps,wind_x_mps,wind_y_mps,wind_z_mps," +
            "pitch_deg,roll_deg,heading_deg,sideslip_deg,agl_m," +
            "rate_pitch_dps,rate_yaw_dps,rate_roll_dps," +
            "raw_cyclic_x,raw_cyclic_y,raw_yaw,raw_collective," +
            "cmd_cyclic_x,cmd_cyclic_y,cmd_yaw,cmd_collective," +
            "lift_n,mass_kg,rotor_rpm,hover_collective";

        public static void AppendRow(StringBuilder builder, in FlightRecordSample s)
        {
            Number(builder, s.Time, "0.000");
            Text(builder, s.Mode); Text(builder, s.MissionState); Text(builder, s.Assists);
            builder.Append(',').Append(s.Grounded ? '1' : '0').Append(',').Append(s.Crashed ? '1' : '0');
            Vector(builder, s.Position, "0.000"); Vector(builder, s.Velocity, "0.000");
            Vector(builder, s.AirVelocityLocal, "0.000"); Vector(builder, s.Wind, "0.000");
            Next(builder, s.PitchDegrees, "0.00"); Next(builder, s.RollDegrees, "0.00");
            Next(builder, s.HeadingDegrees, "0.00"); Next(builder, s.SideslipDegrees, "0.00");
            Next(builder, s.AltitudeAGL, "0.000");
            Vector(builder, s.AngularRatesDegrees, "0.00");
            Next(builder, s.Raw.Cyclic.x, "0.0000"); Next(builder, s.Raw.Cyclic.y, "0.0000");
            Next(builder, s.Raw.Yaw, "0.0000"); Next(builder, s.Raw.Collective, "0.0000");
            Next(builder, s.Assisted.Cyclic.x, "0.0000"); Next(builder, s.Assisted.Cyclic.y, "0.0000");
            Next(builder, s.Assisted.Yaw, "0.0000"); Next(builder, s.Assisted.Collective, "0.0000");
            Next(builder, s.LiftNewtons, "0.0"); Next(builder, s.MassKg, "0.0");
            Next(builder, s.RotorRpm, "0.0"); Next(builder, s.HoverCollective, "0.0000");
            builder.Append('\n');
        }

        private static void Number(StringBuilder b, float value, string format)
            => b.Append(float.IsNaN(value) || float.IsInfinity(value) ? "0" : value.ToString(format, CultureInfo.InvariantCulture));

        private static void Next(StringBuilder b, float value, string format) { b.Append(','); Number(b, value, format); }

        private static void Vector(StringBuilder b, Vector3 value, string format)
        {
            Next(b, value.x, format); Next(b, value.y, format); Next(b, value.z, format);
        }

        private static void Text(StringBuilder b, string value)
        {
            b.Append(',');
            if (string.IsNullOrEmpty(value)) return;
            // Quote so commas or arrows in assist summaries never shift columns.
            b.Append('"').Append(value.Replace("\"", "'")).Append('"');
        }
    }
}
