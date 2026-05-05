# Documentation Système Voix (UniVoice + Mirror)

Ce document résume les découvertes techniques et les corrections apportées au système de chat vocal multi-joueurs, ainsi qu'une explication approfondie du fonctionnement de la librairie UniVoice.

---

## 0. Fonctionnement Interne de UniVoice (Deep Dive)

UniVoice est une librairie modulaire conçue pour être indépendante du moteur de rendu et du système réseau. Elle repose sur trois piliers :

### A. Le Pipeline de Données (Input -> Network -> Output)
1. **Capture (IAudioInput)** : 
   - Utilise `UniMic` pour capturer le flux audio du microphone.
   - Les données sont découpées en "Frames" (généralement de 60ms).
   - Chaque frame est un objet `AudioFrame` contenant un tableau d'octets (`byte[] samples`).
   - *Note technique* : Le format des samples dépend du driver. Pour les micros pros (Samson C03U), Unity capture en 32-bit Float, ce qui signifie que chaque nombre est codé sur 4 octets.

2. **Transport (IChatroomNetwork / IAudioClient)** :
   - UniVoice ne sait pas envoyer de données sur Internet tout seul. Il délègue cela à une implémentation (ici Mirror).
   - Le `MirrorClient` sérialise l'objet `AudioFrame` en un paquet de bytes (`MirrorMessage`) et l'envoie au serveur via les canaux **Unreliable** de Mirror (pour la performance).

3. **Restitution (IAudioOutput)** :
   - Quand le client reçoit un paquet d'un autre joueur, il cherche l'ID du "Peer" (le partenaire).
   - Il crée un `StreamedAudioSource` : c'est un composant qui gère un buffer (une file d'attente) pour lisser les micro-coupures réseau.
   - Le son est joué via un `AudioSource` standard de Unity.

### B. Le ClientSession
C'est le "cerveau" du système. Il fait le lien entre ton micro et le réseau. 
- Il écoute ton micro via l'événement `OnFrameReady`.
- Dès qu'une frame est prête, il la donne au `IAudioClient` pour l'envoyer.
- Il gère aussi le dictionnaire `PeerOutputs`, qui contient toutes les voix des autres joueurs que tu es en train d'écouter.

---

## 1. Problèmes Identifiés et Résolus

### A. Bug de l'Hôte (Host)
- **Symptôme** : L'Hôte entend les clients, mais aucun client n'entend l'Hôte.
- **Cause (Détail Technique)** : Dans Mirror, l'Hôte est une `LocalConnection`. Le `MirrorServer` de UniVoice envoie un message `PEER_INIT` au démarrage. Cependant, comme tout se passe sur la même machine, ce message est envoyé avant même que le `MirrorClient` de l'Hôte ne soit prêt à le recevoir. L'Hôte rate son propre message d'initialisation et garde un `ID = -1`. Sans ID, le système de sécurité de UniVoice bloque tout envoi audio vers les autres joueurs.
- **Solution** : Notre `CustomMirrorClient` détecte s'il tourne sur un Hôte et s'auto-identifie avec l'ID `0` sans attendre de message réseau.

### B. Format de Données Micro (Samson C03U)
- **Symptôme** : Volume bloqué à 100% en permanence dans les logs.
- **Cause** : Le micro (48kHz) envoyait des données en **32-bit Float**, alors que le script de debug essayait de lire du **16-bit PCM**.
- **Solution** : Utilisation de `System.BitConverter.ToSingle` avec un pas de 4 octets pour calculer correctement le pic de volume.

### C. Mapping des Identités (Peer ID vs NetID)
- **Symptôme** : La voix n'était pas attachée au robot ou échouait après un timeout.
- **Cause** : `netId` de Mirror change à chaque spawn, alors que l'ID UniVoice est lié à la connexion.
- **Solution** : Ajout d'un `[SyncVar] public int ConnectionId` sur les prefabs joueurs. Le `VoiceBridge` utilise cet ID pour faire le lien.

---

## 2. Architecture du Fix (`Assets/1_Scripts/Network/UniVoiceFix`)

Pour contourner les bugs de la DLL UniVoice, les classes suivantes ont été réimplémentées :
- **`CustomMirrorClient`** : Gère l'envoi/réception des paquets audio via Mirror. Force l'ID 0 pour l'Hôte.
- **`CustomMirrorServer`** : Gère la redistribution (broadcast) des paquets audio à tous les clients.
- **`CustomUniVoiceSetup`** : Remplace le sample de la librairie. C'est le point d'entrée central.

---

## 3. Workflow de Spatialisation

1. **Connexion** : UniVoice déclenche `OnPeerJoined(id)`.
2. **Recherche** : `VoiceBridge` parcourt les objets `NetworkClient.spawned`.
3. **Identification** : Il cherche un objet avec `PlayerPhysicsMovement` ou `PlayerObjectController` ayant le `ConnectionId` correspondant.
4. **Attachement** :
   - Le `StreamedAudioSource` devient enfant du robot.
   - `spatialBlend` est réglé sur `1.0` (3D).
   - En cas d'échec (timeout), le système bascule en **2D (Global)** par sécurité.

---

## 4. Notes pour le Futur
- **Changement de Scène** : Lors du passage Lobby -> Jeu, les objets sont remplacés. Le `VoiceBridge` actuel attend 10s, ce qui couvre généralement la transition.
- **VAD** : La détection d'activité vocale (VAD) est désactivée par défaut dans `CustomUniVoiceSetup` pour éviter les coupures intempestives.
- **Dépendances** : Ce système nécessite `Mirror` et les types de base de `Adrenak.UniVoice`.

---
*Document généré par Antigravity - 06/05/2026*
