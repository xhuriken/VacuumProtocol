# Réseau et Gestion du Lobby (Network & Lobby)

Le système multijoueur utilise **Mirror** pour la synchronisation réseau de haut niveau et **Steamworks.NET** pour le matchmaking (invitations d'amis, création de lobby).

## 1. Handshake Steam vers Mirror

1. **SteamLobby** (`Assets/1_Scripts/Networking/Lobby/SteamLobby.cs`) :
   - C'est la porte d'entrée. Il intercepte les requêtes de création ou de jonction de lobby Steam.
   - Lorsqu'un lobby est créé (`OnLobbyCreated`), il ordonne au `MyNetworkManager` de démarrer en tant que Host (`StartHost`).
   - Lorsqu'un client rejoint via une invitation (`OnLobbyEntered`), il lit l'ID Steam de l'hôte stocké dans les métadonnées du lobby Steam, l'assigne au `networkAddress` du Manager, puis démarre en tant que Client (`StartClient`).

2. **MyNetworkManager** (`Assets/1_Scripts/Networking/Manager/MyNetworkManager.cs`) :
   - Remplace le `NetworkManager` par défaut de Mirror.
   - **Particularité (KISS)** : Surcharge `OnServerAddPlayer` pour instancier des prefabs différents selon la scène active.
   - Si la scène est "Lobby", il spawn un simple `PlayerObjectController` (invisible).
   - S'il s'agit d'une scène de jeu, il spawn le lourd prefab du robot (`_gamePlayerPrefab`).

## 2. Interface du Lobby et Joueurs

1. **LobbyController** (`Assets/1_Scripts/Networking/Lobby/LobbyController.cs`) :
   - **Rôle** : L'orchestrateur de l'interface utilisateur. Maintient la liste visuelle (`PlayerListItem`) en phase avec la liste réseau (`GamePlayers`).
   - Gère le bouton **Start Game**, qui n'est cliquable que par l'Hôte (PlayerId == 1) et seulement si tout le monde est prêt.

2. **PlayerObjectController** (`Assets/1_Scripts/Networking/Lobby/PlayerObjectController.cs`) :
   - C'est la représentation réseau du joueur dans la scène du lobby (Attaché au prefab).
   - Utilise des `[SyncVar]` avec des `hook` (ex: `PlayerNameUpdate`, `PlayerReadyUpdate`).
   - **Flux de données** : Dès qu'une valeur change sur le serveur, le hook est déclenché chez tous les clients, forçant le `LobbyController` à rafraîchir la liste visuelle sans avoir besoin de requêtes régulières (polling).

3. **LobbyCustomizationUI** (`Assets/1_Scripts/Networking/Lobby/LobbyCustomizationUI.cs`) :
   - Fait le pont entre les boutons cliqués (Couleur, Note audio) et le système réseau.
   - Sauvegarde immédiatement les choix dans les `PlayerPrefs` locaux pour qu'ils soient récupérés par le robot de jeu lors du transfert de scène.
