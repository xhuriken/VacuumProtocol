# Lobby System

This feature handles the Steam integration for multiplayer lobbies, allowing players to create, join, and synchronize their status before starting the game.

## Principle
The system uses **Steamworks.NET** to communicate with the Steam API. It creates a Steam lobby, stores the host's connection address in the lobby data, and uses Mirror to handle the actual networking once players are connected.

## Related Files
- `Assets/1_Scripts/Networking/Lobby/SteamLobby.cs`: Handles Steam API calls for lobby creation and joining.
- `Assets/1_Scripts/Networking/Lobby/LobbyController.cs`: Manages the UI list of players and the ready system.
- `Assets/1_Scripts/Networking/Lobby/PlayerObjectController.cs`: The networked representation of a player in the lobby.
- `Assets/1_Scripts/Networking/Lobby/PlayerListItem.cs`: UI element for a single player entry in the list.
- `Assets/1_Scripts/Networking/Lobby/SteamManager.cs`: Boilerplate for Steam API initialization.

---

## File Details

### SteamLobby.cs
**Context:** Attached to a persistent NetworkManager object in the Lobby scene.
**Usage:** Automatically initializes on Start. `HostLobby()` is called by a UI button.

#### Variables
- `Instance`: Singleton reference for easy access.
- `CurrentLobbyId`: Stores the ID of the Steam lobby we are currently in.
- `LobbyCreated`, `JoinRequest`, `LobbyEntered`: Steam callbacks for async operations.

#### Functions
- `HostLobby()`: Calls Steam to create a new friends-only lobby.
- `OnLobbyCreated()`: Called when Steam confirms lobby creation. Starts the Mirror host and sets lobby metadata (Name, HostAddress).
- `OnJoinRequest()`: Triggered when someone joins via Steam UI.
- `OnLobbyEntered()`: Triggered when the player successfully enters a lobby. Connects the Mirror client to the host address stored in the lobby data.

### LobbyController.cs
**Context:** Manages the "Lobby" scene UI.
**Usage:** Updates the player list whenever a player joins, leaves, or changes their ready state.

#### Variables
- `LobbyNameText`: Displays the name of the Steam lobby.
- `PlayerListItems`: Internal list of UI items representing connected players.
- `LocalPlayerController`: Reference to the local player's network object.
- `ReadyButtonText`: UI text that toggles between "Plug" (Ready) and "Unplug" (Not Ready).

#### Functions
- `ReadyPlayer()`: Called by the UI button to toggle the local player's readiness.
- `UpdatePlayerList()`: Syncs the UI items with the `GamePlayers` list in the Network Manager.
- `CheckIfAllReady()`: Enables the "Start" button for the host if everyone is ready.

### PlayerObjectController.cs
**Context:** Prefab spawned by Mirror when a player connects.
**Usage:** Exists throughout the lobby duration to sync data.

#### Variables
- `PlayerName`: SyncVar holding the Steam persona name.
- `Ready`: SyncVar boolean for the player's ready status.
- `ConnectionId`: Mirror connection ID.
- `PlayerSteamId`: Steam ID used to fetch avatars.

#### Functions
- `CmdSetPlayerReady()`: Notifies the server that the player changed their ready state.
- `PlayerReadyUpdate()`: Hook that updates the UI when the `Ready` variable changes.
- `CmdStartGame()`: Tells the server to switch to the gameplay scene.

### PlayerListItem.cs
**Context:** UI Prefab instantiated inside the Lobby UI list.

#### Variables
- `PlayerNameText`: UI text for the player name.
- `PlayerIcon`: RawImage displaying the Steam avatar.
- `PlayerReadyText`: Displays "Charged!" or "Not Charged!".

#### Functions
- `SetPlayerValues()`: Updates the UI elements with provided player data.
- `GetSteamImageAsTexture()`: Converts Steam's raw image data into a Unity Texture2D.
