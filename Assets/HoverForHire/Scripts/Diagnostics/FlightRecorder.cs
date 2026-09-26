using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace HoverForHire
{
    /// <summary>
    /// Opt-in flight data recorder. Writes one CSV row per physics step to persistentDataPath/flights/.
    /// Start with the RecordFlight binding (F3) or the -recordFlight command-line switch.
    /// Recording observes the aircraft only; it never changes controls or physics.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public sealed class FlightRecorder : MonoBehaviour
    {
        public HelicopterController Aircraft;
        public MissionDirector Missions;
        [Tooltip("Rows buffered before each disk write.")]
        [Min(1)] public int FlushEveryRows = 250;
        /// <summary>Optional directory override (tests). Empty uses persistentDataPath/flights.</summary>
        public string DirectoryOverride;

        public bool IsRecording => writer != null;
        public string CurrentPath { get; private set; } = "";
        public int RowsWritten { get; private set; }
        public float RecordingStartTime { get; private set; }

        private StreamWriter writer;
        private readonly StringBuilder buffer = new StringBuilder(64 * 1024);
        private int bufferedRows;

        public static bool RequestedOnCommandLine()
            => Array.IndexOf(Environment.GetCommandLineArgs(), "-recordFlight") >= 0;

        public void Toggle()
        {
            if (IsRecording) StopRecording(); else StartRecording();
        }

        public bool StartRecording()
        {
            if (IsRecording) return true;
            try
            {
                string directory = string.IsNullOrWhiteSpace(DirectoryOverride)
                    ? Path.Combine(Application.persistentDataPath, "flights") : DirectoryOverride;
                Directory.CreateDirectory(directory);
                CurrentPath = Path.Combine(directory, "flight-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".csv");
                writer = new StreamWriter(CurrentPath, false, new UTF8Encoding(false));
                writer.Write(FlightRecordFormat.Header);
                writer.Write('\n');
                RowsWritten = 0;
                bufferedRows = 0;
                buffer.Clear();
                RecordingStartTime = Time.time;
                Debug.Log("Flight recording started: " + CurrentPath);
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                Debug.LogWarning("Flight recording could not start: " + exception.Message);
                writer = null;
                return false;
            }
        }

        public void StopRecording()
        {
            if (writer == null) return;
            Flush();
            writer.Dispose();
            writer = null;
            Debug.Log($"Flight recording saved: {CurrentPath} ({RowsWritten} rows)");
        }

        private void FixedUpdate()
        {
            if (writer == null || Aircraft == null || Aircraft.Body == null) return;
            Rigidbody body = Aircraft.Body;
            Quaternion rotation = body.rotation;
            Vector3 forward = rotation * Vector3.forward, right = rotation * Vector3.right, up = rotation * Vector3.up;
            var sample = new FlightRecordSample
            {
                Time = Time.time - RecordingStartTime,
                Mode = Missions != null ? Missions.Mode.ToString() : "",
                MissionState = Missions != null ? Missions.MissionState.ToString() : "",
                Assists = Aircraft.Assists != null ? Aircraft.Assists.Summary : "",
                Grounded = Aircraft.Grounded,
                Crashed = Aircraft.Crashed,
                Position = body.position,
                Velocity = body.linearVelocity,
                AirVelocityLocal = Quaternion.Inverse(rotation) * Aircraft.AirVelocity,
                Wind = Aircraft.CurrentWind,
                PitchDegrees = Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg,
                RollDegrees = Mathf.Atan2(-right.y, up.y) * Mathf.Rad2Deg,
                HeadingDegrees = Aircraft.Heading,
                SideslipDegrees = Aircraft.SideslipDegrees,
                AltitudeAGL = Aircraft.AltitudeAGL,
                AngularRatesDegrees = Aircraft.LocalAngularRatesDegrees,
                Raw = Aircraft.RawCommand,
                Assisted = Aircraft.AssistedCommand,
                LiftNewtons = Aircraft.LiftNewtons,
                MassKg = body.mass,
                RotorRpm = Aircraft.RotorRpm,
                HoverCollective = Aircraft.HoverCollective
            };
            FlightRecordFormat.AppendRow(buffer, sample);
            bufferedRows++;
            RowsWritten++;
            if (bufferedRows >= FlushEveryRows) Flush();
        }

        private void Flush()
        {
            if (writer == null || buffer.Length == 0) return;
            try
            {
                writer.Write(buffer.ToString());
                writer.Flush();
            }
            catch (IOException exception)
            {
                Debug.LogWarning("Flight recording stopped after a write failure: " + exception.Message);
                writer.Dispose();
                writer = null;
            }
            buffer.Clear();
            bufferedRows = 0;
        }

        private void OnApplicationPause(bool paused) { if (paused) Flush(); }
        private void OnDisable() => StopRecording();
    }
}
