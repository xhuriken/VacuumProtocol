# Voice System

This feature provides real-time VoIP (Voice over IP) with 3D spatialization for multiplayer communication.

## Principle
The system integrates **UniVoice** with Mirror. It captures microphone input, encodes it (using Opus/Concentus), sends it over the Mirror transport, and decodes it on other clients. Audio sources are dynamically positioned on player models to provide spatial awareness.

## Related Files
- `Assets/1_Scripts/Audio/UniVoiceMirrorSetupSample.cs`: Configures the UniVoice server and client.
- `Assets/1_Scripts/Audio/UniVoicePlayerAudio.cs`: Synchronizes the voice audio source with the player object's position.
- `Assets/1_Scripts/Audio/MouthAnimator.cs`: Animates the player's mouth based on voice volume (local/remote) and vacuum state.

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

### MouthAnimator.cs
**Context:** Attached to the Player prefab (specifically on the Mouth object).
**Usage:** Synchronizes mouth scaling with voice volume for both local and remote players.

#### Variables
- `_mouthTransform`: The transform to scale.
- `_remoteVoiceSource`: The AudioSource used for remote voice playback.
- `_vacuumController`: Reference to `PlayerVacuumController` for the vacuum bypass.
- `_sensitivity`: Multiplier for voice-to-scale mapping.
- `_enableDebugLogs`: Toggle for detailed console logging of ID finding and audio detection.

#### Functions
- `SetupLocalMicLogging()`: Coroutine that subscribes to local microphone frames if `isLocalPlayer` is true.
- `Update()`: 
    - If local: uses mic peak data.
    - If remote: uses `AudioSource.GetOutputData` to calculate peak volume.
    - Applies vacuum bypass (forces max scale if vacuuming).
    - Interpolates scale for smooth animation.
