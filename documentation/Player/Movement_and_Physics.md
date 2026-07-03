# Déplacement et Physique du Joueur

Le système de déplacement du joueur a été factorisé en composants modulaires (KISS) afin d'améliorer la maintenabilité, de simplifier l'inspection réseau et de séparer les responsabilités physiques des simples lectures d'entrées (inputs).

## Architecture et Composants
Le système suit une conception **Orientée Composant** :

1. **PlayerInputHandler** (`Assets/1_Scripts/Player/Controller/PlayerInputHandler.cs`) : 
   - **Rôle** : Point d'entrée unique pour les callbacks du nouveau système d'Input d'Unity.
   - **Fonctionnement** : Lit les actions (Move, Look, Jump, Sprint, Bras gauche/droit) et expose ces états sous forme de propriétés publiques (`MoveInput`, `LookInput`, `IsSprinting`, `IsVacuuming`).
   - **Justification** : Centralise la gestion des contrôles pour éviter que chaque script n'interroge l'InputSystem individuellement.

2. **PlayerMovementComponent** (`Assets/1_Scripts/Player/Controller/PlayerMovementComponent.cs`) : 
   - **Rôle** : Gère les déplacements horizontaux et le sprint basés sur la physique (`Rigidbody`).
   - **Fonctionnement** : Applique des forces (`AddForce`) pour l'accélération et limite manuellement la vélocité horizontale (`_maxSpeed`). Gère également l'amortissement décélératif (`linearDamping`) et un contrôle aérien réduit.
   - **Justification** : L'utilisation de forces plutôt que de translations directes donne une sensation de poids et d'inertie réaliste au robot.

3. **PlayerLookComponent** (`Assets/1_Scripts/Player/Controller/PlayerLookComponent.cs`) : 
   - **Rôle** : Gère la rotation de la vue (souris).
   - **Fonctionnement** : La rotation horizontale (Yaw) fait tourner le corps complet du joueur. La rotation verticale (Pitch) n'affecte que la caméra (isolée du corps) pour éviter de renverser le robot en regardant vers le bas ou le haut.

4. **PlayerJumpComponent** (`Assets/1_Scripts/Player/Controller/PlayerJumpComponent.cs`) : 
   - **Rôle** : Gère les sauts et la gravité personnalisée.
   - **Fonctionnement** : Applique une impulsion (`_jumpImpulse`) vers le haut si le joueur est au sol (estimé via une vélocité Y faible). Ajoute une force d'accélération descendante (`_gravityMultiplier`) lors de la chute.
   - **Justification** : La gravité standard d'Unity est souvent trop "flottante" (floaty). Ce multiplicateur force une retombée plus rapide et réactive, façon plateforme classique (ex: Super Mario).

5. **PlayerController** (`Assets/1_Scripts/Player/Controller/PlayerController.cs`) : 
   - **Rôle** : Contrôleur maître et gestionnaire du cycle de vie réseau.
   - **Fonctionnement** : Gère l'activation des entrées (désactive les inputs sur les clones distants via `PlayerInput.enabled`), initialise la caméra locale, et stocke l'ID de connexion réseau (`ConnectionId`).

6. **InputSettingsConsumer** (`Assets/1_Scripts/Player/Controller/InputSettingsConsumer.cs`) : 
   - **Rôle** : Consommateur de paramètres qui gère les remplacements de touches (rebinding).
   - **Fonctionnement** : S'enregistre auprès du `SettingsManager` global, écoute les mises à jour JSON des contrôles et les applique via `LoadBindingOverridesFromJson`.

## Justification Technique Globale (KISS)
Chaque composant se concentre sur une seule tâche (Single Responsibility Principle). Le `Rigidbody` est configuré par défaut en interpolation pour assurer un rendu fluide, et les calculs de vélocité (dans `FixedUpdate`) utilisent `isLocalPlayer` pour empêcher les clients d'appliquer une physique locale sur les avatars distants. Les avatars distants sont interpolés automatiquement par le composant NetworkTransform de Mirror.
