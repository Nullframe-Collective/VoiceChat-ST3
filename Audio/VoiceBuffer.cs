namespace VoiceChat.Audio
{
    internal sealed class VoiceBuffer
    {
        private readonly object _sync = new();
        private readonly Dictionary<uint, float[]> _frames = new();
        private float[] _current;
        private int _offset = ImaCodec.Samples;
        private uint _next;
        private bool _started;
        private bool _seeded;
        private float _last;
        private int _lost;

        internal void Add(uint sequence, float[] samples)
        {
            if (samples?.Length != ImaCodec.Samples) return;

            lock (_sync)
            {
                if (!_seeded || unchecked((int)(sequence - _next)) > 50)
                {
                    _frames.Clear();
                    _current = null;
                    _offset = ImaCodec.Samples;
                    _seeded = true;
                    _started = false;
                    _next = sequence;
                }

                int ahead = unchecked((int)(sequence - _next));
                if (ahead is < 0 or > 12 || _frames.ContainsKey(sequence)) return;

                _frames[sequence] = samples;
            }
        }

        internal void Read(int length, Action<int, float> write)
        {
            lock (_sync)
            {
                for (int i = 0; i < length; i++)
                {
                    if (!_started && _frames.Count >= 3)
                    {
                        _started = true;
                    }

                    if (_offset >= ImaCodec.Samples && _started)
                    {
                        _frames.Remove(_next++, out _current);
                        _offset = 0;
                        _lost = _current == null ? _lost + 1 : 0;

                        if (_lost >= 5)
                        {
                            _started = false;
                            _seeded = false;
                            _frames.Clear();
                        }
                    }

                    _last = _current != null && _offset < ImaCodec.Samples ? _current[_offset] : _last * 0.98f;
                    write(i, _last);
                    _offset++;

                    if (_offset >= ImaCodec.Samples)
                    {
                        _current = null;
                    }
                }
            }
        }
    }
}
