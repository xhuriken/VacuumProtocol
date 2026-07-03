# Systèmes Procéduraux et Physique (Procedural & Physics)

Ce document détaille les systèmes gérant la physique avancée, la génération procédurale de contraintes, et les champs de force d'aspiration du jeu.

## 1. Génération de Corps Souples ([ProceduralTubePhysics.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Physics/ProceduralTubePhysics.cs))

Ce système automatise la configuration de composants physiques lourds qui seraient autrement extrêmement fastidieux à configurer manuellement dans l'éditeur (ex: le tuyau de l'aspirateur).

### A. Principe de Fonctionnement
Le script s'attache à la racine d'une hiérarchie d'os (bones) d'un mesh 3D. Lorsqu'on déclenche l'action `Setup()` via l'inspecteur Odin, il descend récursivement l'arbre et génère dynamiquement une chaîne de type "Softbody" :
* **Rigidbodies** : Ajoutés avec des itérations de solveur élevées pour garantir la stabilité de la chaîne.
* **CapsuleColliders** : Calculés mathématiquement pour relier parfaitement un os à son enfant, évitant ainsi que le tube ne traverse les murs.
* **ConfigurableJoints** : Verrouille les mouvements de translation mais autorise une rotation limitée (`angularLimit`), amortie par des forces de ressort (Spring/Damper) via un mode `Slerp`.

### B. Paramétrage Avancé
* `segmentMass` : Une masse trop élevée déchire les joints, une masse trop faible provoque des tremblements.
* `tipStiffnessMultiplier` : La buse (dernier segment) possède un multiplicateur de rigidité. Cela garantit que la pointe de l'aspirateur reste stable et contrôlable par le joueur, même si le reste du tuyau flotte librement.

---

## 2. Champ de Force d'Aspiration ([VacuumSuctionZone.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Physics/VacuumSuctionZone.cs))

Ce système est attaché au volume de détection (Trigger) situé à l'extrémité de la buse de l'aspirateur.

### A. Attraction Physique Dynamique
Lorsqu'un objet possédant un `Rigidbody` et implémentant `Collectible` entre dans la zone :
1. Le script calcule le vecteur directionnel vers le point de convergence (`_nozzleTransform`).
2. Il applique une force continue (`_suctionForce`) en utilisant `ForceMode.Force`.
3. Cette force est divisée par la `PullResistance` de l'objet, permettant aux level designers de créer des objets légers mais difficiles à aspirer (ex: accrochés au sol).

### B. Effet Visuel d'Engouffrement (Shrinking)
Pour que les gros objets semblent passer dans la petite buse de l'aspirateur sans clipper brutalement :
* **Zone de Réduction (`_shrinkStartDistance`)** : Dès que l'objet franchit ce rayon, le script interpole linéairement son échelle locale de 100% vers 0%.
* **Zone d'Absorption (`_absorbDistance`)** : Lorsque l'objet atteint ce rayon critique, il est considéré comme "avalé" et l'événement d'absorption finale est déclenché vers le `PlayerVacuumController`.
* **Récupération de Sécurité** : Si le joueur relâche le bouton d'aspiration pendant qu'un objet est à moitié réduit, le script restaure immédiatement son échelle d'origine (`OriginalScale`) pour éviter qu'un objet minuscule ne retombe au sol.
