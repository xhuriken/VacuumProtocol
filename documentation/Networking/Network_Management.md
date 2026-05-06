# Network Management

This feature handles the core Mirror Network Manager configuration and scene transitions.

## Principle
The project uses **Mirror** for high-level networking. A custom `MyNetworkManager` extends the base functionality to handle specific player prefab spawning based on which scene is currently active (Lobby vs Gameplay).

## Related Files
- `Assets/1_Scripts/Networking/Manager/MyNetworkManager.cs`: Custom manager for player instantiation and connection lifecycle.

---

## File Details

### MyNetworkManager.cs
**Context:** Global persistent object (usually in a Bootstrap or Lobby scene).
**Usage:** Orchestrates connections and scene changes.

#### Variables
- `_playerPrefab`: The prefab spawned for the Lobby scene.
- `_gamePlayerPrefab`: The prefab spawned for the gameplay scene (Mecha/Robot).
- `GamePlayers`: A list of all `PlayerObjectController` instances currently connected.

#### Functions
- `OnServerAddPlayer()`: Triggered when a client connects. It checks `SceneManager.GetActiveScene().name` to decide whether to spawn a lobby player or a gameplay robot.
- `OnStartServer()` / `OnStopServer()`: Logs server lifecycle events.
- `OnClientConnect()` / `OnClientDisconnect()`: Logs client connection status.
- `ServerChangeScene()`: Mirror method used to move all clients to a new scene simultaneously.
