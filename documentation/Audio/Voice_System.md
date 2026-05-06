# Voice System

This feature provides real-time VoIP (Voice over IP) with 3D spatialization for multiplayer communication.

## Principle
The system integrates **UniVoice** with Mirror. It captures microphone input, encodes it (using Opus/Concentus), sends it over the Mirror transport, and decodes it on other clients. Audio sources are dynamically positioned on player models to provide spatial awareness.

## Related Files
- `Assets/1_Scripts/Audio/UniVoiceMirrorSetupSample.cs`: Configures the UniVoice server and client.
- `Assets/1_Scripts/Audio/UniVoicePlayerAudio.cs`: Synchronizes the voice audio source with the player object's position.
- `Assets/1_Scripts/Audio/MicVolumeLogger.cs`: Debug utility to monitor microphone input levels.

---

## File Details

### UniVoiceMirrorSetupSample.cs
**Context:** Placed on a global object in the first scene.
**Usage:** Initializes UniVoice once. Runs on both host and clients.

#### Variables
- `HasSetUp`: Static flag to prevent multiple initializations.
- `ClientSession`: Global reference to the current UniVoice session.
- `useConcentusEncodeAndDecode`: Toggle for Opus compression.
- `useVad`: Toggle for Voice Activity Detection (only sends audio when speaking).

#### Functions
- `Setup()`: Initializes the `MirrorServer` and `MirrorClient`.
- `Update()`: Contains a fix for Steamworks where the Host ID might be reported as -1; it forces it to 0 for correct local playback.
- `SetupClientSession()`: Configures the microphone device and registers audio filters.

### UniVoicePlayerAudio.cs
**Context:** Attached to the Player prefab.
**Usage:** Exists on every player instance.

#### Variables
- `_cachedId`: The connection ID associated with this player object.

#### Functions
- `OnStartClient()`: Retrieves the connection ID to match this object with its corresponding UniVoice peer.
- `Update()`: Finds the audio output source for the player and moves its `transform.position` to follow the player's model with a 1.5m vertical offset.

### MicVolumeLogger.cs
**Context:** Optional debug object.
**Usage:** Logs the current microphone peak volume to the console.

#### Variables
- `_enableLogging`: Boolean toggle.
- `_lastPeak`: Stores the highest volume sample from the last audio frame.

#### Functions
- `SetupLogger()`: Coroutine that subscribes to the UniVoice input frame events.
- `Update()`: Prints the volume percentage to the console every 60 frames if sound is detected.
