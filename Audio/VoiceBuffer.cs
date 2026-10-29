using System.Diagnostics;

namespace VoiceChat.Audio
{
    // The jitter buffer of one speaker. Frames arrive over the network in bursts, and Unity's audio thread reads the
    // stream in large chunks ahead of time, so playback starts only once the buffer covers a whole read plus some jitter.
    // A frame that has not arrived yet is waited for; it is skipped only when later frames are already here (it was lost).
    internal sealed class VoiceBuffer
    {
        private const int JitterFrames = 3;
        private const int MaxTargetFrames = 25;
        private const int ExtraFrames = 10;

        private readonly object _sync = new();
        private readonly Dictionary<uint, float[]> _frames = new();
        private float[] _current;
        private int _offset = ImaCodec.Samples;
        private uint _next;
        private bool _started;
        private bool _seeded;
        private float _last;
        private int _target = JitterFrames + 1;
        private long _waitingSince;

        internal void Add(uint sequence, float[] samples)
        {
            if (samples?.Length != ImaCodec.Samples) return;

            lock (_sync)
            {
                int ahead = unchecked((int)(sequence - _next));

                // A new speaker or a restarted microphone starts a new sequence.
                if (!_seeded || ahead is < -100 or > 100)
                {
                    _frames.Clear();
                    _current = null;
                    _offset = ImaCodec.Samples;
                    _started = false;
                    _seeded = true;
                    _next = sequence;
                    ahead = 0;
                }

                // Already played, already skipped, or a duplicate.
                if (ahead < 0 || _frames.ContainsKey(sequence)) return;

                if (_frames.Count == 0 && !_started)
                {
                    _waitingSince = Stopwatch.GetTimestamp();
                }

                _frames[sequence] = samples;
                TrimDelay(sequence);
            }
        }

        // When much more audio piles up than needed, the oldest frames are skipped so the delay stays short.
        private void TrimDelay(uint newest)
        {
            if (unchecked((int)(newest - _next)) < _target + ExtraFrames) return;

            uint first = unchecked(newest - (uint)(_target - 1));
            foreach (uint key in _frames.Keys.ToArray())
            {
                if (unchecked((int)(key - first)) < 0)
                {
                    _frames.Remove(key);
                }
            }

            _next = first;
        }

        // Called on the audio thread.
        internal void Read(int length, Action<int, float> write)
        {
            lock (_sync)
            {
                // The largest read decides how much has to be buffered before playback starts.
                int frames = (length + ImaCodec.Samples - 1) / ImaCodec.Samples;
                _target = Math.Clamp(Math.Max(_target, frames + JitterFrames), JitterFrames + 1, MaxTargetFrames);

                for (int i = 0; i < length; i++)
                {
                    if (_offset >= ImaCodec.Samples)
                    {
                        NextFrame();
                    }

                    // Gaps fade out instead of clicking.
                    _last = _current != null ? _current[_offset] : _last * 0.98f;
                    write(i, _last);
                    _offset++;
                }
            }
        }

        private void NextFrame()
        {
            _offset = 0;
            _current = null;

            if (!_started)
            {
                if (!ReadyToStart()) return;

                _started = true;
            }

            if (_frames.Remove(_next, out _current))
            {
                _next++;
                return;
            }

            // The frame is missing. With later frames already here it was lost and is skipped;
            // otherwise the buffer ran dry and fills up again before playing on.
            if (_frames.Count > 0)
            {
                _next++;
            }
            else
            {
                _started = false;
            }
        }

        // Playback starts with a full buffer, or earlier when the first frame has waited as long as a full buffer
        // would take to arrive, so a short phrase is not held back.
        private bool ReadyToStart()
        {
            if (_frames.Count >= _target) return true;
            if (_frames.Count == 0) return false;

            double waited = (Stopwatch.GetTimestamp() - _waitingSince) / (double)Stopwatch.Frequency;
            return waited >= _target * 0.02 + 0.04;
        }
    }
}
