# VoiceChat

Optional proximity voice for Slendytubbies 3 / NullFrame servers.

Install the supplied NullServers.dll on the host and clients. Install VoiceChat.dll only for players who want to send/hear voice. The host can forward voice without the optional DLL. Do not require VoiceChat in room content manifests.

- Hold **V** to talk. Change the key under **Options → Voice Chat** or **Pause → Voice Chat**.
- Select a microphone by clicking its device button; Refresh rescans connected devices.
- Microphone gain: 0–4x. Voice playback volume: 0–1x.
- Test microphone shows a local input meter and transmits nothing.
- The HUD microphone turns green with the signal level; speaking players have a speaker icon.
- Settings and recording stop on room changes. Losing window focus stops recording.

Build with .NET 10 SDK, targeting net6.0, against the current game's MelonLoader assemblies and NullServers 0.0.66. Override GamePath and NullServersPath as needed. Run tests with `dotnet run --project tests/VoiceChat.Checks.csproj -c Release`.

See docs/PROTOCOL.md for transport, limits and known limitations. A real two-person voice quality/latency check is still required before treating this initial version as production-tested.
