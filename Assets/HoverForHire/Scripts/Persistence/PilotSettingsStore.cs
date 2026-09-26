using System;
using UnityEngine;

namespace HoverForHire
{
    /// <summary>Player choices outside the input system: assists, realism, units, graphics, volume and first-launch state.</summary>
    [Serializable]
    public sealed class PilotSettings
    {
        public int Version = PilotSettingsStore.CurrentVersion;
        public AssistSettings Assists = new AssistSettings();
        public RealismSettings Realism = new RealismSettings();
        public UnitSystem Units = UnitSystem.Metric;
        public GraphicsChoices Graphics = new GraphicsChoices();
        [Range(0f, 1f)] public float Volume = 0.65f;
        public bool FirstRunComplete;

        public void Sanitize()
        {
            if (Assists == null) Assists = new AssistSettings();
            if (Realism == null) Realism = new RealismSettings();
            Realism.Sanitize();
            if (Graphics == null) Graphics = new GraphicsChoices();
            Graphics.Sanitize();
            if (!Enum.IsDefined(typeof(UnitSystem), Units)) Units = UnitSystem.Metric;
            Volume = float.IsNaN(Volume) ? 0.65f : Mathf.Clamp01(Volume);
            Version = PilotSettingsStore.CurrentVersion;
        }
    }

    /// <summary>Key-value storage, so the store can be tested without touching the player's real preferences.</summary>
    public interface IPreferenceStorage
    {
        bool HasKey(string key);
        string GetString(string key);
        float GetFloat(string key, float fallback);
        void SetString(string key, string value);
        void DeleteKey(string key);
        void Save();
    }

    public sealed class PlayerPrefsStorage : IPreferenceStorage
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public string GetString(string key) => PlayerPrefs.GetString(key);
        public float GetFloat(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void DeleteKey(string key) => PlayerPrefs.DeleteKey(key);
        public void Save() => PlayerPrefs.Save();
    }

    /// <summary>
    /// One versioned key for <see cref="PilotSettings"/>. On first load it migrates the 0.2 keys (assists, volume)
    /// and the phase-3 realism and first-run keys, then removes them, so older installs keep their choices.
    /// </summary>
    public static class PilotSettingsStore
    {
        public const int CurrentVersion = 1;
        public const string Key = "HoverForHire.Pilot.v1";
        /// <summary>Keys written by earlier versions, migrated once and then removed.</summary>
        public const string LegacyAssistsKey = "hfh.assists", LegacyVolumeKey = "hfh.volume";
        public const string LegacyRealismKey = "HoverForHire.Realism.v1", LegacyFirstRunKey = "HoverForHire.FirstRunComplete.v1";

        public static PilotSettings Load(IPreferenceStorage storage)
        {
            var settings = new PilotSettings();
            if (storage.HasKey(Key))
            {
                try { JsonUtility.FromJsonOverwrite(storage.GetString(Key), settings); }
                catch (ArgumentException) { settings = new PilotSettings(); }
                settings.Sanitize();
                return settings;
            }
            bool migrated = false;
            if (storage.HasKey(LegacyAssistsKey))
            {
                try { JsonUtility.FromJsonOverwrite(storage.GetString(LegacyAssistsKey), settings.Assists); } catch (ArgumentException) { }
                migrated = true;
            }
            if (storage.HasKey(LegacyVolumeKey)) { settings.Volume = storage.GetFloat(LegacyVolumeKey, settings.Volume); migrated = true; }
            if (storage.HasKey(LegacyRealismKey))
            {
                try { JsonUtility.FromJsonOverwrite(storage.GetString(LegacyRealismKey), settings.Realism); } catch (ArgumentException) { }
                migrated = true;
            }
            if (storage.HasKey(LegacyFirstRunKey)) { settings.FirstRunComplete = true; migrated = true; }
            settings.Sanitize();
            if (migrated)
            {
                Save(storage, settings);
                foreach (string legacy in new[] { LegacyAssistsKey, LegacyVolumeKey, LegacyRealismKey, LegacyFirstRunKey }) storage.DeleteKey(legacy);
                storage.Save();
            }
            return settings;
        }

        public static void Save(IPreferenceStorage storage, PilotSettings settings)
        {
            settings.Sanitize();
            storage.SetString(Key, JsonUtility.ToJson(settings));
            storage.Save();
        }
    }
}
