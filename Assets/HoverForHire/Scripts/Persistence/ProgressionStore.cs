using System;
using System.IO;
using UnityEngine;

namespace HoverForHire
{
    /// <summary>Separate from controls/preferences. Interrupted writes retain a recoverable backup.</summary>
    public sealed class ProgressionStore
    {
        public string Path { get; }
        public string LastError { get; private set; } = "";
        public bool RecoveredBackup { get; private set; }

        public ProgressionStore(string path) { Path = path; }

        public ProgressionData Load()
        {
            LastError = "";
            RecoveredBackup = false;
            ProgressionData data = Read(Path);
            if (data != null) return data;
            data = Read(Path + ".bak");
            if (data != null) { RecoveredBackup = true; return data; }
            return new ProgressionData();
        }

        public bool Save(ProgressionData data)
        {
            if (data == null || !data.IsValid) { LastError = "Progression data was invalid."; return false; }
            try
            {
                string directory = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                string temporary = Path + ".tmp";
                // Flush the complete JSON before any replacement of the previous save.
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(data, true));
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(Path))
                {
                    // Do not overwrite the last good backup with a corrupt primary file.
                    if (Read(Path) == null)
                    {
                        File.Delete(Path);
                        File.Move(temporary, Path);
                    }
                    else
                    {
                        try { File.Replace(temporary, Path, Path + ".bak"); }
                        catch (PlatformNotSupportedException) { ReplaceWithBackup(temporary); }
                    }
                }
                else File.Move(temporary, Path);
                LastError = "";
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                LastError = exception.Message;
                return false;
            }
        }

        private void ReplaceWithBackup(string temporary)
        {
            if (File.Exists(Path + ".bak")) File.Delete(Path + ".bak");
            File.Move(Path, Path + ".bak");
            File.Move(temporary, Path);
        }

        private ProgressionData Read(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                if (new FileInfo(path).Length > 8 * 1024 * 1024) { LastError = "Save exceeded the supported size."; return null; }
                string json = File.ReadAllText(path);
                // JsonUtility can create defaults from an empty object; that is not a valid save.
                if (!json.Contains("\"Version\"") || !json.Contains("\"AppliedAttemptIds\"") || !json.Contains("\"Results\""))
                {
                    LastError = "Save was incomplete or used an unsupported version.";
                    return null;
                }
                ProgressionData data = JsonUtility.FromJson<ProgressionData>(json);
                if (data != null && data.IsValid) return data;
                LastError = "Save was incomplete or used an unsupported version.";
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                LastError = exception.Message;
            }
            return null;
        }
    }
}
