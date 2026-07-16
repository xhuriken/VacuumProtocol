# Plan de Reconstruction et Validation Étape par Étape (V5)

Ce plan décrit la reconstruction complète (à partir de zéro) des fonctionnalités de **caméra**, de **rotation de la tête**, de **courbure du cou**, de **déplacement**, et de **suivi des yeux**. 

Pour éliminer définitivement les bugs et le code inutile, nous allons **supprimer le code existant lié à ces fonctionnalités** et le réécrire étape par étape, en validant chaque phase dans l'éditeur.

---

## 1. Nouvelle Architecture Physique & Hiérarchie

### Hiérarchie des Transforms dans l'Éditeur :
* Le joueur (`NewPlayer`) est le parent de l'Armature.
* La **Camera** est placée manuellement dans l'éditeur comme enfant de l'os **Head** (qui est lui-même enfant de `Neck_04`).
* Au démarrage (`Start`), la tête se détache du parent (`SetParent(null)`), emportant la caméra avec elle dans l'espace physique.

### Rôles et Précision de Rotation :
1. **Caméra (100% précision partout)** :
   * Pour éviter le doublon de rotation (caméra enfant de la tête qui tourne elle-même), la rotation monde de la caméra est forcée à 100% d'exactitude par le script de visée :
     `camera.rotation = Quaternion.Euler(_cameraPitch, torsoYaw, 0f);`
   * **Bénéfice** : La visée reste à 100% nette (pas de double sensibilité ni de lag), mais la caméra suit physiquement la position et les secousses/recouvrements de la tête pour un feeling ultra-immersif.
2. **Tête Physique (Yaw: 100% / Pitch: 70%)** :
   * **Lacet (Y / Yaw)** : Le joint est **Locked** à 100% sur la rotation du torse. Pas de retard ni de torsion horizontale du cou lors du demi-tour.
   * **Tangage (X / Pitch)** : Suit le cou et la visée avec un ratio de **70%** (`_followRatio = 0.7f`) pour créer une inertie verticale et permettre les amortis physiques de dodinement (wiggle).
3. **Yeux (70% partout)** : Vitesse slerpée pour simuler les saccades et le retard biologique.
4. **Pupilles (100% partout)** : Os enfants des yeux, s'orientent instantanément vers la cible sans aucun amortissement.

---

## 2. Directives de Code (KISS, SSOT et Erreurs)

* **Pas de cache silencieux** : Retrait de tous les `if (xxx == null)` de confort. Si une référence configurée dans l'inspecteur est manquante, une `NullReferenceException` doit être levée ou une erreur critique explicitement logguée pour bloquer le jeu.
* **Logs de debug** : Présence d'un booléen `_enableDebugLogs` dans chaque composant pour activer des logs clairs et compacts dans la console.

---

## 3. Plan de Reconstruction Étape par Étape

### ÉTAPE 0 : Nettoyage Complet (Scrap)
* [ ] Vider le contenu logique de `PhysicalHeadController.cs` et `PlayerLookComponent.cs`.

---

### ÉTAPE 1 : Rotation du Torse (Yaw), Caméra (Pitch) et Déplacement Relatif
L'objectif est d'avoir une visée souris stable en tournant le torse et la caméra, et de s'assurer que le joueur se déplace dans la direction où regarde son torse.

#### Configuration Éditeur :
* `PlayerLookComponent` et `PlayerMovementComponent` sur **NewPlayer**.
* Slots requis (non nuls) :
  * `_cameraTransform` : Le GameObject Camera (placé sous `Head`).
  * `_torsoBone` : L'os **Torso** (Rigidbody **Kinematic**).

#### Code à implémenter :
* **Visée (Look)** :
  * Gestion de la souris dans `Update()`.
  * Application de la rotation Yaw sur le Rigidbody du Torse via `MoveRotation` dans `FixedUpdate`.
  * Application de la rotation Monde à 100% sur la caméra dans `Update` ou `LateUpdate` (prévention du doublon de sensibilité) :
    `_cameraTransform.rotation = Quaternion.Euler(_cameraPitch, _cameraYaw, 0f);`
* **Déplacement (Movement)** :
  * Calcul de la direction de mouvement relative au lacet du torse (`PlayerLookComponent.CurrentYaw`) plutôt qu'au root `NewPlayer` (qui ne tourne pas) :
    ```csharp
    float yaw = lookComponent.CurrentYaw;
    Vector3 lookForward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
    Vector3 lookRight = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
    Vector3 moveDirection = lookRight * input.x + lookForward * input.y;
    ```

#### Validation Étape 1 :
* [ ] **Visuel** : Le torse pivote horizontalement avec la souris. La caméra tourne haut/bas verticalement sans double sensibilité.
* [ ] **Physique** : Appuyer sur Z fait avancer le robot dans la direction exacte du torse, même si les roues restent initialement fixes.
* [ ] **Erreurs** : Une erreur rouge critique apparaît si le Rigidbody du Torse n'est pas Kinematic.

---

### ÉTAPE 2 : Courbure du Cou (Neck Bending) & Offset Signé
L'objectif est de plier les os du cou (Neck_02, Neck_03, Neck_04, voire l'os Head lui-même si désiré) proportionnellement au regard vertical de la caméra, tout en translatant les os du cou en Z pour éviter que la tête n'entre dans le corps.

#### Configuration Éditeur :
* Dans `PhysicalHeadController.cs` :
  * `_neckBones` : Liste d'os qui doivent tourner (ex: `Neck_02`, `Neck_03`, `Neck_04` et potentiellement `Head` si l'utilisateur souhaite inclure l'os de la tête dans la répartition de rotation).
  * `_neckRotationWeights` : Répartition (ex: `0.15`, `0.25`, `0.35`, `0.25`).
  * `_neckBackwardFactors` : Facteur d'offset signé **uniquement pour les os du cou** (ex: `0.002`, `0.004`, `0.008`, `0` pour l'os Head qui n'est pas translaté).

#### Code à implémenter :
* Calcul de la rotation Pitch progressive sur tous les os de la liste `_neckBones` en fonction du pitch caméra.
* Application de l'offset signé en Z (sans `Mathf.Abs`) uniquement si le facteur de translation de l'os est supérieur à 0 : recul en `-Z` si regarde en bas, avance en `+Z` si regarde en haut.

#### Validation Étape 2 :
* [ ] **Visuel** : Le cou forme une courbe verticale propre. La tête recule en regardant en bas et avance en regardant en haut. Les os de cou se translatent tandis que l'os de la tête (si présent dans la liste) ne fait que pivoter sans se translater.

---

### ÉTAPE 3 : Joint Physique de la Tête
L'objectif est de lier la tête au Torse avec un ConfigurableJoint, de la détacher au démarrage, et de la faire suivre le cou.

#### Configuration Éditeur :
* Le GameObject **Head** (unparented au Start) :
  * Rigidbody (Mass = 0.1, useGravity = false).
  * ConfigurableJoint connecté au Rigidbody du **Torso** (Kinematic).
  * `Angular Y Motion` (yaw) : **Locked**.
  * `Angular X/Z Motion` : **Limited** ou **Free** pour le dodinement (X) et l'inclinaison (Z).
  * Slerp Drive Spring = **8000**, Damper = **300**.

#### Code à implémenter :
* `transform.SetParent(null)` au démarrage.
* Calcul des targets joint (`targetPosition` et `targetRotation`) dans le référentiel du Torse (`connectedBody`).
* Application du ratio de suivi de rotation de 70% (`_followRatio = 0.7f`) uniquement pour l'axe vertical (Pitch/X) de la tête.

#### Validation Étape 3 :
* [ ] **Visuel** : La tête dodeline d'avant en arrière sur les freinages/collisions mais reste parfaitement droite sur le plan horizontal (pas de torsion).

---

### ÉTAPE 4 : Yeux et Pupilles (Eye-Tracking)
L'objectif est d'orienter les yeux et les pupilles vers la cible.

#### Configuration Éditeur :
* Script `Eye` sur les os des yeux.

#### Code à implémenter :
* Rotation des yeux slerpée à **70%**.
* Rotation des os de pupilles (enfants des yeux) alignée à **100%** sur la cible.

#### Validation Étape 4 :
* [ ] **Visuel** : Les yeux et pupilles fixent l'entité avec le bon ratio réactivité/naturel.
