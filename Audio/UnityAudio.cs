using Il2CppInterop.Runtime;
using System.Runtime.InteropServices;
using UnityEngine;

namespace VoiceChat.Audio
{
    internal static unsafe class UnityAudio
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSpan
        {
            internal void* Data;
            internal int Length;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int DeviceCall(ref NativeSpan name);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr StartCall(int device, [MarshalAs(UnmanagedType.I1)] bool loop, float seconds, int rate);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int PositionCall(int device);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void EndCall(int device);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void CapsCall(int device, out int minimum, out int maximum);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr ConstructCall();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void CreateCall(IntPtr clip, ref NativeSpan name, int samples, int channels, int rate,
            [MarshalAs(UnmanagedType.I1)] bool stream);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool DataCall(IntPtr clip, ref NativeSpan samples, int offset);

        private static readonly DeviceCall Device = IL2CPP.ResolveICall<DeviceCall>("UnityEngine.Microphone::GetMicrophoneDeviceIDFromName_Injected");
        private static readonly StartCall StartRecord = IL2CPP.ResolveICall<StartCall>("UnityEngine.Microphone::StartRecord_Injected");
        private static readonly PositionCall Position = IL2CPP.ResolveICall<PositionCall>("UnityEngine.Microphone::GetRecordPosition");
        private static readonly EndCall EndRecord = IL2CPP.ResolveICall<EndCall>("UnityEngine.Microphone::EndRecord");
        private static readonly CapsCall DeviceCaps = IL2CPP.ResolveICall<CapsCall>("UnityEngine.Microphone::GetDeviceCaps");
        private static readonly ConstructCall Construct = IL2CPP.ResolveICall<ConstructCall>("UnityEngine.AudioClip::Construct_Internal_Injected");
        private static readonly CreateCall CreateSound = IL2CPP.ResolveICall<CreateCall>("UnityEngine.AudioClip::CreateUserSound_Injected");
        private static readonly DataCall ReadData = IL2CPP.ResolveICall<DataCall>("UnityEngine.AudioClip::GetData_Injected");

        internal static int DeviceId(string name)
        {
            fixed (char* text = name)
            {
                NativeSpan span = new() { Data = text, Length = name?.Length ?? 0 };
                return Device(ref span);
            }
        }

        internal static void Caps(int device, out int minimum, out int maximum) => DeviceCaps(device, out minimum, out maximum);

        internal static AudioClip Start(int device, int rate) => Unmarshal(StartRecord(device, true, 1f, rate));

        internal static int GetPosition(int device) => Position(device);

        internal static void End(int device) => EndRecord(device);

        private static AudioClip Unmarshal(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return null;

            IntPtr target = IL2CPP.il2cpp_gchandle_get_target(handle);
            if (target == IntPtr.Zero) throw new InvalidOperationException("Unity did not return an audio clip.");

            return new AudioClip(target);
        }

        internal static AudioClip Create(AudioClip.PCMReaderCallback reader)
        {
            AudioClip clip = Unmarshal(Construct());
            clip.m_PCMReaderCallback = reader;
            const string name = "NullFrameVoiceStream";
            fixed (char* text = name)
            {
                NativeSpan span = new() { Data = text, Length = name.Length };
                CreateSound(clip.m_CachedPtr, ref span, ImaCodec.Rate, 1, ImaCodec.Rate, true);
            }

            return clip;
        }

        internal static bool GetData(AudioClip clip, float[] data, int offset)
        {
            fixed (float* samples = data)
            {
                NativeSpan span = new() { Data = samples, Length = data.Length };
                return ReadData(clip.m_CachedPtr, ref span, offset);
            }
        }
    }
}
