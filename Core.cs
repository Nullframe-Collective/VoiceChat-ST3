using MelonLoader;
using NullServers.Voice;
using VoiceChat.Audio;
using VoiceChat.UI;
using UnityEngine;

[assembly: MelonInfo(typeof(VoiceChat.Core), "VoiceChat", "0.1.0", "Nullframe Collective")]
[assembly: MelonGame("ZeoWorks", "Slendytubbies 3")]
[assembly: MelonColor(255, 52, 152, 219)]
[assembly: MelonAdditionalDependencies("NullServers")]
[assembly: MelonPriority(200)]

namespace VoiceChat
{
    public sealed class Core : MelonMod
    {
        private VoiceOptions _options;
        private MicrophoneCapture _microphone;
        private VoicePlayback _playback;
        private SettingsMenu _settings;
        private string _room;

        public override void OnInitializeMelon()
        {
            _options = VoiceOptions.Load();
            _microphone = new MicrophoneCapture(_options);
            _playback = new VoicePlayback(_options);
            _settings = new SettingsMenu(_options, _microphone, LoggerInstance);
            VoiceApi.Register(_settings.Show);
            VoiceApi.Enabled = _options.Enabled;
            LoggerInstance.Msg("Initialized. Hold " + _options.Key + " to talk on Nullframe Servers.");
        }

        public override void OnUpdate()
        {
            if (_room != VoiceApi.RoomId)
            {
                _microphone.Dispose();
                _settings.Close();
                _room = VoiceApi.RoomId;
            }

            VoiceApi.Enabled = _options.Enabled;
            _settings.Update();
            _microphone.Update(_settings.Visible);
            _playback.Update();
        }

        public override void OnGUI()
        {
            if (!VoiceApi.Active || Event.current.type != EventType.Repaint) return;

            Color previous = GUI.color;
            GUI.color = Color.Lerp(new Color(0.5f, 0.5f, 0.5f), new Color(0.2f, 1f, 0.35f), _microphone.Level);
            float size = Math.Clamp(Screen.height / 24f, 28f, 48f);
            GUI.DrawTexture(new Rect(Screen.width - size - 24f, Screen.height - size - 24f, size, size), VoiceIcons.Microphone);
            GUI.color = previous;
        }

        public override void OnApplicationQuit()
        {
            _microphone?.Dispose();
            _playback?.Dispose();
            VoiceApi.Unregister();
            _settings?.Close();
        }
    }
}
