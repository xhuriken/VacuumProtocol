# Peaufinage Visuel (Visual Polish)

Ces systèmes gèrent les comportements interactifs, d'animations procédurales et de personnalisation visuelle du joueur, qui améliorent l'immersion et donnent vie au robot.

## 1. Personnalisation Multi-joueurs (Customization)

### PlayerCustomization (`Assets/1_Scripts/Player/Visuals/PlayerCustomization.cs`)
- **Rôle** : Synchronise les couleurs et les sons choisis dans le Lobby à travers le réseau sur l'avatar In-Game.
- **Fonctionnement** : 
  - Utilise des `[SyncVar]` Mirror pour `PlayerColor` (couleur) et `PlayerRootNote` (note de synthé audio).
  - À la connexion d'un client distant (`OnStartClient`), les valeurs synchronisées lui sont immédiatement appliquées pour éviter les "flashs" de couleur par défaut.
  - Le joueur local (`OnStartLocalPlayer`) lit les préférences sauvegardées localement (PlayerPrefs) et demande au serveur de les imposer à tous.
- **Justification** : Clone dynamiquement le matériau (Instanced Material) au démarrage pour éviter de teinter accidentellement tous les robots de la session qui partageraient le même Material source. Les "Lobby Dummies" (mannequins hors ligne) sont pris en charge de façon transparente.

## 2. Détection de Priorité (PlayerViewRange)

### PlayerViewRange (`Assets/1_Scripts/Player/Visuals/PlayerViewRange.cs`)
- **Rôle** : Le "radar" frontal du robot, utilisé pour détecter les entités intéressantes avec lesquelles le joueur interagit visuellement.
- **Mécanique** :
  1. **OverlapSphere** : Collecte tous les objets dans le rayon d'action (`_viewDistance`).
  2. **Cône de vision (FOV)** : Exclut les objets hors de l'angle (`_viewAngle`).
  3. **Ligne de mire (Line-of-Sight)** : Lance un Raycast sur la couche d'obstacles (`_obstacleLayer`) pour s'assurer que l'objet n'est pas caché derrière un mur.
- **Priorité** : Trie les entités valides selon leur `PriorityLevel` (exposé par l'interface `IEntity`) et stocke la meilleure cible dans `HighestPriorityEntity`.
- **Performance** : Totalement désactivé sur les clients distants (car seuls les joueurs locaux doivent calculer vers quoi leurs propres yeux regardent).

## 3. Animation d'Œil Procédurale (Eye)

### Eye (`Assets/1_Scripts/Player/Visuals/Eye.cs`)
- **Rôle** : Contrôle l'orientation de l'œil du robot vers la cible détectée par `PlayerViewRange`.
- **Comportement (Slerp et Quaternions)** : 
  - Convertit la direction cible du monde mondial en rotation locale relative au parent.
  - Applique cette rotation via un `Quaternion.Slerp` à une vitesse définie (`_rotationSpeed`), ce qui simule le mouvement saccadé et organique de l'œil biologique.
  - Si aucune cible n'est vue, l'œil retourne à sa rotation locale d'origine (mémorisée dans `Start`).

## 4. Roues Directionnelles Automatiques (Wheels)

### WheelSteering (`Assets/1_Scripts/Player/Visuals/Wheels.cs`)
- **Rôle** : Oriente les os des roulettes pour qu'elles fassent face à la direction du déplacement.
- **Mécanique (Façon siège de bureau)** : 
  - Récupère le vecteur de vélocité du `Rigidbody`.
  - Projette cette vélocité sur le plan horizontal (Y = 0) et calcule la rotation Y cible.
  - Applique la rotation via `Mathf.LerpAngle` de manière douce sur l'axe Y local de l'os (sans corrompre l'axe X ou Z).
- **Avantage** : Entièrement découplé des animations (pas besoin d'Animator Blend Trees complexes).

## 5. Utilitaire de Migration d'Éditeur (ModelMigrator)

### ModelMigrator (`Assets/1_Scripts/Player/Utilities/ModelMigrator.cs`)
- **Rôle** : Outil `[Button]` Odin Inspector pour re-rigger un nouveau modèle 3D sans perdre les références et les scripts du GameObject parent (le Player).
- **Fonctionnement** :
  - Traverse l'ancien modèle, identifie les scripts, colliders et sous-objets personnalisés (caméras, particules).
  - Les copie/colle ou les re-parente sur les os correspondants du nouveau modèle.
  - Utilise la `Reflection` C# pour scanner tous les `MonoBehaviour` du joueur et réaffecter automatiquement les références pointant vers l'ancien modèle vers le nouveau.
