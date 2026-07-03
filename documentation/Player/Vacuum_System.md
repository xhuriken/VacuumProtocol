# Système d'Aspiration (Vacuum System)

Le système d'aspiration est la mécanique principale du jeu. Il permet aux joueurs d'absorber des objets, de les stocker, et de les recracher. L'implémentation est réseau-centrée (Mirror) pour garantir une synchronisation parfaite entre les clients.

## 1. Flux d'Aspiration et Rôles

1. **PlayerVacuumController** (`Assets/1_Scripts/Player/Controller/PlayerVacuumController.cs`) :
   - **Rôle** : Orchestrateur de la mécanique d'aspiration.
   - **Déclencheur** : L'aspiration (`IsVacuuming`) est active lorsque le clic gauche et le clic droit sont maintenus simultanément (géré via `PlayerInputHandler`).
   - **Réseau** : Communique l'état d'aspiration au serveur (`CmdSetVacuumState`), qui le synchronise via un `SyncVar`. C'est également ce script qui envoie la commande serveur pour absorber un objet (`CmdAbsorbObject`).

2. **VacuumSuctionZone** (`Assets/1_Scripts/Physics/VacuumSuctionZone.cs`) :
   - **Rôle** : Le déclencheur physique (Trigger Box) situé à l'avant du joueur.
   - **Fonctionnement** : Applique une force d'attraction croissante sur les objets (`Collectible`) proportionnelle à leur proximité. Plus l'objet est proche, plus il rétrécit (scale shrinking) pour simuler son absorption dans la buse. Dès qu'un seuil de distance critique est franchi, la zone appelle `AbsorbObject` sur le `PlayerVacuumController`.

3. **PlayerInventory** (`Assets/1_Scripts/Player/Controller/PlayerInventory.cs`) :
   - **Rôle** : Gère le stockage temporaire (LIFO) des objets aspirés.
   - **Capacité** : Limitée par `_maxCapacity` (ex: 10).
   - **Sécurité Réseau** : Les méthodes d'ajout (`AddItem`) et de rejet (`SpitItem`) sont restreintes au serveur (`[Server]`). Le serveur désactive l'objet absorbé et l'ajoute à la pile, ou le réactive, le repositionne et applique une impulsion de lancement lors du crachat.

4. **Collectible** (`Assets/1_Scripts/Physics/Collectible.cs`) :
   - **Rôle** : Les objets de l'environnement interactifs.
   - **Particularité** : Sauvegardent leur échelle initiale (scale) au démarrage (`Awake`). S'ils échappent à la zone d'aspiration, ils reprennent progressivement leur taille d'origine.

## 2. Audio Systémique de l'Aspirateur

Le son de l'aspirateur est conçu pour être hautement personnalisable par joueur, ajoutant une identité auditive unique.

### Paramètres Personnalisables (Lobby Customization)
Les joueurs peuvent personnaliser la note musicale de base (`PlayerRootNote`) via le script `PlayerCustomization.cs`. Cette valeur est synchronisée par Mirror via un `SyncVar`.

### Implémentation Audio (`VacuumAudioController.cs`)
- **Mécanisme** : Gère la synthèse sonore en temps réel ou la modulation de filtres (via `OnAudioFilterRead`).
- **Lien avec Customization** : La méthode `SetRootNote` injecte la note musicale choisie, et `SetVacuumState` contrôle les boucles et les transitions (Fade In/Out) via DOTween.

## 3. Comportements des Bras (`PlayerArmsController.cs`)

L'aspiration n'est visuellement active que si le joueur étend les bras vers l'avant.
- **Principe de Physique** : Les bras utilisent des `ConfigurableJoints` (ressorts). Le code modifie les ancres cibles (`targetPosition` et `targetRotation`) vers le point de visée.
- **Cracher un objet** : Effectué via un clic gauche seul. Le code impose un **délai physique** : le crachat (`TrySpitItem`) n'est ordonné au serveur que lorsque le bras gauche a réellement atteint 80% de son extension physique (ou via un timeout de secours), rendant l'animation du recul de tir très organique.
