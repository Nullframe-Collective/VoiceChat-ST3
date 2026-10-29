using System.Buffers.Binary;

namespace VoiceChat.Audio
{
    internal static class ImaCodec
    {
        internal const int Rate = 24000;
        internal const int Samples = 480;
        internal const int Bytes = 244;
        private static readonly int[] Indices = [-1, -1, -1, -1, 2, 4, 6, 8];
        private static readonly int[] Steps =
        [
            7, 8, 9, 10, 11, 12, 13, 14, 16, 17, 19, 21, 23, 25, 28, 31, 34, 37, 41, 45, 50, 55, 60, 66, 73, 80,
            88, 97, 107, 118, 130, 143, 157, 173, 190, 209, 230, 253, 279, 307, 337, 371, 408, 449, 494, 544, 598,
            658, 724, 796, 876, 963, 1060, 1166, 1282, 1411, 1552, 1707, 1878, 2066, 2272, 2499, 2749, 3024, 3327,
            3660, 4026, 4428, 4871, 5358, 5894, 6484, 7132, 7845, 8630, 9493, 10442, 11487, 12635, 13899, 15289,
            16818, 18500, 20350, 22385, 24623, 27086, 29794, 32767
        ];

        internal static byte[] Encode(float[] samples)
        {
            if (samples.Length != Samples) throw new ArgumentException("A voice frame must contain 480 samples.");

            byte[] data = new byte[Bytes];
            int predictor = Pcm(samples[0]);
            int delta = Math.Abs(Pcm(samples[1]) - predictor);
            int index = 0;

            while (index < 88 && Steps[index] < delta)
            {
                index++;
            }

            BinaryPrimitives.WriteInt16LittleEndian(data, (short)predictor);
            data[2] = (byte)index;

            for (int i = 1; i < Samples; i++)
            {
                int difference = Pcm(samples[i]) - predictor;
                int code = difference < 0 ? 8 : 0;
                difference = Math.Abs(difference);
                int step = Steps[index];

                for (int bit = 4; bit >= 1; bit >>= 1)
                {
                    if (difference >= step)
                    {
                        code |= bit;
                        difference -= step;
                    }

                    step >>= 1;
                }

                predictor = DecodeNibble(code, predictor, ref index);
                int offset = i - 1;
                data[4 + offset / 2] |= (byte)(code << ((offset & 1) * 4));
            }

            return data;
        }

        internal static float[] Decode(byte[] data)
        {
            if (data?.Length != Bytes || data[2] > 88 || data[3] != 0) return null;

            float[] samples = new float[Samples];
            int predictor = BinaryPrimitives.ReadInt16LittleEndian(data);
            int index = data[2];
            samples[0] = predictor / 32768f;

            for (int i = 1; i < Samples; i++)
            {
                int offset = i - 1;
                int code = data[4 + offset / 2] >> ((offset & 1) * 4) & 15;
                predictor = DecodeNibble(code, predictor, ref index);
                samples[i] = predictor / 32768f;
            }

            return samples;
        }

        private static int Pcm(float sample) => float.IsFinite(sample) ? (int)(Math.Clamp(sample, -1f, 1f) * 32767f) : 0;

        private static int DecodeNibble(int code, int predictor, ref int index)
        {
            int step = Steps[index];
            int delta = step / 8;

            if ((code & 4) != 0)
            {
                delta += step;
            }

            if ((code & 2) != 0)
            {
                delta += step / 2;
            }

            if ((code & 1) != 0)
            {
                delta += step / 4;
            }

            index = Math.Clamp(index + Indices[code & 7], 0, 88);
            return Math.Clamp(predictor + ((code & 8) != 0 ? -delta : delta), short.MinValue, short.MaxValue);
        }
    }
}
