using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using NullServers.Voice;
using UnityEngine;

namespace VoiceChat.Audio
{
    internal sealed class VoicePlayback : IDisposable
    {
        // Every talking player is a 3D sound at their position: full volume within 2 m, silent beyond 40 m.
        private sealed class Speaker : IDisposable
        {
            internal readonly VoiceBuffer Buffer = new();
            internal GameObject Root;
            internal AudioSource Source;
            internal AudioClip Clip;
            internal AudioClip.PCMReaderCallback Callback;
            internal float LastFrame;

            internal Speaker(int id)
            {
                Root = new GameObject("NullFrameVoice-" + id);
                UnityEngine.Object.DontDestroyOnLoad(Root);
                Source = Root.AddComponent<AudioSource>();
                Source.playOnAwake = false;
                Source.loop = true;
                Source.spatialBlend = 1f;
                Source.rolloffMode = AudioRolloffMode.Linear;
                Source.minDistance = 2f;
                Source.maxDistance = 40f;
                Source.dopplerLevel = 0f;
                Source.priority = 64;
                Callback = DelegateSupport.ConvertDelegate<AudioClip.PCMReaderCallback>(
                    new Action<Il2CppStructArray<float>>(Read));
                Clip = UnityAudio.Create(Callback);
                Source.clip = Clip;
            }

            private void Read(Il2CppStructArray<float> data) => Buffer.Read(data.Length, (index, value) => data[index] = value);

            public void Dispose()
            {
                if (Source)
                {
                    Source.Stop();
                }

                if (Root)
                {
                    UnityEngine.Object.Destroy(Root);
                }

                if (Clip)
                {
                    UnityEngine.Object.Destroy(Clip);
                }
            }
        }

        private readonly Dictionary<int, Speaker> _speakers = new();
        private readonly VoiceOptions _options;
        private string _room;

        internal VoicePlayback(VoiceOptions options)
        {
            _options = options;
            VoiceApi.Received += Receive;
        }

        // Frames from players without a known position are ignored; at most 32 players are heard at once.
        private void Receive(int id, uint sequence, byte[] frame)
        {
            if (!_options.Enabled || frame == null || !VoiceApi.TryPosition(id, out var position)) return;

            float[] samples = ImaCodec.Decode(frame);
            if (samples == null) return;

            if (!_speakers.TryGetValue(id, out var speaker))
            {
                if (_speakers.Count >= 32) return;

                speaker = new Speaker(id);
                _speakers[id] = speaker;
            }

            speaker.LastFrame = Time.unscaledTime;
            speaker.Root.transform.position = position;
            speaker.Source.volume = _options.Volume;
            speaker.Buffer.Add(sequence, samples);

            if (!speaker.Source.isPlaying)
            {
                speaker.Source.Play();
            }
        }

        // A speaker is removed after a second of silence, when the player leaves, or when the room changes.
        internal void Update()
        {
            bool reset = _room != VoiceApi.RoomId || !VoiceApi.Active || !_options.Enabled;
            _room = VoiceApi.RoomId;

            foreach (var pair in _speakers.ToArray())
            {
                Speaker speaker = pair.Value;
                if (reset || Time.unscaledTime - speaker.LastFrame > 1f || !VoiceApi.TryPosition(pair.Key, out var position))
                {
                    speaker.Dispose();
                    _speakers.Remove(pair.Key);
                    continue;
                }

                speaker.Root.transform.position = position;
                speaker.Source.volume = _options.Volume;
            }
        }

        public void Dispose()
        {
            VoiceApi.Received -= Receive;

            foreach (Speaker speaker in _speakers.Values)
            {
                speaker.Dispose();
            }

            _speakers.Clear();
        }
    }
}
