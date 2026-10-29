using MelonLoader.Utils;
using System.Text.Json;
using UnityEngine;

namespace VoiceChat
{
    internal sealed class VoiceOptions
    {
        private static readonly string FilePath = Path.Combine(MelonEnvironment.UserDataDirectory, "NullFrame", "voice.json");
        public bool Enabled { get; set; } = true;
        public string Device { get; set; } = "";
        public float Gain { get; set; } = 1f;
        public float Volume { get; set; } = 1f;
        public KeyCode Key { get; set; } = KeyCode.V;

        internal static VoiceOptions Load()
        {
            try
            {
                VoiceOptions options = File.Exists(FilePath)
                    ? JsonSerializer.Deserialize<VoiceOptions>(File.ReadAllText(FilePath)) : new();
                options ??= new();
                options.Gain = float.IsFinite(options.Gain) ? Math.Clamp(options.Gain, 0f, 4f) : 1f;
                options.Volume = float.IsFinite(options.Volume) ? Math.Clamp(options.Volume, 0f, 1f) : 1f;

                if (!Enum.IsDefined(typeof(KeyCode), options.Key) || options.Key is KeyCode.None or KeyCode.Escape)
                {
                    options.Key = KeyCode.V;
                }

                return options;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
            {
                return new();
            }
        }

        internal void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
