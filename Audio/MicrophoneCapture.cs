using Il2CppInterop.Runtime.InteropTypes.Arrays;
using NullServers.Voice;
using UnityEngine;

namespace VoiceChat.Audio
{
    internal sealed class MicrophoneCapture : IDisposable
    {
        private readonly VoiceOptions _options;
        private AudioClip _clip;
        private string _device;
        private int _read;
        private float[] _input;
        private int _deviceId = -1;
        private readonly float[] _samples = new float[ImaCodec.Samples];
        private uint _sequence = (uint)Environment.TickCount;
        private float _started;
        private float _retry;
        private float _lastInput;
        private float _lastOutput;
        private int _hangover;
        internal float Level { get; private set; }
        internal bool Transmitting { get; private set; }
        internal bool Testing { get; set; }
        internal string Error { get; private set; }

        internal MicrophoneCapture(VoiceOptions options) => _options = options;

        // The microphone records only while the talk key is held (or the settings menu tests it).
        // Every 20 ms of input becomes one voice frame.
        internal void Update(bool menu)
        {
            bool talk = VoiceApi.InputAllowed && !menu && Input.GetKey(_options.Key);
            bool capture = Application.isFocused && _options.Enabled && (talk || Testing && menu);
            Transmitting = false;
            Level *= 0.85f;

            if (!capture)
            {
                Stop();
                return;
            }

            if (!_clip)
            {
                if (Time.unscaledTime < _retry) return;

                _retry = Time.unscaledTime + 3f;
                Start();
                return;
            }

            if (_device != SelectedDevice())
            {
                Stop();
                _retry = 0f;
                return;
            }

            int position = UnityAudio.GetPosition(_deviceId);
            if (position <= 0 && Time.unscaledTime - _started > 2f)
            {
                Error = "Microphone did not start. Check the selected device and Windows microphone access.";
                Stop();
                return;
            }

            int frames = Math.Max(1, _clip.frequency / 50);
            int available = (position - _read + _clip.samples) % _clip.samples;

            // After a long pause (a freeze or a loading screen) old audio is skipped instead of being sent late.
            if (available > frames * 6)
            {
                _read = (position - frames + _clip.samples) % _clip.samples;
                available = frames;
            }

            // At most four frames per update, so a slow frame cannot flood the host.
            int sent = 0;
            while (available >= frames && sent++ < 4)
            {
                if (!UnityAudio.GetData(_clip, _input, _read))
                {
                    Error = "Could not read microphone samples.";
                    Stop();
                    return;
                }

                _read = (_read + frames) % _clip.samples;
                available -= frames;

                float rms = ReadFrame(frames);
                Level = Math.Max(Level, Math.Clamp(rms * 5f, 0f, 1f));

                // Silence is not sent, but sending goes on for 300 ms after the last loud frame,
                // so quiet sounds inside a word and the ends of words are not cut off.
                if (rms > 0.002f)
                {
                    _hangover = 15;
                }

                if (talk && _hangover > 0)
                {
                    _hangover--;
                    VoiceApi.Send(_sequence++, ImaCodec.Encode(_samples));
                    Transmitting = true;
                }
            }
        }

        // Mixes the channels to mono, resamples 20 ms of input to the codec's sample count, removes the DC offset
        // with a high-pass filter and applies the gain. Returns the frame's loudness (RMS).
        private float ReadFrame(int frames)
        {
            double energy = 0;

            for (int i = 0; i < _samples.Length; i++)
            {
                float at = i * (frames - 1f) / (_samples.Length - 1f);
                int first = (int)at;
                int second = Math.Min(first + 1, frames - 1);
                float a = 0f;
                float b = 0f;

                for (int channel = 0; channel < _clip.channels; channel++)
                {
                    a += _input[first * _clip.channels + channel];
                    b += _input[second * _clip.channels + channel];
                }

                float sample = (a + (b - a) * (at - first)) / _clip.channels;
                float filtered = sample - _lastInput + 0.98f * _lastOutput;
                _lastInput = sample;
                _lastOutput = filtered;
                _samples[i] = Math.Clamp(filtered * _options.Gain, -1f, 1f);
                energy += _samples[i] * _samples[i];
            }

            return (float)Math.Sqrt(energy / _samples.Length);
        }

        // The chosen microphone, or the first one when it is unplugged.
        private string SelectedDevice()
        {
            string[] devices = Microphone.devices;
            if (devices.Length == 0) return null;

            return devices.Contains(_options.Device) ? _options.Device : devices[0];
        }

        private void Start()
        {
            _device = SelectedDevice();
            if (_device == null)
            {
                Error = "No microphone found. Connect a microphone, then refresh devices.";
                return;
            }

            try
            {
                _deviceId = UnityAudio.DeviceId(_device);
                UnityAudio.Caps(_deviceId, out int minimum, out int maximum);

                // The codec's rate when the device supports it, otherwise the nearest rate it does.
                int rate = maximum > 0 ? Math.Clamp(ImaCodec.Rate, Math.Max(8000, minimum), Math.Max(8000, maximum)) : ImaCodec.Rate;
                _clip = UnityAudio.Start(_deviceId, rate);

                if (!_clip)
                {
                    Error = "Could not open the microphone.";
                    return;
                }

                _input = new float[_clip.frequency / 50 * _clip.channels];
                _read = 0;
                _started = Time.unscaledTime;
                Error = null;
            }
            catch (Exception error)
            {
                Error = "Microphone unavailable: " + error.Message;
                Stop();
            }
        }

        private void Stop()
        {
            if (_clip)
            {
                UnityAudio.End(_deviceId);
                UnityEngine.Object.Destroy(_clip);
                _clip = null;
            }

            _input = null;
            _lastInput = _lastOutput = 0f;
            _hangover = 0;
        }

        public void Dispose() => Stop();
    }
}
