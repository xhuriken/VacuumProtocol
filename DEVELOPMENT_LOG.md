## [2026-08-23] SystËme de Taches de SaletÈ (Dirt Stains)
**Goal:** ImplÈmenter des taches de saletÈ statiques que les joueurs peuvent aspirer pour collecter de la donnÈe, avec dÈgradation visuelle et statistiques rÈseau.
**Changes:**
- **`DirtStain.cs` [NEW]**: Objet rÈseau reprÈsentant la tache. GËre la quantitÈ de saletÈ (MaxDirtAmount), le drainage cÙtÈ serveur et met ‡ jour son sprite en fonction du ratio d'usure via SyncVar.
- **`DirtMetricsManager.cs` [NEW]**: Manager centralisÈ gardant la trace du total de poussiËre de l'Èquipe et communiquant avec les clients (TargetRpc) pour sauvegarder leurs mÈtriques personnelles dans les PlayerPrefs.
- **`PlayerVacuumController.cs` [MODIFIED]**: Ajout des fonctions DrainDirt() et de la commande CmdDrainDirt() pour extraire les donnÈes d'une tache au lieu de la ramasser physiquement.
- **`VacuumSuctionZone.cs` [MODIFIED]**: DÈtecte dÈsormais les objets DirtStain dans le cÙne de vision et appelle la mÈthode de drainage continu (DirtDrainRatePerSecond) au lieu d'appliquer des forces de Vortex/Attraction.
**Justification:** SÈparation propre de la logique physique (cubes qui volent) et logique de donnÈe (taches qui se vident). L'aspirateur sert de routeur qui s'adapte au composant trouvÈ (Collectible ou DirtStain) et laisse le serveur centraliser les scores pour Èviter la triche et gÈrer la coopÈration sur la mÍme tache.

## [2026-08-23] Fix Calcul Longueur Bras (Bezier Explosion)
**Goal:** Corriger le bug o˘ la courbe de BÈzier explosait quand le bras droit visait vers le haut ou le bas aprËs avoir ajoutÈ des particules.
**Changes:**
- **`PlayerV2_Arms.cs` [MODIFIED]**: Mise ‡ jour de CalculateHierarchyLength() pour s'arrÍter explicitement ‡ l'os de la main (le dernier Rigidbody) au lieu de parcourir bÍtement tous les enfants jusqu'‡ la fin.
**Justification:** La mÈthode CalculateHierarchyLength parcourait rÈcursivement le premier enfant de chaque objet pour additionner la longueur de l'os. Comme le joueur a rajoutÈ le Particle System Force Field en enfant de la main droite (avec son offset positionnel long pour simuler l'aspirateur), le script comptait cet offset comme faisant partie de la longueur physique du bras ! Le bras droit devenait virtuellement gigantesque. Lors de la visÈe extrÍme (haut/bas), la courbe de BÈzier multipliait la courbure par cette longueur gigantesque, provoquant une explosion. La fonction est dÈsormais sÈcurisÈe pour ne compter que les vrais os de la hiÈrarchie physique.

## [2026-08-23] Fix Explosion Physique du Bras Droit
**Goal:** Corriger le bug o˘ le bras droit partait dans tous les sens et faisait bugger le torse.
**Changes:**
- **`PlayerV2_Arms.cs` [MODIFIED]**: Suppression du bloc d'initialisation dupliquÈ pour le bras droit dans la mÈthode Start().
**Justification:** Le bloc de recherche des joints Ètait prÈsent deux fois pour le bras droit. Le second bloc (incorrect) utilisait GetComponent au lieu de GetComponentInChildren pour trouver l'Èpaule, ce qui Ècrasait la variable _rightShoulderJoint avec une valeur 
ull. ConsÈquence : la rustine critique joint.connectedMassScale = 0.00001f n'Ètait jamais appliquÈe sur l'Èpaule droite. DËs que le bras droit forÁait pour viser, il repoussait le torse entier, provoquant un glitch physique incontrÙlable. En supprimant le bloc en trop, la bonne rÈfÈrence est conservÈe et la masse est correctement ignorÈe.

## [2026-08-23] Simplification HUD Crosshair
**Goal:** Supprimer le second crosshair (bras) qui est trop instable visuellement.
**Changes:**
- **`PlayerV2_DynamicCrosshair.cs` [MODIFIED]**: Suppression totale de la logique de suivi de la main rÈelle (HandActualCrosshair).
**Justification:** Le tracking du bras physique avec un crosshair flottant ajoute beaucoup de bruit visuel ‡ cause des ressorts physiques et de l'offset de rotation (qui peut toujours lÈgËrement fluctuer selon la position du torse et la limite des joints). Comme le joueur ne regarde que le centre de l'Ècran pour viser (comme dans 99% des jeux de tir/interaction), le second crosshair n'est pas nÈcessaire en terme de gameplay et pollue l'Ècran.

## [2026-08-23] Fix DÈrive du HUD Crosshair (Bras)
**Goal:** Corriger le fait que le disque du bras dÈrive sur le cÙtÈ (droite) au lieu de rester alignÈ devant la buse.
**Changes:**
- **`PlayerV2_DynamicCrosshair.cs` [MODIFIED]**: La direction visÈe par le bras (	rueAimDir) est dÈsormais calculÈe en inversant l'offset de rotation local de la main (HandRotationOffset).
**Justification:** Le modËle 3D du tentacule a ses axes locaux tournÈs de 90 degrÈs (paramÈtrÈ via HandRotationOffset). De ce fait, faire simplement leftHand.forward renvoyait la direction brute de l'os (le cÙtÈ de la buse), d'o˘ la fuite du crosshair sur la droite ! En annulant mathÈmatiquement cette rotation via Quaternion.Inverse, on obtient la vÈritable trajectoire du bout du bras.

## [2026-08-23] Correction des erreurs (Crosshair / Hand)
**Goal:** Fixer les erreurs de compilation liÈes ‡ LeftHand et amÈliorer le support de Shapes 3D.
**Changes:**
- **`PlayerV2_Arms.cs` [MODIFIED]**: Ajout des mÈthodes utilitaires GetLeftHand() et GetRightHand() et suppression de la rÈfÈrence directe dans le check IsHand.
- **`PlayerV2_DynamicCrosshair.cs` [MODIFIED]**: Utilisation de Transform standard au lieu de RectTransform. Ajout du toggle UseScreenSpace pour autoriser le positionnement en 3D World (pour un rendu avec des Shapes sans Canvas).
**Justification:** L'accËs ‡ LeftHand n'Ètait pas exposÈ publiquement dans le controlleur, entraÓnant des erreurs de compilation. L'implÈmentation a ÈtÈ revue pour s'adapter ‡ la volontÈ d'utiliser des composants vectoriels (Shapes) plutÙt que des images Canvas standard, en supportant le positionnement 3D direct face ‡ la camÈra.

## [2026-08-23] PrÈcision VisÈe Bras et Crosshair HUD
**Goal:** Permettre ‡ la main de s'aligner parfaitement avec la cible mÍme avec des angles extrÍmes (haut/bas) et ajouter un HUD in-game pour visualiser la diffÈrence entre la cible (camÈra) et la buse (bras).
**Changes:**
- **`PlayerV2_Arms.cs` [MODIFIED]**: DÈsactivation forcÈe du LockAngularX de l'articulation (ConfigurableJoint) **uniquement** pour la main (le dernier segment du bras).
- **`PlayerV2_DynamicCrosshair.cs` [ADDED]**: CrÈation d'un script HUD qui projette en temps rÈel la position ciblÈe par la camÈra et la position rÈellement visÈe par la buse sur l'Ècran.
**Justification:** Le manque de prÈcision de la main sur l'axe vertical (Pitch) venait du fait que le paramËtre LockAngularX verrouillait physiquement l'articulation de la main sur cet axe. La physique l'empÍchait donc de s'orienter parfaitement vers le haut ou le bas. En dÈverrouillant spÈcifiquement ce segment, la main peut se tordre librement pour Èpouser l'axe de la cible. Le nouveau script HUD permet au joueur d'avoir un retour visuel prÈcis de cette physique en jeu (‡ lier dans un Canvas avec des images).

## [2026-08-23] Synchronisation des Saccades des Yeux
**Goal:** EmpÍcher l'effet 'camÈlÈon' (les deux yeux qui bougent indÈpendamment dans des directions diffÈrentes) lors des saccades.
**Changes:**
- **`Eye.cs` [MODIFIED]**: Mise en place d'un systËme Maitre/Esclave au dÈmarrage (Start). Le premier oeil trouvÈ sur le joueur devient le maitre et calcule les saccades alÈatoires. L'autre oeil devient l'esclave et copie l'offset _currentSaccadeOffset du maitre au lieu de gÈnÈrer le sien.
**Justification:** Comme chaque script Eye.cs est indÈpendant et calcule ses propres valeurs alÈatoires Random.insideUnitCircle, les deux yeux pointaient inÈvitablement dans des directions diffÈrentes, donnant au robot un regard trËs Ètrange. Le design Maitre/Esclave permet aux deux composants de rester parfaitement parallËles et synchronisÈs sans nÈcessiter la crÈation d'un script gestionnaire centralisÈ lourd.

## [2026-08-23] Correction Direction Courbe Bras (Pitch vs Yaw)
**Goal:** EmpÍcher les bras de faire un arc de cercle sur les cÙtÈs (effet crabe) et forcer la courbe sur l'axe vertical (Pitch) quand le joueur vise en haut ou en bas.
**Changes:**
- **`PlayerV2_Arms.cs` [MODIFIED]**: La tangente de dÈpart (p1) de la courbe de BÈzier utilise dÈsormais _controller.TorsoRigidbody.transform.forward au lieu du orward de l'Èpaule.
**Justification:** Le modËle 3D du joueur a des Èpaules qui s'orientent physiquement ‡ 90 degrÈs vers l'extÈrieur (gauche/droite) quand le bras est sorti. En utilisant le vecteur *forward* de l'Èpaule pour initier la courbe, le bras partait vers l'extÈrieur avant de revenir vers le centre, crÈant une courbe horizontale trËs Ètrange. En utilisant le vecteur *forward* du Torse (qui est toujours parfaitement horizontal et pointe devant le joueur), on force la courbe ‡ dÈmarrer tout droit, puis ‡ s'arrondir doucement vers le haut ou vers le bas (sur l'axe du Pitch) pour rejoindre la cible visÈe par la camÈra.

## [2026-08-22] Refonte de l'Arc en Courbe de BÈzier
**Goal:** …viter que le premier segment du bras (‡ la base) ne prenne toute la rotation et forme un angle dur ‡ 90 degrÈs.
**Changes:**
- **`PlayerV2_Arms.cs` [MODIFIED]**: Remplacement de l'offset sinus sur un Lerp par une vraie courbe de BÈzier Cubique (Cubic Bezier).
**Justification:** L'ancienne mÈthode tirait le bras vers une ligne droite, ce qui forÁait la racine (connectÈe ‡ l'Èpaule horizontale) ‡ se tordre brutalement ‡ 90∞ si le joueur visait vers le haut ou le bas. En utilisant une courbe de BÈzier, on dÈfinit deux points de contrÙle (tangentes). Le premier force le dÈbut de la courbe ‡ pointer dans la continuitÈ de l'Èpaule (horizontal), et le second force la fin ‡ pointer vers la cible (dans l'axe du regard). Le bras s'enroule dÈsormais parfaitement sans aucune cassure ‡ la base, et le paramËtre ArcHeight agit comme la puissance de cette tangente (le 'mou' de la tentacule).

## [2026-08-22] Ajout Courbe Bras (Arc)
**Goal:** Rendre le bras plus arrondi et organique lorsqu'il est tendu, au lieu d'une ligne parfaitement droite.
**Changes:**
- **`PlayerV2_Arms.cs` [MODIFIED]**: Ajout du paramËtre ArcHeight dans l'inspecteur. Modification de ApplyArmPhysicsForces pour ajouter un offset basÈ sur Mathf.Sin sur la cible physique de chaque segment.
**Justification:** Au lieu d'interpoler linÈairement (Vector3.Lerp) les cibles physiques sur une ligne droite parfaite entre l'Èpaule et la main, on applique une sinusoÔde (Mathf.Sin). Le sinus fait 0 ‡ l'Èpaule, monte ‡ 1 au milieu du bras, et redescend ‡ 0 ‡ la main. En multipliant Áa par le vecteur *Up* (le haut de la camÈra) et par ArcHeight, on force la physique ‡ courber le bras vers le haut, crÈant un arc parabolique naturel de type 'tentacule'.

## [2026-08-22] Ajustement GroundCheck (SphereCast)
**Goal:** Corriger le bug du Raycast qui ratait le sol et faisait sautiller le joueur en boucle.
**Changes:**
- **`PlayerV2_Movement.cs` [MODIFIED]**: Remplacement du Physics.Raycast par un Physics.SphereCast.
**Justification:** Le Raycast (Èpaisseur 0) Ètait trop prÈcis. Si la roue reposait sur le sol mais que le contact physique repoussait trËs lÈgËrement le centre (ou sur une micro-pente), le rayon vertical ratait le sol d'un millimËtre. RÈsultat : le script croyait que le joueur Ètait en l'air, puis au sol, dÈclenchant le OnHardLanding en boucle (sautillement). Le SphereCast possËde une Èpaisseur (rayon rÈduit ‡ 50% de la roue). Il est assez fin pour ne jamais toucher les murs sur les cÙtÈs (Èvite le Wall Jump), mais assez Èpais pour garantir de toujours dÈtecter le sol sous la roue.

## [2026-08-21] Rollback Bras & Fix GroundCheck
**Goal:** Revenir ‡ la version prÈcÈdente de la physique des bras (sans le blocage Kinematic des Èpaules) et empÍcher le 'Wall Jump'.
**Changes:**
- **`PlayerV2_Arms.cs` [MODIFIED]**: Suppression complËte de la logique KinematicRootSegments, ApplyKinematicRoots() et EnforceKinematicRoots(). Les bras redeviennent 100% dynamiques et tirÈs par l'Èpaule.
- **`PlayerV2_Movement.cs` [MODIFIED]**: Remplacement de Physics.CheckSphere (qui dÈtectait les collisions sur les cÙtÈs, permettant de wall jump) par un Physics.Raycast strict orientÈ vers le bas (Vector3.down). Mise ‡ jour des Gizmos pour afficher une ligne avec un petit cercle au bout.
**Justification:** Le fix des bras Kinematic causait des dÈtachements indÈsirables en mouvement qui nÈcessiteraient une refonte de la hiÈrarchie. On annule l'expÈrience sur demande. Le Ground Check via Raycast garantit quant ‡ lui que seules les surfaces sous le joueur (sol) autorisent le saut.

## [2026-08-21] Fix DÈtachement Bras (4 mËtres) en Multi
**Goal:** Corriger le bug o˘ les bras se dÈtachent du corps et volent ‡ 4 mËtres lorsqu'ils sont utilisÈs en mouvement.
**Changes:**
- **`PlayerV2_Arms.cs` [MODIFIED]**: Les joints (ConfigurableJoints) de la base Kinematic ne sont plus dÈtruits.
- **`PlayerV2_Arms.cs` [MODIFIED]**: Ajout de EnforceKinematicRoots() appelÈ dans FixedUpdate pour imposer isKinematic = true chaque frame, et forÁage de weight = 0f pour interdire toute force AddForce sur ces os.
**Justification:** Le prÈcÈdent fix dÈtruisait les joints de la racine pour plus de puretÈ Kinematic. Le problËme, c'est que sur un jeu multi, des composants comme NetworkRigidbody ou le moteur physique peuvent accidentellement repasser le Rigidbody en dynamique (isKinematic = false) l'espace d'une frame. Sans joint pour les retenir, et recevant soudainement la force d'attraction du clic, les segments s'envolaient librement ‡ plusieurs mËtres ! En gardant les joints (comme filet de sÈcuritÈ) et en forÁant le blocage de la force et de l'Ètat Kinematic *‡ chaque frame*, le bug est dÈfinitivement ÈradiquÈ.

## [2026-08-21] Immobilisation de la Base des Bras (Tube)
**Goal:** EmpÍcher le dÈcalage (drift) de la base du bras lors des dÈplacements du joueur, tout en permettant au reste du bras de rester physique.
**Changes:**
- **`PlayerV2_Arms.cs` [MODIFIED]**: Ajout d'une variable KinematicRootSegments (par dÈfaut ‡ 2). Au Start(), le script rend les N premiers segments du bras (ex: Arm.Base.L et Arm.L) isKinematic = true et supprime leur ConfigurableJoint.
**Justification:** Un Rigidbody dynamique attachÈ par un joint physique a une inertie. Lors des dÈplacements du robot, l'inertie fait dÈvier la base de l'Èpaule ('dÈcalage du tube'). En rendant la base *Kinematic*, elle s'accroche indÈfectiblement ‡ l'animation de l'Èpaule parente. Les joints suivants (Arm.L.001, etc.) utilisent alors ce segment Kinematic comme point d'ancrage physique parfait, gardant la fluiditÈ de la tentacule sans jamais dÈcoller de l'Èpaule.

## [2026-08-21] Fix Vibration (Jitter) de l'extension des bras
**Goal:** …liminer la vibration chaotique de tous les segments lorsque le bras se tend.
**Changes:**
- **`PlayerV2_Arms.cs` [MODIFIED]**: Calcul d'un segmentTargetPos unique par segment via Vector3.Lerp(armRootPos, finalTargetPosition, weight).
**Justification:** PrÈcÈdemment, **tous** les segments du bras (poignet, coude, Èpaule) Ètaient attirÈs vers le **mÍme point final** (inalTargetPosition). Le moteur physique (PhysX) essayait de fusionner les os en un seul point (singularitÈ), ce qui violait violemment les contraintes de distance des ConfigurableJoints. Les ressorts des articulations repoussaient les os tandis que le script les ramenait au mÍme endroit, crÈant une boucle de vibration (jitter) incontrÙlable. En attirant chaque segment vers sa place ''naturelle'' sur la ligne (ex: coude ‡ 50% de la distance), la force d'attraction tombe parfaitement ‡ 0 une fois le bras tendu, rendant la courbe solide comme un roc.

## [2026-08-21] Refonte Physique Courbe & Saccades Bras
**Goal:** Rendre l'extension des bras plus fluide (PAF) et courber naturellement le bras pour Èviter les angles droits ‡ l'Èpaule.
**Changes:**
- **`PlayerV2_Arms.cs` [MODIFIED]**: La force d'attraction n'est plus appliquÈe uniquement sur la main (handRb), mais rÈpartie sur **tous** les Rigidbody du bras (de l'Èpaule jusqu'‡ la main).
- **`PlayerV2_Arms.cs` [MODIFIED]**: ImplÈmentation d'un systËme de poids linÈaire (weight) : 100% sur la main, dÈcroissant jusqu'‡ l'Èpaule.
- **`PlayerV2_Arms.cs` [MODIFIED]**: IntÈgration de la rÈtractation (Crouch) dans le calcul du poids. Les segments rÈtractÈs ont un poids de 0%, et la courbe se redistribue doucement sur les segments restants.
**Justification:** Avant, appliquer la force uniquement sur la main crÈait un effet de fouet (whip effect) responsable de la 'saccade en 2 temps' : la main partait vite, l'Èpaule/coude restait derriËre jusqu'‡ ce que les limites du joint soient percutÈes. En appliquant la force proportionnellement sur chaque segment, l'ensemble du bras se dÈplace instantanÈment et simultanÈment (PAF). De plus, en tirant le coude vers la cible finale, on le force ‡ s'Èlever au lieu de pendre horizontalement, transformant l'angle droit hachurÈ en une courbe physique fluide et naturelle (arc de catÈnaire).

# Development Log

## [2026-08-20] Post-Deletion Codebase Adaptation (V1 -> V2)
**Goal:** Fix compilation errors in global/gameplay scripts that were still referencing the deleted V1 Player scripts.
**Changes:**
- **`PlayerBoneBridge.cs` [MODIFIED]**: Replaced `WheelSuspensionController` with `PlayerV2_Suspension` to dynamically extract wheel joints instead of raw transforms.
- **`PlayerVacuumController.cs` [MODIFIED]**: Swapped the `PlayerArmsController` dependency for `PlayerV2_Arms`. It works perfectly as a 1:1 drop-in replacement since V2 exposes the exact same properties (`LeftHand`, `IsRightArmExtended`, etc.).
- **`MouthAnimator.cs` & `UniVoicePlayerAudio.cs` [MODIFIED]**: Changed peer ID network lookup to target `PlayerV2_Controller` instead of `PlayerController`. Removed redundant `else if` branches.
- **`PlayerV2_Movement.cs` & `PlayerV2_Look.cs` [MODIFIED]**: Replaced `_controller.TorsoRigidbody.transform.forward` with a purely mathematical `yaw` calculation (exposed from `PlayerV2_Look.cs`) to determine movement direction.
**Justification:** When a player extended an arm, the physics joint pulled the Torso, causing it to physically twist. Because movement was relative to the Torso's physical rotation in V2, this caused the player to veer left or right. Reverting to a math-based yaw calculation perfectly matches the old V1 behavior and completely eliminates the movement drift without requiring extreme mass scale hacks.

## [2026-08-20] Obsolete V1 Scripts Cleanup & API Fix
**Goal:** Remove old V1 scripts that are fully replaced by V2 and fix remaining API Updater prompts for Mirror deprecated network code.
**Changes:**
- **`PlayerV2_Arms.cs` [MODIFIED]**: Replaced `isLocalPlayer` with `isOwned` to fix the Unity "Script Updating Consent" popup caused by Mirror API conflicts.
- **`PlayerV2_CollisionManager.cs` [MODIFIED]**: Encapsulated the `ColliderGroupType` enum inside the class (`PlayerV2_CollisionManager.ColliderGroupType`) to bypass IDE OmniSharp cache bugs that falsely reported a global namespace collision even after the old file was deleted.
- **`PlayerArmsController.cs`, `PlayerCollisionManager.cs`, `PhysicalHeadController.cs`, `PlayerController.cs`, `PlayerJumpComponent.cs`, `PlayerLookComponent.cs`, `PlayerMovementComponent.cs`, `WheelSuspensionController.cs` [DELETED]**: Removed from the project entirely.
**Justification:** The V2 refactor made these core movement and collision scripts obsolete. Deleting them prevents namespace/API confusion and cleans up the codebase. The `isOwned` fix applies the exact same logic as `PlayerV2_Movement` to circumvent Unity's outdated UNET scanner.

## [2026-08-20] Fix ColliderGroupType Definition Conflict
**Goal:** Fix a compilation error where `ColliderGroupType` was defined twice in the global namespace after removing custom namespaces.
**Changes:**
- **`PlayerV2_CollisionManager.cs` [MODIFIED]**: Removed the duplicate definition of the `ColliderGroupType` enum. The script now shares the global enum defined in `PlayerCollisionManager.cs`.
**Justification:** The recent refactoring removed all custom namespaces, causing identical enums in `Player` and `PlayerV2` scripts to collide in the global namespace. Deleting the duplicate resolves the error while keeping both scripts functional.

## [2026-08-20] Global Namespace Refactoring & PhysicsMaterial Fix
**Goal:** Remove all custom namespaces (`VacuumProtocol...`) from the codebase and update Unity physics API naming.
**Changes:**
- **Codebase [MODIFIED]**: Stripped `namespace VacuumProtocol...` blocks and dedented all C# scripts inside `Assets/1_Scripts/`.
- **Codebase [MODIFIED]**: Removed all `using VacuumProtocol...;` statements.
- **Codebase [MODIFIED]**: Renamed `PhysicMaterial` to `PhysicsMaterial` globally to align with modern Unity API standards.
**Justification:** User requested a complete removal of namespaces to simplify script referencing. Reverted `PhysicMaterial` to `PhysicsMaterial` for correctness.

## [2026-08-20] PlayerV2 Arms Zero Friction Physics
**Goal:** Prevent the physical arms from snagging or dragging on walls and floors by explicitly removing all friction.
**Changes:**
- **`PlayerV2_Arms.cs` [MODIFIED]**: In `ConfigureArmJointsPhysics`, instantiated a `PhysicMaterial` with 0 static/dynamic friction and `Minimum` combine mode.
- **`PlayerV2_Arms.cs` [MODIFIED]**: Applied this new zero-friction material to all `Collider` components found within both arm hierarchies (`LeftArmRoot` and `RightArmRoot`).
**Justification:** When arms drag along surfaces, Unity's default friction can cause the physics solver to fight the arm-extension forces, leading to jitter and unwanted torque. Creating and applying a frictionless material dynamically at runtime ensures a perfectly smooth sliding behavior without requiring manual setup on every single collider in the Prefab.

## [2026-08-20] PlayerV2 Arms Retraction Offset
**Goal:** Allow the user to skip the first X joints when the arms retract during crouch to prevent the upper arm (shoulder connection) from clipping into the torso.
**Changes:**
- **`PlayerV2_Arms.cs` [MODIFIED]**: Added `RetractedSegmentsOffset` (default 1) parameter to skip the first `X` joints.
- **`PlayerV2_Arms.cs` [MODIFIED]**: Updated the `AnimateArmRetraction` loops to start at `startIndex` and end at `startIndex + count`.
**Justification:** Pulling the very first child joint (the upper arm) towards the shoulder pivot often looks unnatural and causes severe clipping with the torso. Adding an offset parameter allows developers to only retract the forearms and wrists while leaving the upper arms locked in place.

## [2026-08-20] PlayerV2 Arms Physics Explosion Fix
**Goal:** Fix physics glitch where the player instantly shoots backwards into the void upon spawning.
**Changes:**
- **`PlayerV2_Arms.cs` [MODIFIED]**: Changed `ConfigurableJoint.projectionDistance` to `0.1f` (10cm) and `projectionAngle` to `180f` (Unity defaults).
- **`PlayerV2_Arms.cs` [MODIFIED]**: Corrected `autoConfigureConnectedAnchor` initialization. Manually calculated the true T-Pose connected anchor using `InverseTransformPoint(worldAnchor)` before disabling the auto-configuration.
**Justification:** The script was applying a `projectionAngle` of `0.1f` degrees and `projectionDistance` of `0.01f`. In Unity Physics, if a joint violates these extremely tight constraints even slightly (which always happens on spawn due to initial gravity drops), the solver forcefully teleports the Rigidbodies back into position, generating infinite separation velocities (the classic "void explosion"). Additionally, disabling `autoConfigureConnectedAnchor` during `Start()` on a dynamically spawned prefab often caused the joint to latch onto `Vector3.zero`, pulling the arm colliders violently into the torso's center of mass.

## [2026-08-20] IEnumerator Generic Compilation Fix
**Goal:** Fix compilation error "Using the generic type 'IEnumerator<T>' requires 1 type arguments" in `PlayerV2_Arms.cs`.
**Changes:**
- **`PlayerV2_Arms.cs` [MODIFIED]**: Added `using System.Collections;` at the top of the file.
**Justification:** Unity Coroutines use the non-generic `IEnumerator` from `System.Collections`. Without this namespace imported, the compiler resolves `IEnumerator` to `System.Collections.Generic.IEnumerator<T>`, which requires a type parameter and causes a build failure.

## [2026-08-19] Eye Texture Button Dynamic Assignment Fix
**Goal:** Fix a bug where clicking any instantiated eye texture button would apply the incorrect texture or the default prefab texture, and allow clean default textures to be injected.
**Changes:**
- **`LobbyCustomizationUI.cs` [MODIFIED]**: Added `btnComp.onClick.RemoveAllListeners()` before assigning the dynamic texture to prevent any Inspector-assigned default Unity Events on the prefab from overwriting the dynamic choice.
- **`LobbyCustomizationUI.cs` [MODIFIED]**: Added a fallback for `UICustomButtonBase` in case the prefab uses a custom Shapes/vector UI component rather than the standard UGUI `Button`.
- **`LobbyCustomizationUI.cs` [MODIFIED]**: Implemented a local scope copy `Texture2D capturedTex = tex;` inside the `foreach` loop to guarantee C# closure safety.
- **`LobbyCustomizationUI.cs` [MODIFIED]**: Added `public Texture2D[] DefaultEyeTextures` to cleanly inject base textures (like Circle or Square) from the Inspector without needing manual persistent scene buttons. Extracted instantiation into `SpawnEyeButton(Texture2D tex)`.
**Justification:** Custom vector UI components inherit from `UICustomButtonBase` (not `UnityEngine.UI.Button`). By adding a fallback `GetComponent<UICustomButtonBase>()`, the script seamlessly supports both standard Unity Buttons and custom Shapes-based interactables.

## [2026-08-19] NetworkTextureTransfer (UGC Network Chunking)
**Goal:** Synchronize large User-Generated Content (like 1024x1024 painted custom eye textures) over the Mirror network without relying on external web APIs and without crashing the reliable packet queue.
**Changes:**
- **`NetworkTextureTransfer.cs` [NEW]**: A robust, universal script that cuts large files into 16KB chunks. Sends chunks safely with a 0.05s delay via `[Command]` and `[ClientRpc]`. Includes logic for Late-Joiners where the server caches the reconstructed byte array and sends it specifically via `[TargetRpc]` when requested.
- **`NetworkTextureTransfer.cs` [MODIFIED]**: Refactored to act as a pure network relay. Removed direct Renderer and Material manipulation fields. It now delegates all visual application directly to `PlayerCustomization.ApplyLocalEyeTexture()` to maintain a Single Source of Truth (SSOT).
- **`CustomEyeTextureManager.cs` [MODIFIED]**: Made `GetFolderPath()` public.
- **`LobbyCustomizationUI.cs` [MODIFIED]**: Updated `SetLocalEyeTexture` to reconstruct the exact `.png` disk path and save it to `PlayerPrefs.SetString("SelectedEyeTexture", path)` when a custom button is clicked. If a default texture is chosen, it clears the PlayerPrefs entry.
**Justification:** The previous implementation caused race conditions with `PlayerBoneBridge.cs` and `PlayerCustomization.cs` because both scripts were instantiating and assigning the `.materials` array independently. By routing the network bytes through `PlayerCustomization` (SSOT), it seamlessly updates the correct existing material instances regardless of the shader property (`_MainTex` vs `_BaseMap`).

## [2026-08-19] Head Bouncing (Boing-Boing) & Crouch Prep
**Goal:** Add a procedural Y-axis bounce effect to the player's head when moving/jumping, and prepare the neck joints for a crouch mechanic.
**Changes:**
- **`PlayerV2_Head.cs` [MODIFIED]**: Unlocked `yMotion` (set to `Limited`) on the `ConfigurableJoint`s.
- **`PlayerV2_Head.cs` [MODIFIED]**: Added `yDrive` (position spring) with configurable `YSpringForce`, `YSpringDamper`, and `YLimit` to create a natural physical bounce.
- **`PlayerV2_Head.cs` [MODIFIED]**: Added `SetHeadHeightOffset(float offsetY)` which distributes a target Y offset across all neck joints via `joint.targetPosition`.
**Justification:** By utilizing the existing `ConfigurableJoint` structure and simply unlocking the Y-axis with a linear spring, the physics engine automatically handles the bouncing inertia when jumping or landing, requiring zero manual animation code. `SetHeadHeightOffset` prepares the exact same spring system to seamlessly pull the head down during a crouch.

## [2026-08-19] Player Crouch & Sprint Implementation
**Goal:** Implement Crouch and Sprint mechanics linked to Unity's new Input System, affecting both movement speed and procedural head retraction.
**Changes:**
- **`PlayerInputHandler.cs` [MODIFIED]**: Added `OnCrouch` callback and `IsCrouching` property to bridge the Input System events.
- **`PlayerV2_Movement.cs` [MODIFIED]**: Introduced tweakable Inspector variables: `SprintSpeedMultiplier` (1.5x), `CrouchSpeedMultiplier` (0.5x), and `CrouchHeadOffset` (-0.3m).
- **`PlayerV2_Movement.cs` [MODIFIED]**: In `FixedUpdate`, dynamically applied the speed multipliers based on active inputs. In `Update`, added logic to call `HeadController.SetHeadHeightOffset` to physically retract the head when crouching.
**Justification:** Keeps input routing centralized while granting immediate, highly configurable game feel tweaking via the Inspector. The head lowering uses the existing physical spring system, ensuring extreme fluidity.

## [2026-08-19] Crouch Polish: Head Pitch Lock & Eye Camera Override
**Goal:** Prevent the physical head from pitching up/down when crouching, and force the eyes to look forward (track camera) instead of tracking entities while crouching.
**Changes:**
- **`PlayerV2_Look.cs` [MODIFIED]**: Forces `SetTargetPitch(0f)` on the `HeadController` when `IsCrouching` is true, keeping the physical head perfectly horizontal. The camera naturally compensates for 100% of the pitch rotation.
- **`Eye.cs` [MODIFIED]**: Grabs a reference to `PlayerInputHandler` via `GetComponentInParent()`. Bypasses the `HighestPriorityEntity` target logic when `IsCrouching` is true, seamlessly falling back to the virtual camera target while preserving the existing 100% pupil and 75% eye tracking weights.
**Justification:** Keeps the logic perfectly aligned with the SSOT philosophy. The camera movement remains completely unrestricted, but visually, the robot's head stays tucked in and its eyes lock forward, emphasizing a defensive or stealthy crouch posture.

## [2026-08-20] Arm Retraction during Crouch
**Goal:** Visually shorten the player's arms during crouch without breaking the physics of the hand joint.
**Changes:**
- **`PlayerV2_Arms.cs` [MODIFIED]**: Disabled `autoConfigureConnectedAnchor` for all arm joints at startup to lock and cache their default T-Pose world-relative anchors.
- **`PlayerV2_Arms.cs` [MODIFIED]**: Added a Coroutine `AnimateArmRetraction` that gracefully Lerps the `connectedAnchor` of the first X arm segments to `Vector3.zero`.
- **`PlayerV2_Movement.cs` [MODIFIED]**: Linked the Crouch input state to `ArmsController.SetArmRetraction()`.
**Justification:** By dynamically Lerping the connectedAnchor to zero, the physical segments of the arm fold instantly and perfectly into their parents. Because `PlayerV2_CollisionManager` disables self-collisions within the arm, this doesn't cause any physics explosions. The hands remain active and physics-driven, just attached to a virtually shorter arm.

## [2026-08-19] PlayerV2 Procedural Multiplayer Visuals (Head & Eye Sync)
**Goal:** Synchronize procedural physics/visuals (head pitch, eye tracking) across the network without relying on heavy `NetworkTransform` components.
**Changes:**
- **`PlayerV2_Head.cs` [MODIFIED]**: Promoted `CurrentTargetPitch` to a `[SyncVar]` with a `[Command]` and Hook. The local player predicts the pitch locally, while remote players receive the target pitch and apply it directly to the `ConfigurableJoint.targetRotation`. The local physics engine on each client handles the smooth `SlerpDrive` transition.
- **`Eye.cs` [MODIFIED]**: Promoted `_targetLocalRotation` and `_pupilTargetLocalRotation` to `[SyncVar]`. Only the local player runs the target tracking calculations (`CalculateTargetRotation`), while all clients run the `ApplyRotation` Slerp.
**Justification:** This guarantees 100% smooth, perfectly interpolated procedural animations for the head and eyes on all clients without the enormous bandwidth cost of placing NetworkTransforms on small bones.

## [2026-08-19] PlayerV2 Multiplayer Bugfixes (Camera, Input, Physics)
**Goal:** Fix severe multiplayer instantiation issues on `PlayerV2` where multiple clients shared global states (cameras, inputs) and suffered from physics explosions when spawning.
**Changes:**
- **`PlayerV2_Controller.cs` [MODIFIED]**: Added `isOwned` checks in `Start()` to automatically disable the `Camera` and `AudioListener` components for remote players. This prevents the "2 audio listeners" warning and stops the host's view from being hijacked by joining clients.
- **`PlayerInputHandler.cs` [MODIFIED]**: Added `isOwned` check in `Start()` to disable the `UnityEngine.InputSystem.PlayerInput` component for remote players. This guarantees a player's hardware inputs only drive their own local avatar.
- **`PlayerV2_Head.cs` [MODIFIED]**: Removed the `!isOwned` return check from `Start()`. 
**Justification:** The most critical bug (host's head detaching when player 2 joins) was caused by the remote player bypassing joint configuration. Without springs, limits, or collision exemptions, the remote player's ragdoll head would violently explode upon spawn, physics-colliding with the host. By forcing physical joint configuration for *all* instances (local and remote) while only restricting *input driving* to the local player, the multiplayer stability is fully restored.
## [2026-08-19] Dynamic Map Spawn Manager
**Goal:** Implement a dynamic player spawn system in the game map to easily configure up to 10 spawn points with positions and orientations.
**Changes:**
- **`MapSpawnManager.cs` [NEW]**: A singleton script placed in the game scene that holds a list of `Transform` spawn points and circularly returns the next available point. It automatically registers its points to `Mirror.NetworkManager.RegisterStartPosition()` in `Awake()`, making it instantly compatible with the default NetworkManager HUD used for local testing. It now strictly throws a `Debug.LogError` if no points are configured.
- **`MyNetworkManager.cs` [MODIFIED]**: Updated `OnServerAddPlayer` to check for `MapSpawnManager.Instance` when spawning the in-game player (`sceneName != "Lobby"`). It now dynamically fetches the position and rotation from the manager. Enforces strict checks by throwing a `Debug.LogError` instead of silently falling back to `Vector3.zero` if the manager or points are missing.
**Justification:** Mirror's default spawning system is rigid. This custom `MapSpawnManager` provides a KISS approach allowing designers to visually drop empty GameObjects in the scene. By registering native Mirror start positions, it supports both the custom Steam lobby workflow and standard local NetworkManager tests perfectly. Following the newly created strict "No Silent Fallbacks" global rule, it prevents silent bugs by shouting loudly if spawn setup is forgotten.

## [2026-08-15] Custom Eye Textures & Lobby UI Tabs
**Goal:** Restructure the Lobby UI into Tabs (Appearance, Drawing, Audio) and allow players to save hand-drawn textures to apply them specifically to the character's eye material (M_Iris).
**Changes:**
- **`CustomizationMenuTabs.cs` [NEW]**: Simple UI tab controller logic to toggle between different customization panels.
- **`CustomEyeTextureManager.cs` [NEW]**: Static helper for Disk I/O. Saves `Texture2D` as PNGs to `%AppData%/VacuumProtocol/CustomEyes/` and loads them.
- **`LobbyCustomizationUI.cs` [MODIFIED]**: Added logic to listen to `TextureEditorPanelUI.OnTextureSaved`. Instantiates buttons dynamically for every loaded eye texture, applying them to the Dummy locally.
- **`PlayerCustomization.cs` [MODIFIED]**: Upgraded `_instancedMaterial` to an array `_instancedMaterials` to clone all materials on the SkinnedMeshRenderer. Added `ApplyLocalEyeTexture()` targeting `_eyeMaterialIndex` (Index 2 : M_Iris).
**Justification:** Prepares the groundwork for advanced personalization. Saving textures locally as PNGs decoupled the painting logic from the network limitation. A dedicated Mirror `[Command]` will be needed in the future to chunk and broadcast these raw bytes to other clients in multiplayer.
## [2026-08-15] Player Customization Save Fix (Lobby Dummy)
**Goal:** Ensure the offline Lobby Dummy automatically loads the player's saved visual and audio customizations on startup.
**Changes:**
- **`PlayerCustomization.cs` [MODIFIED]**: Added a `Start()` callback specifically to check `if (IsLobbyDummy)`. If true, it loads the saved `PlayerVisualIndex` and `PlayerNoteIndex` from `PlayerPrefs` and applies them instantly.
**Justification:** Mirror network scripts typically block `OnStartLocalPlayer()` for offline dummies to avoid network errors. Adding an explicit local loading routine in `Start()` ensures the player sees their correct saved skin in the main menu as soon as the game launches.
## [2026-08-15] Player Customization Presets & Dynamic Lobby Player Count
**Goal:** Replace the basic hex color customization system with curated visual presets containing both a UI color and a character `BaseMap` texture. Also add a dynamic "Current/Max" player count text to the lobby UI.
**Changes:**
- **`PlayerCustomization.cs` [MODIFIED]**:
  - Replaced `PlayerColor` with `PlayerVisualIndex` (`[SyncVar]`).
  - Added a `PlayerVisualPreset` struct (contains `PresetName`, `BaseColor`, `BaseMap`).
  - Updated `ApplyVisuals()` to map both the color and the texture directly to the `_instancedMaterial` (supporting Standard/URP shader naming).
- **`LobbyCustomizationUI.cs` [MODIFIED]**:
  - Changed `SetPlayerColor` to `SetPlayerVisualPreset(int presetIndex)`. It now saves `PlayerVisualIndex` to PlayerPrefs and requests the change via the server.
- **`LobbyController.cs` [MODIFIED]**:
  - Added `PlayerCountText` reference.
  - Implemented `UpdatePlayerCountText()` that reads `Manager.GamePlayers.Count` and `Manager.maxConnections` to display dynamic UI text `(e.g., 1/5)`.
**Justification:** Moving to presets rather than raw Hex colors allows full-fledged character material customization (applying different robot skin textures or decals) instead of just single-color tints. The player count logic ensures the lobby properly communicates real server availability without hardcoding values.## [2026-08-14] V2 Voice & Mouth Synchronization
**Goal:** Restore UniVoice VoIP 3D spatialization and `MouthAnimator` support for the new Player_V2 architecture.
**Changes:**
- **`PlayerV2_Controller.cs` [MODIFIED]**: Added `[SyncVar] public int ConnectionId = -1` to act as the network peer identifier for UniVoice.
- **`MyNetworkManager.cs` [MODIFIED]**: Updated `OnServerAddPlayer` to inject the Steam/Mirror `connectionId` into the `PlayerV2_Controller` on spawn.
- **`UniVoicePlayerAudio.cs` & `MouthAnimator.cs` [MODIFIED]**: 
  - Updated `TryFindPeerId` / `OnStartClient` logic to recursively search for `PlayerV2_Controller` to fetch the `ConnectionId`.
  - Fixed 3D Spatialization for V2: The `UniVoicePlayerAudio` now correctly tracks the physical `HipsRigidbody` instead of the static prefab root, ensuring voices always emanate from the moving avatar.
  - Added a `_inputHandler` (`PlayerInputHandler`) field to `MouthAnimator` to read the `IsVacuuming` state directly from the generic input instead of relying on the deprecated V1 `PlayerVacuumController`.
  - Corrected `MouthAnimator` baseline scaling: Set `_minScale` to `(0,0,0)` and `_maxScale` to `(2,2,2)`. A target scale of 50% now perfectly resolves to `(1,1,1)`, allowing the 3D mouth bones to start at their exact original size.
- **`Eye.cs` [MODIFIED]**:
  - Changed the default camera tracking mode (when no explicit target is in view). Instead of projecting a point 50m ahead and using `Quaternion.LookRotation` with world `Vector3.up` (which caused twisting and incorrect pitch offsets), it now directly targets `Camera.main.transform.rotation`. This perfectly compensates for the head's pitch multiplier, forcing the eyes to look completely down even if the head physically can't.
**Justification:** The new V2 multi-body architecture doesn't use the monolithic V1 `PlayerController`, breaking the peer ID lookup. Adding the ID to the V2 hub and adapting the audio scripts allows seamless fallback compatibility. By listening to the `PlayerInputHandler` instead of the Vacuum controller, the mouth animation reacts to the "2-clicks" input instantly without needing the vacuum physics to be ported yet.## [2026-08-14] Jump Physics Overhaul & Asynchronous Wheel Retraction Animation
**Goal:** Fix the non-intuitive mass-dependent jump force, implement heavier "Hollow Knight" style fall gravity, and add a juicy asynchronous retraction animation to the wheels when jumping.
**Changes:**
- **`PlayerV2_Movement.cs` [MODIFIED]**:
  - Refactored `JumpForce` application: Now subtracts existing Y velocity before applying `ForceMode.VelocityChange`. This prevents players from exploiting the suspension bounce to jump exponentially higher, ensuring a consistent max jump height.
  - Added hard-landing detection tracking previous frame's `currentVelocity.y` to trigger shock absorption upon impact.
- **`PlayerV2_Suspension.cs` [MODIFIED]**:
  - Implemented dynamic `OnHardLanding` damper increase. Temporarily multiplies the wheel springs' `Damper` by 5 upon heavy impact, rapidly dissipating the stored kinetic energy and removing the bouncy landing effect while preserving normal suspension bounciness during regular locomotion.
  - Added `TriggerJumpRetraction()` method calling an `IEnumerator AnimateWheelRetraction(joint)`.
  - On jump, each wheel waits a tiny random delay (`Random.Range(0.01f, MaxRandomRetractionDelay)`) before suddenly snapping up its ConfigurableJoint `targetPosition` to `RetractedExtension` (pulling the spring up).
  - After a brief hold, the spring lerps smoothly back down to its standard `TargetExtension`.
- **`PlayerV2_Movement.cs` [MODIFIED]**:
  - Replaced `ForceMode.Impulse` with `ForceMode.VelocityChange` for the `JumpForce`. This ignores the Rigidbody's mass entirely, allowing intuitive and predictable values (e.g., 8 instead of 500).
  - Implemented `FallGravityMultiplier` (default 3x). In `FixedUpdate()`, if vertical velocity is negative (falling), additional downward acceleration is applied to make the character feel heavier and snap faster to the ground.
**Justification:** Using `VelocityChange` provides designer-friendly values without having to calculate $F = m \cdot \Delta v$. The "Hollow Knight" gravity multiplier is an industry-standard trick to ensure jumps feel responsive on the way up but snappy and weighty on the way down. The asynchronous wheel retraction utilizes Unity's physics springs dynamically mid-air, creating a highly polished, organic "cartoony" leap animation without needing a dedicated Animator.
## [2026-08-14] Eye tracking & Pupil Interpolation
**Goal:** Smooth out pupil movements with a dedicated speed setting and ensure eyes/pupils target the camera's center view when no specific target is found.
**Changes:**
- **`Eye.cs` [MODIFIED]**:
  - Added `_pupilRotationSpeed` to configure the speed of pupil tracking independently.
  - Calculated `_pupilTargetLocalRotation` and applied smooth `Quaternion.Slerp` in `ApplyRotation()`.
  - Added a fallback in `CalculateTargetRotation()` to fetch `Camera.main.transform` and target `position + forward * 50f` when no active entity target is detected by the `PlayerViewRange`.
**Justification:** Instantly snapping the pupil feels mechanical; separating its slerp speed adds a more organic, biological feel. Targeting the camera's center view natively gives a sense of focus, visually decoupling the head's physical delay from what the player is actually looking at.
## [2026-08-13] PlayerV2 Arms Physical System
**Goal:** Port the physical, procedural arm-reaching system from V1 to V2 (`PlayerV2_Arms`) and implement a Gizmo tool.
**Changes:**
- **`PlayerV2_Arms.cs` [NEW]**: Recreated the physics-based arm extension logic. 
  - Subscribes to `PlayerInputHandler` to trigger left/right extensions on clicks.
  - Dynamically computes arm max physical reach and assigns `ConfigurableJoint` spring settings.
  - Added `FreeHangAtRest` toggle. When retracted, the script no longer attempts to force the hands back into a strict T-pose, simply allowing them to dangle organically using standard gravity and joint stiffness.
- **`PlayerV2_Controller.cs` [MODIFIED]**: Added references (`LeftArmRoot`, `RightArmRoot`, `LeftShoulder`, `RightShoulder`, `ArmsController`) to act as the Single Source of Truth for hierarchy traversal.
- **`PlayerV2_Gizmos.cs` [NEW]**: Created an Odin-compatible Gizmos manager. Added toggles (`ShowArmsGizmos`, `ShowHeadGizmos`, `ShowSuspensionGizmos`) to visualize invisible physics data directly in the Scene view (e.g. arm root, target reach position, suspension raycasts).
**Justification:** Reusing the proven V1 mechanics ensures identical game feel while adopting the cleaner V2 architecture. Removing the forced rest-pose solves the T-pose jitter issue entirely, letting standard Unity physics handle the organic dangling state.

## [2026-08-13] PlayerV2 Physical Head/Neck (Torsion Spring)
**Goal:** Implement a physics-based, driven neck and head system (ragdoll but responsive to mouse pitch) where the base is fixed relative to the Torso but the rest is a physical spring.
**Changes:**
- **`PlayerV2_CollisionManager.cs` [NEW]**: Duplicated and adapted the old `PlayerCollisionManager.cs` to the V2 namespace. Added explicit support to ignore self-collisions within the `_neckColliders` group to completely prevent physics-induced stretching caused by neck bones repelling each other.
- **`PlayerV2_Head.cs` [NEW/MODIFIED]**: Created a script to handle an array of `ConfigurableJoint`s (`NeckJoints`). It automatically configures `slerpDrive` settings (spring, damper) on `Start()` to avoid manual setup. It exposes `SetTargetPitch(float)` to divide the target pitch evenly across all physical joints.
  - *Update:* Inverted pitch orientation for correct mouse Y-axis handling.
  - *Update:* Forced linear motions to `Locked`, enabled angular `Limited` motions with a configurable `JointAngleLimit` (default 30¬∞), and set `enableCollision = false` on joints. Increased default `SpringForce` (5000) and `SpringDamper` (500) for a more "muscular" and less wobbly feel.
- **`PlayerV2_Look.cs` [MODIFIED]**: Altered the `LateUpdate()` method to transmit the `_cameraPitch` to `HeadController.SetTargetPitch()`.
  - *Update:* Added `MaxTurnSpeed` to clamp the maximum degrees per second the torso (Yaw) and camera (Pitch) can turn. This prevents violent physics snaps when the mouse is flicked extremely fast, ensuring the physical head can smoothly keep up with the Torso's rotation.
- **`PlayerV2_Controller.cs` [MODIFIED]**: Added `HeadController` reference to centralize references.
**Justification:** By using `ConfigurableJoint.slerpDrive` with `RotationDriveMode.Slerp` and passing the divided pitch via `targetRotation`, the neck acts as a cohesive torsion spring. It follows the player's look inputs while remaining physically reactive to external forces and allowing the head to lag/wobble dynamically.

## [2026-08-12] PlayerV2 Suspension Extension & Jump Physics
**Goal:** Implement professional dynamic suspension that extends wheels mid-air, and add jump logic with reliable ground checking.
**Changes:**
- **Suspension Target Position (`PlayerV2_Suspension.cs`)**: Added `TargetExtension` and mapped it to `joint.targetPosition`. This configures the ConfigurableJoint's internal spring to actively push the wheels away from the chassis. When airborne, the wheels visually stretch down. Upon landing, the chassis weight compresses the spring to find a natural resting height.
- **Physics Impulse Jump (`PlayerV2_Movement.cs`)**: Implemented a physical jump by applying `ForceMode.Impulse` upwards on the `HipsRigidbody`. The multi-body physics handles the rest automatically: the Hips launch upward, the springs extend the wheels downward, and the wheels lift off the ground only when the joint `linearLimit` is reached.
- **Robust Ground Check**: Integrated a `Physics.SphereCast` firing downwards from the Hips. The cast distance ensures ground detection even when the suspension is heavily compressed or fully extended.
**Justification:** Pushing the joint `targetPosition` away from zero utilizes Unity's native physics springs correctly, completely decoupling the need for manual script-based wheel offset animations. Firing an impulse on the parent body cleanly achieves "jump takeoff lag" natively through the joint's constraints.

## [2026-08-12] PlayerV2 Wheels Steering Angle & Drift Fix
**Goal:** Fix wheels turning on themselves when stopped and ensure all wheels rotate uniformly regardless of their individual base orientations.
**Changes:**
- **Absolute Steering Authority**: Decoupled the Lerp from the wheel's actual `localEulerAngles.y`. The script now maintains a single internal `_currentSteeringAngle` and forcefully applies it. This prevents the ConfigurableJoint's Free AngularY axis from drifting due to physics micro-collisions when stopped.
- **Base Offset Independence**: Removed the global `_baseOffset`. Instead, cached each wheel's `_initialWheelY` in `Start()`. The `_currentSteeringAngle` is now additively applied to each wheel's initial Y rotation.
**Justification:** When `angularYMotion` is Free, Physics can impart tiny rotational forces. If the script reads the wheel's rotation to Lerp it, it creates a feedback loop causing drift. By keeping an isolated steering state and applying it as an offset to each wheel's initial rotation, all wheels stay perfectly synchronized and ignore physical jitter, solving the perpendicular orientation bug on the side wheels.

## [2026-08-12] PlayerV2 Wheels Refactoring (KISS)
**Goal:** Simplify wheel orientation logic after identifying the true source of rotation bugs (locked angularYMotion in PlayerV2_Suspension).
**Changes:**
- **KISS Refactoring**: Completely rewrote `Wheels.cs` (`WheelSteering`). Removed all complex gimbal lock workarounds, cached arrays, and velocity smoothing.
- **Distance-Based Delta**: Restored the simple delta position calculation (`movement = currentPosition - _lastPosition`).
- **Local Inverse Math**: Used `transform.InverseTransformDirection(movement)` and `Mathf.Atan2` to calculate the exact local angle relative to the parent Hips.
- **Direct Application**: Simply Lerped `localEulerAngles.y` directly without attempting to enforce cached X and Z axes.
**Justification:** The previous complex gimbal lock and rotation snapping fixes were unnecessary. The root cause of the wheels not turning properly was the physical suspension joint (`ConfigurableJoint.angularYMotion`) being accidentally locked. By removing the over-engineered workarounds, the code is robust, readable, and relies strictly on pure parent-relative math.

## [2026-08-12] PlayerV2 Movement Physics Bugfixes
**Goal:** Fix physics accumulation and rotation bugs on the Hips, solve infinite sliding, and correct movement velocity scaling.
**Changes:**
- Enforced `RigidbodyConstraints.FreezeRotation` in code on `HipsRigidbody` during `Start()` to prevent any ground friction from inducing torque and spinning the Hips indefinitely.
- Fixed infinite sliding: Now applies explicit velocity zeroing (`HipsRigidbody.velocity = new Vector3(0, y, 0)`) when braking velocity falls below a 0.1 threshold to counter the lack of physics material friction.
- Restructured acceleration logic: Separated `MoveSpeed` (target velocity limit), `Acceleration` (force to reach target), and `Deceleration` (braking force). Changed the movement application from instant `VelocityChange` to gradual `Acceleration` to prevent physics snapping that made movement feel uncontrollable.
- Re-assigned movement direction calculation to use `CameraTransform` (`Look`) instead of `TorsoRigidbody` so players move relative to where they are looking instead of where the torso is visually rotated.
- **API Updater False Positive Fix**: Replaced all occurrences of `isLocalPlayer` with `isOwned` in `PlayerV2_Movement.cs` and `PlayerV2_Look.cs`. Unity's API Updater incorrectly scans the text `isLocalPlayer` and mistakes Mirror scripts for deprecated UNET scripts, causing an endless loop of "Script Updating Consent" prompts whenever the file is edited externally.
**Justification:** Physics materials without friction cause rigidbodies to drift forever unless hard-stopped. Floating character bases will accumulate rolling torque from floor drag unless their rotation is explicitly frozen. The previous snappy movement using `VelocityChange` applied max speed instantly, bypassing normal physical acceleration curves. Changing `isLocalPlayer` to `isOwned` strictly bypasses Unity's broken UNET regex scanner while preserving identical Mirror networking authority logic.

## [2026-08-12] PlayerV2 Wheels Decoupling & Strict Checks
**Goal:** Fix wheel steering orientation issues introduced by the new physics architecture and enforce strict reference checks instead of silent fallbacks.
**Changes:**
- **Strict Architecture**: Removed the silent fallback in `PlayerV2_Movement.cs`. The script now strictly requires `CameraTransform` and throws a `Debug.LogError` if it is missing, rather than quietly guessing another transform.
- **Visual Wheel Decoupling**: Completely removed Rigidbody dependency (`linearVelocity`) from `Wheels.cs` (`WheelSteering`). The wheels now calculate their orientation strictly based on pure spatial position delta (`transform.position - _lastPosition`).
- **Animator Override (Wheel Jitter/Reset Fix)**: Moved wheel rotation logic to `LateUpdate()` and implemented internal state tracking (`_currentWheelY`). Previously, if an Animator or NetworkTransform reset the wheel's local rotation to default every frame, reading `wheel.localEulerAngles.y` would constantly start the Lerp from 0, causing violent trembling and making the wheel snap back when stopped. Now, the script ignores the overwritten physical angle, Lerps its own internal float, and brute-forces it onto the Transform.
- **Gimbal Lock Destruction Fix**: Cached initial local X and Z rotations (`_baseX`, `_baseZ`) in `Wheels.cs`. Previously, the script was reading `localEulerAngles.x` and writing it back while modifying `y`. When crossing 180 degrees backwards, Unity's internal Quaternion calculation would flip the Euler X and Z values to 180 (Gimbal lock). Writing these back permanently locked the wheel meshes upside-down and backwards, causing them to orbit sideways outside the robot's body due to their offset pivot.
**Justification:** Silent fallbacks create unpredictable long-term bugs because developers don't realize their configuration is broken. For the wheels, using physical velocity directly on an active suspension rig causes micro-stutters and orientation issues; position delta guarantees smooth, 100% accurate visual rotation independent of the underlying physics system (V1 or V2) or network sync states.

## [2026-08-12] PlayerV2 Movement Physics Bugfixes
- Planned the architecture: Hips (Base RB) and Torso (Turret RB) connected via a ConfigurableJoint with Free Y rotation.
- Created `PlayerV2` directory and basic script structure.
- Ignored Multiplayer SyncVars for now to focus purely on local physics stability.
**Justification:** The previous setup used a single root RB and tried to counter-rotate child transforms visually or physically, causing Unity physics to fight the Transform hierarchy, leading to jitter and joint failures. The Multi-Body approach delegates the "turret" logic entirely to the physics engine, ensuring 100% stability.
## [2026-08-06] - 4-Wheel Procedural Raycast Suspension Refactoring

### Feature Added / Refactoring
- **Dynamic Relative Wheel Baseline & Independent 4-Wheel Suspension (`WheelSuspensionController.cs`)**:
  - Captured each wheel's initial Editor local position (`_initialWheelLocalPos[i]`, e.g. `y = -0.07642244f`) on `Awake()` as the single-source-of-truth baseline offset.
  - Calculated independent downward extension distance (`-Y`) relative to each wheel's cached initial local Y position, ensuring exact model pivot preservation without hardcoded `0f` assumptions.
  - Refactored `RoutineJumpSuspensionSequence()` to maintain full downward extension in mid-air (`_maxSuspensionDistance`), providing a realistic leg-stretch visual effect during jump takeoff and airtime.

### Code Modified/Added
- **Modified `Assets/1_Scripts/Player/Movement/WheelSuspensionController.cs`**:
  - Retained `_initialWheelLocalPos` caching to capture prefab default local offsets (such as `y = -0.07642244f`).
  - Updated visual Lerp calculations in `AnimateVisualWheels()` to subtract extension from `baseLocalPos.y`.
  - Refactored jump sequence coroutine to stretch legs in mid-air and hold extension until ground landing.

### Technical Justification & Details
- **Pivot Integrity**: Hardcoding local Y to `0f` breaks 3D models whose wheel origins start at negative or custom offsets (e.g. `y = -0.07642244f`). Storing `baseLocalPos` ensures the suspension travel (`_maxSuspensionDistance` / `_restExtensionDistance`) is applied relative to the mesh's natural origin.
- **Airborne Leg Stretch**: Keeping wheels extended downward in mid-air gives immediate visual feedback of jump takeoff dynamics and prepares wheels for ground impact compression on landing.

## [2026-08-05] - Wheel Suspension Edit-Mode Gizmos Fix

### Feature Added / Bug Fix
- **Edit-Mode Suspension Gizmo Auto-Discovery (`WheelSuspensionController.cs`)**:
  - Implemented `GetCandidateWheelTransforms()` helper to dynamically discover wheel child transforms from `_wheelsRoot` when running in Edit Mode before `Awake()` populates runtime lists.
  - Added serialized `_drawGizmosOnlyWhenSelected` setting (default `true`) allowing developers to toggle between showing gizmos only when selected vs continuously in the Scene View during Edit Mode.

### Code Modified/Added
- **Modified `Assets/1_Scripts/Player/Movement/WheelSuspensionController.cs`**:
  - Added `_drawGizmosOnlyWhenSelected` serialized boolean field under `Debug & Gizmos`.
  - Added `GetCandidateWheelTransforms()` for Edit Mode transform querying.
  - Added `OnDrawGizmos()` callback to support continuous rendering when `_drawGizmosOnlyWhenSelected` is `false`.
  - Refactored `OnDrawGizmosSelected()` and extracted `DrawSuspensionGizmos()`.

### Technical Justification & Details
- **Root Cause**: `WheelSuspensionController` relies on `DiscoverAndSetupWheels()` executed during `Awake()` to populate `_wheelTransforms` at runtime. In Edit Mode (outside Play Mode), `Awake()` does not run, leaving `_wheelTransforms` empty (Count = 0) unless manually assigned in Inspector. In addition, Gizmo drawing was locked inside `OnDrawGizmosSelected()`, requiring the object to be actively selected.
- **Solution**: Extracted wheel candidate resolution logic so that in Edit Mode `_wheelsRoot.GetComponentsInChildren` / `foreach (Transform child in _wheelsRoot)` supplies target transforms without needing Play Mode execution.

## [2026-07-21] - Lobby Texture Editor Feature (Tomodachi Style)

### Feature Added
- **Core Texture Painting Engine (`TexturePainter.cs`)**: Non-UI drawing engine operating directly on raw `Color32[]` pixel buffers with dynamic texture dimensions ($W \times H$). Supports:
  - **Bresenham Interpolation**: Connects consecutive drag points to ensure smooth, gap-free strokes during fast mouse movements.
  - **Brush Tools**: Hard Pencil, Soft Brush (radial falloff gradient), Airbrush (stochastic spray), Eraser (background restore/clear), Flood Fill (BFS queue bucket fill), and Eyedropper (color sampler).
- **Snapshot History Engine (`TextureUndoSystem.cs`)**: Memory-efficient Undo/Redo stack manager storing pixel array snapshots with configurable step bounds.
- **SSOT Custom Cursor Integration (`CustomCursorFollower.cs`)**: Extended existing single-source-of-truth custom cursor follower with `SetBrushCursorMode()`. Canvas hover dynamically switches standard UI cursor graphics into a *Shapes* vector brush ring matching the active tool diameter, color, or eraser indicator.
- **UI Presenter (`TexturePainterUI.cs`)**: Receives Unity UGUI pointer events on canvas `RawImage`, transforms local RectTransform screen points to exact UV pixel coordinates, and updates SSOT cursor visual dimensions.
- **Lobby Studio Control Panel (`TextureEditorPanelUI.cs`)**: Integrates custom tool selection, project `ColorButtonUI` color buttons, `UICustomSlider` for brush size control, and Undo/Redo/Clear/Save action buttons.

### Code Modified/Added
- **Created `Assets/1_Scripts/UI/TextureEditor/Core/BrushData.cs`**: Defines `PainterTool` enum and `BrushSettings` container class.
- **Created `Assets/1_Scripts/UI/TextureEditor/Core/TextureUndoSystem.cs`**: Implements snapshot stacks for memory-friendly Undo/Redo operations.
- **Created `Assets/1_Scripts/UI/TextureEditor/Core/TexturePainter.cs`**: Implements core pixel drawing algorithms, Bresenham line rendering, and flood fill.
- **Implemented Opacity Blending on Flood Fill (`TexturePainter.cs`)**: Refactored `PerformFloodFill()` to support opacity blending using `Color32.Lerp(targetColor, fillColor, opacity)`. Now, bucket filling regions blends the new color cleanly with the target background color based on active opacity slider settings.
- **Dynamic Tool-Specific Slider Visibility (`TextureEditorPanelUI.cs`)**: Refactored `SetTool` to show/hide sliders contextually:
  - Pencil/SoftBrush/Eraser: Shows Size & Opacity.
  - Airbrush: Shows Size, Opacity & Density.
  - FloodFill: Shows Opacity (hides Size as it has no radius).
  - Eyedropper: Hides all sliders (locked to default single-pixel size).
- **Added Airbrush Spray Density Slider (`TextureEditorPanelUI.cs`)**: Created a dedicated `_brushDensitySlider` for the Airbrush tool. The slider is dynamically shown (`SetActive(true)`) only when the Airbrush tool is selected.
- **Implemented Tool-Specific Persistent Settings (`TextureEditorPanelUI.cs`, `UICustomSlider.cs`)**: Designed brush-specific memory and storage. Every brush now maintains independent settings for Size, Opacity, and Spray Density. When switching tools, the sliders automatically transition to their saved settings. To prevent UI lag, PlayerPrefs persistence is triggered only on pointer release (`onPointerUp` event added to `UICustomSlider.cs`).
- **Fixed Collapsed Slider Track Layout Timing Bug (`UICustomSlider.cs`)**: Added fallback handling to `UpdateVisuals()` in `UICustomSlider.cs`. If the UGUI layout pass hasn't completed on initialization, it forces canvas layout update or reads `sizeDelta.x` to prevent the track geometry from collapsing to zero width.
- **Added Brush Opacity Slider (`BrushData.cs`, `TextureEditorPanelUI.cs`)**: Expanded brush settings with a dynamic `Opacity` property (`0.0` to `1.0`) and mapped it to a new `_brushOpacitySlider` control in the editor panel.
- **Implemented Non-Accumulating Blending Mask (`TexturePainter.cs`)**: Introduced `_strokeStartBuffer` (Color32 texture snapshot) and `_strokeAlphaBuffer` (opacity coverage tracking layer) initialized at `BeginStroke`. During a single mouse drag stroke, stamp alphas are combined using `Mathf.Max` rather than additive accumulation. Prevents paint buildup on slow mouse speeds and ensures perfectly uniform opacity coverage regardless of drag speed.
- **Modified `Assets/1_Scripts/UI/Core/UICustomButtonBase.cs`**: Refactored `Interactable` setter to perform an instant physical hover check using `RectTransformUtility.RectangleContainsScreenPoint` and `MouseManager.Instance.MousePosition` when the button is re-enabled. Resolves EventSystem limitation where disabled buttons did not receive exit events.
- **Modified `Assets/1_Scripts/UI/Components/UICustomSimpleButton.cs`**: Removed `_buttonText.DOKill()` from `KillActiveTweens()`. Prevents hover exit transitions from instantly killing text color fade tweens, correcting the bug where button text remained greyed out.
- **Modified `Assets/1_Scripts/UI/Components/UICustomSimpleButton.cs` and `CustomTextButton.cs`**: Implemented hover state synchronization inside `AnimateInteractableTransition(true)`. When a button is re-enabled, it immediately evaluates the physical `IsHovered` check and transitions visual states to `AnimateHoverEnter` or `AnimateHoverExit` automatically.
- **Fixed Stroke Drawing Interruptions (`TexturePainterUI.cs`)**: Refactored `OnDrag` to check `!_painter.IsStrokeActive` and dynamically resume drawing via `BeginStroke` upon mouse drag re-entry into the canvas, resolving the issue where drawing broke if the mouse temporarily exited the canvas.
- **Modified `Assets/1_Scripts/UI/Components/UIColorsPalettes.cs`**: Added generic `OnColorSelected` and `OnHexColorSelected` UnityEvents to decouple color palette selection, allowing the same `UIColorsPalettes` component to drive TextureEditor, Player Customization, or any menu cleanly via Observer pattern.
- **Fixed Brush Cursor Precision Math (`TexturePainterUI.cs`)**: Calculated exact sub-pixel screen radius `uiRadius = (brush.Radius + 0.5f) * uiPixelSize`. Ensures 100.0% exact alignment between the custom cursor ring visual and painted pixels regardless of RawImage UI scaling or texture resolution.

### Technical Justification & Details
- **Responsive Layout Architecture**: Replaced arbitrary static pixel offsets with normalized anchor ranges and UGUI layout groups (`HorizontalLayoutGroup`, `VerticalLayoutGroup`, `GridLayoutGroup`). The drawing canvas enforces a 1:1 ratio using `AspectRatioFitter` so the painting surface stays square regardless of screen size.
- **TextMeshPro Integration**: All section headers and button labels utilize `TextMeshProUGUI` for ultra-crisp vector typography matching project design standards.
- **Auto-Aligning Button Containers**: Tools and color buttons are placed inside auto-wrapping grid containers with `GridLayoutGroup` and `ContentSizeFitter`, allowing endless tool and color button additions without breaking panel alignment.
- **Dynamic Resolution Flexibility**: Canvas resolution is specified dynamically at initialization (`InitializeCanvas(width, height)`) or on texture load (`LoadTexture(Texture2D)`). This allows painting small 64x64 pupil textures, 128x128 player avatar icons, or large player body UV maps with the exact same codebase.
- **SSOT Cursor Consistency**: Reuses the existing `CustomCursorFollower.cs` component rather than creating a secondary mouse follower script, ensuring single source of truth for mouse tracking and screen-space project alignment.
- **Non-Recursive BFS Flood Fill**: Avoids stack overflow exceptions on large texture fills by utilizing a `Queue<Vector2Int>` breadth-first algorithm.

### Accessibility/Visibility Signature Checks
- Microsoft CoreFX naming convention applied: explicit visibilities, Allman brackets, private `_camelCase` members.
- XML `/// <summary>` documentation added on all public classes, methods, and serialized fields.



### Feature Added
- **Unity Color Array Inspector Support**: Replaced hardcoded string colors with an editable `Color[]` array, making it extremely easy to tweak and preview colors directly within the Unity Inspector.
- **Odin Inspector `[Button]` Generator**: Added a customized Odin Inspector action button `GenerateQuantizedPalette()` that programmatically calculates a gorgeous 16-color quantized gradient.
- **Quantized Gradient Calculation (16-bit like)**: The generator calculates:
  - 3 Grayscale tones: Black, Medium Grey, and White.
  - 13 Rainbow hues: Evenly stepping through the HSV spectrum (Red, Orange, Yellow, Green, Cyan, Blue, Violet, Magenta, Pink).
- **Runtime Hex Syncing**: Dynamically converts chosen Unity Colors to HTML Hex values at runtime using `ColorUtility.ToHtmlStringRGB(buttonColor)` to maintain full synchronization compatibility with the multiplayer backend without altering any networking code.
- **Smooth DOTween UI Animations**: Attaches a custom micro-animation controller `UIColorsPaletteButtonAnimator` to each button, handling dynamic hovering, click scaling, and snappy bouncy pop feedback.

### Code Modified/Added

#### `Assets/1_Scripts/UI/UIColorsPalettes.cs`
- **Class `UIColorsPalettes`**: Implements dynamic Unity Color processing, Sirenix Odin Inspector integration, automated HSV-based color quantization, loop-variable capture safety, and runtime event generation.
- **Class `UIColorsPaletteButtonAnimator`**: Handles `IPointerEnterHandler`, `IPointerExitHandler`, `IPointerDownHandler`, `IPointerUpHandler`. Animates button scaling with `.SetUpdate(true)` to support responsive rendering even when `Time.timeScale` is paused in menus.

### Technical Justification & Details
- **Safety from loop variable capture**: Capturing the current loop index or item inside a delegate/lambda expression in C# leads to closure bugs if not assigned to a local variable within the scope of the iteration (`string capturedHex = hexColor;`).
- **Ease of Setup (KISS)**: Developers do not need to manually configure DOTween or animation components on 16 individual buttons in the Unity Inspector. The main controller automatically scans and applies script attachments dynamically in `Start()`.
- **Modern Unity APIs**: Uses `FindAnyObjectByType` instead of the deprecated `FindObjectOfType` to achieve optimal scene query performance.
- **Responsive Menu Rendering**: Configured all DOTween scaling with `.SetUpdate(true)` to guarantee smooth UI feedback regardless of lobby game state pause scales.

### Accessibility/Visibility Signature Checks
- All private/public member access levels are explicitly declared (`private string[] _hexColors`, `private void Start()`, etc.) to prevent compilation errors and comply with style standards.
- Script namespaces properly import `UnityEngine.UI`, `UnityEngine.EventSystems`, `DG.Tweening`, and `VacuumProtocol.Networking.Lobby`.

## [2026-05-26] - Architecture Guidance: Custom Shape Buttons in Unity UI

### Feature Added
- **Invisible Raycast Target Pattern**: Described the industry-standard architecture for interactive UI components containing non-standard graphics (e.g., custom Vector Shapes).
- **EventSystem Custom Handlers**: Outlined pointer interface implementations (`IPointerEnterHandler`, `IPointerExitHandler`, `IPointerDownHandler`, `IPointerUpHandler`, `IPointerClickHandler`) to drive sub-children animations without relying on standard `Button` visual transitions.

### Technical Justification & Details
  - Decoupled Visuals and Interactions: Standard `Button` components require a single `Image` as `targetGraphic` for transitions. By setting the `Button` transition to `None` and placing an invisible `Image` (alpha = 0) with `raycastTarget = true` on the parent, we decouple the collision/interaction area from the complex visual shapes underneath.
  - Custom Event Handlers: Implementing standard Unity UGUI event interfaces on custom scripts allows driving complex multi-child animations (scale, sub-positions, colors of nested custom shapes) using DOTween directly from pointer lifecycle callbacks.

## [2026-05-26] - Custom Vector Shape UI Toolkit (Freya Holm√©r Shapes)

### Feature Added
- **Base UI Pointer Toolkit (`UICustomButtonBase`)**: Extends standard MonoBehaviour and UGUI pointer interfaces (`IPointerEnterHandler`, `IPointerExitHandler`, `IPointerDownHandler`, `IPointerUpHandler`, `IPointerClickHandler`) to expose lifecycle hooks and Unity Events.
- **Global Magnetic Mouse Proximity Solver (`MouseManager`)**: A Canvas-level singleton helper script that computes screen-space distance from interactive UI elements to the mouse pointer, providing Snappy Quadratic Attenuation for magnetic attraction.
- **Lobby Color Button Custom Vector Controller (`ColorButtonUI`)**: Exposes dual `Shapes.Rectangle` properties (`Outline`, `Plain`) for Freya Holm√©r vector components.
- **Responsive Width/Height Morph Animations**: Performs dynamic DOTween property tweens (`DOTween.To`) targeting `Rectangle.Width` and `Rectangle.Height` on pointer enter, exit, down, and up states.
- **Dynamic Magnetic Attraction Offset**: Interpolates the local position of the inner plain shape relative to its cached original coordinate based on real-time mouse direction and proximity.

### Code Modified/Added
- **Created `Assets/1_Scripts/UI/UICustomButtonBase.cs`**: Handles fundamental pointer events and maps them to reusable `ButtonClickedEvent` UnityEvents.
- **Created `Assets/1_Scripts/UI/MouseManager.cs`**: Tracks mouse screen coordinates and offers robust vector proximity formulas.
- **Created `Assets/1_Scripts/UI/ColorButtonUI.cs`**: Subclasses `UICustomButtonBase` to animate outline bounds and plain shape offset translation.
- **Modified `Assets/1_Scripts/UI/UIColorsPalettes.cs`**: Swapped deprecated `UICustomShapeButton` arrays for unified `ColorButtonUI` references.

### Technical Justification & Details
- **Non-UGUI Graphic Compatibilities**: Custom Vector Shape tools like Freya Holm√©r's Shapes asset render via custom MeshRenderers and do not inherit from standard UGUI `Graphic`. This prevents standard UGUI buttons from controlling their properties directly. Custom scripts driving these properties are mandatory.
- **Property Tweening (`DOTween.To`)**: Because standard extension methods like `DOScale` target Transform parameters, custom vector properties like `Rectangle.Width` and `Rectangle.Height` must be tweened using explicit property setters to avoid stretching or pixelating shape bounds.
- **Magnetic Proximity Attenuation**: Calculated in screen space using `RectTransformUtility.WorldToScreenPoint` to ensure responsiveness across different canvas resolutions, aspect ratios, and scaling modes.
- **Spring Damped Interpolation**: Employs `Vector3.Lerp` with `Time.unscaledDeltaTime` to achieve visual spring dampening that works flawlessly during active pauses.

## [2026-05-26] - Fix Input System Compatibility & KISS Cleanups

### Feature Refactored
- **Unity New Input System Support**: Replaced references to deprecated legacy `UnityEngine.Input.mousePosition` with direct queries to the modern `UnityEngine.InputSystem.Mouse.current.position.ReadValue()` API. This resolves dynamic runtime `InvalidOperationException` errors when running under active Input System Package configurations.
- **Strict Separation of Concerns (KISS)**: Stripped procedural mathematical calculations (`CalculateMagneticPull`) out of the `MouseManager`. The global manager is now exclusively a simple, high-performance mouse coordinate reporter.
- **Localized Attraction Logic**: Moved all magnetic vector proximity queries and spring dampening logic directly inside the `ColorButtonUI` script's `Update()` loop. This encapsulates interactive visual mathematics locally on the buttons that consume them, simplifying architecture and avoiding global pollution.

### Code Modified/Added
- **Modified `Assets/1_Scripts/UI/MouseManager.cs`**: Simplified coordinates polling to leverage the `UnityEngine.InputSystem` assembly.
- **Modified `Assets/1_Scripts/UI/ColorButtonUI.cs`**: Handled screen distance calculations and spring offsets internally using the local component's parameters.

## [2026-05-27] - Inject Validation & Interactive Safety Nets

### Feature Added
- **Automated Raycast Safety Net**: UICustomButtonBase now checks for an active UGUI `Graphic` component on the GameObject. If missing or if `raycastTarget` is disabled, it dynamically adds an invisible transparent `Image` (alpha = 0, `raycastTarget = true`), ensuring UGUI pointer raycasts are correctly processed.
- **Critical Diagnostics Logging**: Added strategic `Debug.Log` statements inside pointer events (`OnPointerEnter`, `OnPointerExit`, `OnPointerDown`, `OnPointerUp`, `OnPointerClick`), Awake, and Start cycles to track exactly when and where a collision blockage occurs.
- **Scene Dependency Diagnostics**: Added explicit warning/error reporters in `Start()` to verify if `MouseManager` exists and if an `EventSystem` is missing in the scene hierarchy.
- **World Space Camera Projection Fix**: Replaced overlay-space `RectTransformUtility.WorldToScreenPoint(null, ...)` coordinate mapping with a dynamic camera-projected `Camera.main.WorldToScreenPoint(...)` conversion. This resolves coordinates-projection misalignment when buttons are rendered in the 3D/2D world space (using colliders and physics raycasters) rather than basic screen overlay.

## [2026-05-28] - Smooth Hover Snapping & Reset Integration

### Feature Added
- **Dynamic Proximity Hover Disabling**: Modified the magnetic pull calculation to automatically bypass vector tracking when `IsHovered == true`. The inner `Plain` shape smoothly slides back and snaps perfectly into its original parent local coordinate center using spring-dampened `Vector3.Lerp` interpolation when hovered, resuming interactive drift once the cursor leaves the element collision bounds.
- **KISS Math & Condition Cleanups**: Stripped redundant safety variables (`_magneticRadius > 0.001f`) since magnitude is naturally `>= 0` and standard comparisons organically bypass computation. Consolidated local variables to maximize readable, production-grade flow.

### Bug Fixes
- **Blit Script Compile Restores**: Resolved blocking compile errors in `Blit.cs` by commenting out non-standard decorative attributes (`[ShowIf]`, `[Indent]`). Removing these un-imported attributes immediately satisfies the C# compiler, restoring asset compilation and allowing `Blit` to register correctly as a ScriptableRendererFeature under the URP Forward+ settings inspector.

## [2026-05-28] - Custom Text Button Toolkit Expansion

### Feature Added
- **CustomTextButton Subclassing**: Created `CustomTextButton.cs` inheriting directly from the foundational `UICustomButtonBase` class. Exposed serialized fields for `LeftLine` (Shapes.Line), `Rect` (Shapes.Rectangle), `Dots` (GameObject), and the button text (`TextMeshProUGUI`) with robust XML documentation. Implemented clean override event hooks (`OnPointerEnter`, `OnPointerExit`, `OnPointerDown`, `OnPointerUp`, `OnPointerClick`) to provide clear visual animation placeholders for future DOTween sequences.
- **Holographic DOTween Animations**: Fully implemented premium, smooth custom vector animations inside `CustomTextButton.cs`. Designed an initial state where the Rect is invisible, LeftLine is visible, and Text is visible. Added hover tweens collapsing the LeftLine to zero and fading it out while the Rect fades in and slides 8 units to the right, and the Dots shift 20 units to the left. Relaunches the Febucci Text Animator typewriter dynamically on hover. Integrated a crisp punch scale and bright white scintillation shimmer sequence for pointer clicks. Integrated thorough `.DOKill()` active tween cancellation and automatic cleanups inside `OnDisable` to completely prevent execution overlaps or spam anomalies.

### Bug Fixes
- **SetKeepAlive Compilation Repair**: Resolved C# compilation error `CS1061` in `CustomTextButton.cs` by removing the invalid `.SetKeepAlive(true)` method call from the DOTween animation chain of `_dots.transform.DOLocalMove`. Active cancellation is fully covered by standard `.DOKill()` methods.
- **Visual Upgrades & Holographic Flicker Click**: 
  - **Leftward Rect Shift**: Corrected the translation of the `Rectangle` shape to slide to the LEFT (`-8f` units offset) instead of right on hover enter.
  - **TextMeshPro Leftward Translation**: Added caching and animation of the `TextMeshProUGUI` transform, moving it 20 units to the left matching the `Dots` translation seamlessly.
  - **Holographic Scintillation Flicker**: Redesigned the click scintillation visual effect from a boring fade into a gorgeous high-fidelity projection simulation: features a super-fast bright white bloom peak (0.04s), an instantaneous drop to complete transparency (0.07s), followed by a high-frequency flickering back-and-forth pattern settling smoothly into the normal hover color. Safe under spam-clicking due to instant sequence termination.
- **Visual Fine-tuning & Press Action Triggers**:
  - **Leftward Rect Translation (20 units)**: Enlarged the `Rectangle` offset to EXACTLY **-20 units** (`-20f`) on the X axis, ensuring a perfectly aligned translation along with the Text and the Dots.
  - **Rectangle DashOffset Morphing**: Added high-fidelity tweening to the `Rectangle.DashOffset` (dashed offset), morphing it from **0.3 to 0.2** on hover enter and restoring it to **0.3** on hover exit, creating a premium sliding effect inside the dashed line.
  - **Immediate Press Click (PointerDown)**: Mapped the visual holographic scintillation flicker to trigger immediately on **PointerDown** (press action) instead of PointerClick (release action) to deliver razor-sharp, instant tactile feedback.
  - **Spam Click Scale Stabilization**: Replaced `DOPunchScale` entirely with an explicit `DOScale` sequence integrated inside `_clickFlashSequence`. `DOPunchScale` has internal caching bugs in DOTween when interrupted/killed repeatedly, which caused compounding scaling bugs. Using explicit `DOScale` targets (`_originalRectLocalScale * 1.08f` then returning to `_originalRectLocalScale`) completely resolves any potential scale aggregation and guarantees the rectangle returns to its exact scale even under extreme click spam.
- **Dynamic Dots & Shockwave Animation (Discs)**:
  - **Dynamic Setup**: Automatically queries the main `Disc` on the `Dots` GameObject, and any child `Disc` components under it. Caches their default sizes, colors, and positions.
  - **Hover Enter (Playful Spin & Breathing)**: The parent `Dots` transform immediately starts a **continuous 360¬∞ rotation loop** (incremental Linear orbit) while the two child discs expand and start a **playful continuous breathing yoyo scale pulse (radii breathing between 1.35x and 1.6x)**. This creates a lively, high-tech orbital loader feel.
  - **Hover Exit**: Smoothly interpolates radii, positions, and colors back to their exact cached default states, while gently rotating the parent `Dots` transform back to 0¬∞ alignment.
  - **PointerDown Press (Snappy Shockwave Scintillation)**: Overhauled all durations for an ultra-fast, snappy impact:
    - **Rectangle scale explosion** spikes to **1.15x** in just **0.03s**, snapping back in **0.12s**.
    - **Color flash/bloom peak** triggers in **0.02s**, followed by a **0.04s** blackout and high-speed **0.015s** holographic flickers.
    - **Dots shockwave** expands child discs to a dramatic **2.2x** size and **0.22 units outward burst** in **0.03s**, snapping back to home in **0.12s**.
    - Complete, watertight protection against execution overlaps or scale aggregation.
- **Structural Code Cleanup & Organization**:
  - Organized the entirety of `CustomTextButton.cs` into clear, easy-to-read, standard `#region` blocks (`Serialized Fields`, `Private Fields`, `Properties`, `Unity Lifecycle Callbacks`, `EventSystem Overrides`, `Caching Helpers`, `Core Tween Animations`, `Cleanup & Safety Guards`). 
  - Retained every single feature, duration, transition, and security precaution with 100% functional parity.
- **Custom Button Interactable & Disabled Visual States**:
  - **Base Integration (`UICustomButtonBase.cs`)**: Added an `Interactable` property (backing field `_interactable`) and a virtual `OnInteractableChanged(bool isInteractable)` callback hook. Blocked all EventSystem inputs (PointerEnter, PointerExit, PointerDown, PointerUp, PointerClick) dynamically when `Interactable` is false.
  - **High-Fidelity Transitions (`CustomTextButton.cs`)**: Implemented `OnInteractableChanged` override. When disabled (`Interactable = false`), all active pointer tweens are killed and elements fade smoothly (in `0.25s`) to a gorgeous translucent grey look (text, dashed border, main/child discs). When re-enabled, they return dynamically to their respective cached idle configurations.
  - **LobbyController Migration (`LobbyController.cs`)**: Refactored the `StartGameButton` field from `UnityEngine.UI.Button` to `UICustomButtonBase`, adapting its ready check states to utilize the premium custom transition system via the `.Interactable` property.

## [2026-06-01] - One-Click URL Button Redirect

### Feature Added
- **One-Click URL Button Script (`OpenURLButton.cs`)**: Created a clean, robust, and highly reliable script designed to reside on a standard UGUI Button. It automatically binds to the button click event at Awake and runs the system browser redirect when clicked.
- **Auto-Registration & Dynamic Setup**: Requires a `Button` component, automatically caching and linking listeners at runtime. This avoids manual Inspector click binding, providing a foolproof "one-click" configuration experience.
- **Sanitized Redirects**: Performs robust string trimming (`_url.Trim()`) to remove leading/trailing carriage returns or spaces that frequently trigger system browser failure exceptions.
- **Manual Hook Support**: Exposes a clean public method `OpenConfiguredURL()` so the component can still be manually bound to standard Unity events or custom script sequences if needed.

### Code Modified/Added

#### `Assets/1_Scripts/UI/OpenURLButton.cs`
- **Class `OpenURLButton`**: Implements the automatic registration of listeners on a local standard `Button` component, sanitizes target URL values, triggers standard system browser execution, and implements strict memory logging / safety hooks.

### Technical Justification & Details
- **Foolproof Implementation (KISS)**: Designed for minimal configuration. Dropping the script onto a standard Button GameObject completely wires up the click handler with zero developer interaction needed.
- **Garbage Collection & Memory Safety**: Implements standard listener registration in `Awake` and automated un-registration inside the `OnDestroy` callback to guarantee no lingering listener reference leaks when scenes are reloaded or objects are destroyed.
- **Official API Redirects**: Utilizes `Application.OpenURL` to trigger system browser invocation. Detailed official references are available at [Unity Application.OpenURL Documentation](https://docs.unity3d.com/ScriptReference/Application.OpenURL.html).

### Accessibility/Visibility Signature Checks
- All property members and callbacks (`_url`, `_button`, `Awake()`, `OnDestroy()`, `OpenConfiguredURL()`, `HandleButtonClick()`) are explicitly declared with exact access visibility levels to satisfy compiler requirements and enforce strict standards.

## [2026-06-01] - Physical Multiplayer Arm Reaching (Procedural Joints)

### Feature Added
- **Multiplayer Arm Physical Reaching Component (`PlayerArmsController.cs`)**: Created a high-quality player controller script designed to manage physical joint-based arm movements. When the left or right click is held, the corresponding arm reaches out in the look direction of the player's head, reverting organically back to a relaxed/gravity state upon release.
- **Dynamic Hierarchy Traversal**: Automatically traverses child hierarchies of the Left and Right arm roots to locate the exact terminal node (hand or nozzle) of the physical chain.
- **Auto-Calculated Max Reach**: Measures the cumulative length of each arm segment dynamically, ensuring perfect world-space reach mapping that matches any robotic appendage structure without manual adjustments.
- **Physics Attraction (Forces & Torque)**: Computes dynamic spring-damping vector attraction forces (`AddForce`) and angular look-alignment torques (`AddTorque`) targeting the hand Rigidbody, achieving snappy, responsive, and organically stable reaching animations.
- **Mirror Multiplayer Synchronization**: Syncs inputs via `[SyncVar]` properties and client-to-server `[Command]` methods. Physics forces are simulated locally on every client for all players, providing seamless lag-free animations on remote clones.

### Code Modified/Added

#### `Assets/1_Scripts/Player/Controller/PlayerArmsController.cs`
- **Class `PlayerArmsController`**: Integrates with `PlayerInputHandler`, resolves target reach directions using localized looking components, dynamically traverses limbs to apply physics attractions to terminal nodes, and synchronizes status over Mirror.

### Technical Justification & Details
- **Procedural Joints Coexistence**: Leveraging Unity joints' native spring systems (`ProceduralTubePhysics`) allows remote/local clones to handle physical reactions (collisions, bending) automatically, making the rest-state collapse completely free and organic.
- **Mass-Relative Forces**: Multiplies computed forces and torques by target Rigidbody mass (`handRb.mass`) to guarantee identical, scale-invariant reaching responsiveness regardless of player avatar sizing.
- **Oscillation Mitigation (Damping)**: Implements precise damping coefficients for both linear and angular velocity curves to avoid high-frequency jitter during collision contact.

### Accessibility/Visibility Signature Checks
- Fully declared all access modifier levels (`private`, `public`, `protected`) across properties and methods to prevent compile blockages or reflection ambiguities in Mirror.

## [2026-06-03] - Vacuum Suction Physics, Object Shrinking, and Player Inventory

### Feature Added
- **Vacuum Physics Suction Field (`VacuumSuctionZone.cs`)**: Created a trigger volume component placed on the Right Hand that applies progressive target attraction forces to any physics-enabled Rigidbody marked with the new component.
- **Dynamic Proportional Shrinking**: Shrinks items in scale dynamically as they get closer to the nozzle tip using distance interpolation ratios. Restores the object's scale automatically if it escapes the field or if the vacuum is deactivated.
- **Networked Player Inventory (`PlayerInventory.cs`)**: Added storage capacity tracking for absorbed items on the server. Deactivates GameObjects upon absorption and keeps the item count synchronized to clients via `[SyncVar]`.
- **LIFO Spit/Launch Mechanics**: Allows spitting stored inventory items forward from the Left Hand tip nozzle (initial left click press). Restores their original scale and applies a strong physical impulse force to the object's Rigidbody.
- **Unified Controller Orchestration (`PlayerVacuumController.cs`)**: Integrates inventory, arm extensions, and trigger zone activation under Mirror networking, with instant local-client deactivation for latency-free item pickup.

### Code Modified/Added

#### `Assets/1_Scripts/Player/Controller/PlayerVacuumController.cs`
- **Class `PlayerVacuumController`**: Rewritten to manage the Right-Hand trigger zone activation, monitor left click for projectile spits, and host `CmdAbsorbObject` / `CmdSpitItem` Commands.

#### `Assets/1_Scripts/Player/Controller/PlayerInventory.cs`
- **Class `PlayerInventory`**: New class maintaining LIFO list of GameObjects on the server and synchronizing item count to clients.

#### `Assets/1_Scripts/Physics/VacuumSuctionZone.cs`
- **Class `VacuumSuctionZone`**: New class processing trigger overlaps, pull forces, visual shrinking, and notifying the controller of local absorption.

#### `Assets/1_Scripts/Physics/VacuumableObject.cs`
- **Class `VacuumableObject`**: New marker class containing original local scale and customizable resistance factors to suction force.

#### `Assets/1_Scripts/Player/Controller/PlayerArmsController.cs`
- Exposed public read-only properties for hands (`LeftHand`, `RightHand`) and extension states (`IsLeftArmExtended`, `IsRightArmExtended`).

### Technical Justification & Details
- **Trigger Stay Physics**: Leverages `OnTriggerStay` inside Unity's physics loop to apply forces and calculate relative distance scaling factors dynamically.
- **Visual Scale Restorations**: Prevents objects from permanently shrinking by tracking scale states in an active Dictionary and resetting them inside `OnTriggerExit` and `Update` (if vacuum is deactivated).
- **LIFO Stack Mechanics**: Re-spits the most recently vacuumed item, allowing natural gameplay shooting feedback.

### Accessibility/Visibility Signature Checks
- Fully verified explicit access modifiers and XML comments for all new methods and fields.

## [2026-06-08] - Merging VacuumableObject with Collectible

### Technical Justification & Details
- **Redundancy Reduction**: Rather than maintaining a separate `VacuumableObject` marker component alongside the `Collectible` component, all suction and physical parameters are merged directly into `Collectible`.
- **Inheritance and Requirements**: `Collectible` now implements `IEntity` and requires a `Rigidbody` component, which aligns with both physical simulation and player vision focus targeting systems.
- **Reference Updates**: References to `VacuumableObject` in `PlayerInventory` and `VacuumSuctionZone` have been migrated to `Collectible` to maintain full compilation consistency.

### Code Modified/Added

#### `Assets/1_Scripts/Gameplay/Collectible.cs`
- **Class `Collectible`**: Merged the physics caching, original local scale, pull resistance settings, and `ResetScale()` methods into the existing class structure. Added XML summaries to both properties and methods.

#### `Assets/1_Scripts/Player/Controller/PlayerInventory.cs`
- Updated the collection scale reset callback to query for the `Collectible` component.

#### `Assets/1_Scripts/Physics/VacuumSuctionZone.cs`
- Updated trigger stay lists, cache queries, and dictionary types to map `Collectible` components.

### File Deletions
- Deleted `Assets/1_Scripts/Physics/VacuumableObject.cs` and `Assets/1_Scripts/Physics/VacuumableObject.cs.meta`.

### Accessibility/Visibility Signature Checks
- Verified explicit access modifiers (`private`, `public`) and complete XML summaries on all newly introduced members within `Collectible`.

## [2026-06-08] - Fixing Arm Extension and Mouth Vacuum Input Logic

### Technical Justification & Details
- **Hierarchy Search Fix**: The procedural arm reaching physics searches for the hand GameObject using a recursive child traversal (`FindLastChild`). When a child without a Rigidbody (such as `VacuumSuctionZone`'s trigger collider) was added under the hand, the search returned the child collider rather than the parent hand itself. Since the child has no `Rigidbody`, joint forces could not be applied, causing the arm to remain static. Updated `FindLastChild` to stop and return the deepest node that contains a `Rigidbody` component, falling back to the deepest child only if no Rigidbody is found in descendants.
- **Mouth Vacuum Input Decoupling**: Restored the mouth animation/audio vacuum state to evaluate `_input.IsVacuuming` (which checks if both left and right mouse click inputs are active). This separates individual right-arm suction zone activations from mouth vacuum triggering.

### Code Modified/Added

#### `Assets/1_Scripts/Player/Controller/PlayerArmsController.cs`
- **`FindLastChild`**: Updated recursive algorithm to track and return the deepest child node containing a `Rigidbody` component.

#### `Assets/1_Scripts/Player/Controller/PlayerVacuumController.cs`
- **`Update`**: Decoupled vacuum state check from `_armsController.IsRightArmExtended` and restored it to check for `_input.IsVacuuming` (both keys pressed).

## [2026-06-08] - Spit and Mouth Vacuum Constraints

### Technical Justification & Details
- **Mouth Vacuum Override**: When both clicks are pressed (`IsVacuuming`), only the mouth should perform vacuuming audio/visuals. The arms should not extend. To achieve this, the local input checks in `PlayerArmsController.Update` force the target arm extensions (`leftInput` and `rightInput`) to `false` when `_input.IsVacuuming` is true, ensuring both arms remain at rest during the mouth vacuum.
- **Physical Extension Check for Spitting**: To improve shooting game feel, the projectile spit action must wait until the left arm is physically extended. Added `IsLeftHandExtendedPhysically` property to `PlayerArmsController` which calculates the distance from the left hand to the left arm root, requiring it to reach at least 80% of target extension.
- **Blocked State Timeout Fallback**: If the player is standing close to a wall, physical constraints might prevent the arm from straight-extending. Added a 0.25-second timeout fallback since left-click press: if the arm does not reach 80% physical extension within 0.25s, it spits anyway to prevent lockup.
- **Spit Force Reduction**: Lowered default `_spitForce` from 400f to 15f in `PlayerInventory.cs` to avoid extreme physics impulse launch velocities.

### Code Modified/Added

#### `Assets/1_Scripts/Player/Controller/PlayerArmsController.cs`
- **`IsLeftHandExtendedPhysically`**: Public property returning true if the left hand has physically reached at least 80% of its target extension length.
- **`Update`**: Forced arm extension inputs to false if `_input.IsVacuuming` is active.

#### `Assets/1_Scripts/Player/Controller/PlayerVacuumController.cs`
- **`Update`**: Re-implemented spitting to check for physical extension (`_armsController.IsLeftHandExtendedPhysically`) or a 0.25s timeout after press, and disabled spitting when mouth vacuum is active.

#### `Assets/1_Scripts/Player/Controller/PlayerInventory.cs`
- Lowered default `_spitForce` to 15f.

## [2026-06-10] - Simplifying Arm Targeting (KISS)

### Technical Justification & Details
- **Aim Simplification**: Reduced layout and reach complexity by removing lateral spread, divergence angles, and horizontal offsets when calculating target arm positions. Since the character only extends one arm at a time during gameplay actions, aiming is much more natural and intuitive when the extending arm targets the exact center line of where the player is looking.
- **KISS Philosophy**: Deleted `_horizontalSpread` and `_angleSpread` fields to keep inspector interfaces cleaner and reduce unnecessary math.

### Code Modified/Added

#### `Assets/1_Scripts/Player/Controller/PlayerArmsController.cs`
- **Fields**: Removed `_horizontalSpread` and `_angleSpread`.
- **`ApplyArmReachingForces`**: Simplified the target position and target rotation calculations to pull the hand directly to the center line of vision.

## [2026-06-11] - Sp√©cifications Techniques de la T√™te et de la Vision du Robot Vacuum

### Justification Technique & D√©tails
- **Analyse du Sch√©ma de Fonctionnement** : Traduction et enrichissement des sp√©cifications de mouvement et de vision du Robot Vacuum depuis les sch√©mas manuscrits.
- **Int√©gration Physique via ConfigurableJoint d'Unity** :
  - Utilisation d'un **Rigidbody** (t√™te) reli√© au buste par un **ConfigurableJoint** pour g√©rer nativement les collisions, chocs et forces d'inertie.
  - Configuration du `Slerp Drive` (ressorts de rotation) avec un faible amortissement (`Position Damper`) pour g√©n√©rer le balancement √©lastique ("boing boing") naturel lors des mouvements ou impacts physiques.
  - Configuration du `Y Drive` pour le d√©placement vertical (Crouch) g√©rant l'affaissement √©lastique.
- **Formulation Math√©matique de Guidage (targetRotation / targetPosition)** :
  - La souris pilote directement la **Cam√©ra Verte** (100% de la direction vis√©e).
  - La rotation de la t√™te suit √† amplitude r√©duite (70%) via l'assignation de la `targetRotation` du Joint.
  - La position de la t√™te suit un arc de cercle et un affaissement via l'assignation de la `targetPosition` du Joint (ajoutant l'offset d'accroupissement).
- **Vision Hi√©rarchique Cibl√©e (≈íil, Pupille)** :
  - L'≈íil Bleu s'aligne √† **75%** vers la cible et la Pupille Mauve s'aligne √† **100%**, produisant un effet visuel de regard en coin tr√®s expressif.

### Code Modifi√©/Added
- Cr√©ation du fichier de sp√©cifications : `documentation/Player/Head_and_Vision_Mechanics.md` avec description d√©taill√©e de la configuration du Joint et script C# d'impl√©mentation.
- Cr√©ation de `Assets/1_Scripts/Player/Controller/PhysicalHeadController.cs` : classe de gestion physique de la t√™te avec calcul d'arc de cercle, crouch, et liaison ConfigurableJoint.
- Mise √† jour de `PhysicalHeadController.cs` : impl√©mentation du d√©tachement hi√©rarchique au `Start()` via `transform.SetParent(null)` pour √©liminer les conflits de double contrainte avec les animations de l'armature parent, calcul de la rotation relative par rapport au parent d'origine, et destruction automatique lors de la destruction du joueur. Ajustement avec inversion (`Quaternion.Inverse` et `-desiredOffset`) pour `targetRotation` et `targetPosition` du ConfigurableJoint suite aux sp√©cifications internes d'Unity. Ajout de l'ignorance dynamique des collisions via `Physics.IgnoreCollision` au `Start()` entre le collider de la t√™te et tous les colliders du corps/bras du joueur pour √©viter tout blocage physique. Correction des signes de `targetPosition` (+arcY et -arcZ) pour appliquer la translation de l'arc de cercle dans le bon sens physique.

## [2026-07-01] - Local VAD Filter for Mouth Animator

### Technical Justification & Details
- **Raw Mic Noise Jitter Issue**: The local player's mouth animation previously subscribed directly to the raw microphone stream (`IAudioInput.OnFrameReady`), which triggered *before* any filters were run. Consequently, background white noise and breathing caused the local player's mouth to jitter even when they were silent. Conversely, remote players' mouths appeared perfectly clean because remote volumes are derived from the networked stream, which is gated by the client-side Voice Activity Detector (VAD) filter.
- **Adaptive VAD Synchronization**: Exposing the local `SimpleVad` instance as a static public property in `UniVoiceMirrorSetupSample` allows other scripts to access the local client's VAD state.
- **Local Volume Gating**: Updated `MouthAnimator.cs` to check `UniVoiceMirrorSetupSample.LocalVad` and force `_lastPeak = 0f` if `LocalVad.IsSpeaking` is false. This mirrors the remote network behavior on the local client, resulting in a clean local mouth animation that only activates when actual speech is detected.

### Code Modified/Added

#### `Assets/1_Scripts/Audio/UniVoiceMirrorSetupSample.cs`
- **`LocalVad`**: Added a public static property to hold the active local `SimpleVad` instance.
- **`SetupClientSession`**: Assigned `LocalVad` during VAD initialization before adding it to `ClientSession.InputFilters`.

#### `Assets/1_Scripts/Audio/MouthAnimator.cs`
- **`SetupLocalMicLogging`**: Updated the local microphone `OnFrameReady` handler to check if `LocalVad` is active, forcing the local peak volume to `0f` when the user is not speaking.

#### `documentation/Audio/Voice_System.md`
- **Documentation Update**: Completely rewrote the document to explain how the VAD (Voice Activity Detection) algorithm works (RMS energy, EMA background noise floor estimation, SNR thresholds, attack/release timers). Explicitly differentiated between what is built-in in Mirror/UniVoice (network messaging, mic capture, base VAD/Opus filters) and what we custom-coded as a bridge/pont (dynamic 3D spatialization, mouth volume animation, local VAD gating, Steamworks lobby hosts fix).

## [2026-07-01] - Settings Manager System

### Technical Justification & Details
- **Settings State Isolation**: Designed and implemented a robust, modular, and extensible settings manager pattern. Created `SettingsData` as a POCO class acting as the Single Source of Truth (SSOT). Handled dictionary serialization inside `SettingsData` by implementing `ISerializationCallbackReceiver` to bypass Unity `JsonUtility` serialization limitations.
- **Decoupled Consumer Pipeline**: Added `ISettingsConsumer` interface allowing game components (Voice, Input, Graphics) to dynamically observe Settings changes. `SettingsManager` manages loading/saving JSON configurations from/to PlayerPrefs and routes updates to all registered consumers.
- **Audio Hot-swapping & Sensibility Bridge**: Implemented `VoiceSettingsConsumer.cs` to capture settings changes. Resolves hot-swapping by stopping previous recording devices, starting recording on the new target device, and updating `ClientSession.Input` dynamically. Uses reflection to access `SimpleVad._config` and update SNR thresholds dynamically based on user sensitivity preferences. Calculates combined peer volumes (Master * Voice * PeerMultiplier) dynamically.
- **Input System Rebinding**: Implemented `InputSettingsConsumer.cs` to apply bindings overrides onto `InputActionAsset` at load, and provides methods to trigger interactive rebinding.
- **UI Presenter and Thread-Safe Indicator**: Implemented `SettingsUIPresenter.cs` to bind UI components (Sliders, Dropdowns) to the manager. Calculates frame RMS in-place on the microphone capture thread and uses a cached float field to safely apply level changes to the UI on Unity's main thread inside `Update()`.
- **Namespace Clean-Up**: Removed namespaces from all settings scripts (`SettingsData`, `ISettingsConsumer`, `SettingsManager`, `VoiceSettingsConsumer`, `InputSettingsConsumer`, and `SettingsUIPresenter`) to prevent compilation blocks, match the project conventions, and allow global references.


### Code Modified/Added

#### `Assets/1_Scripts/Core/Settings/SettingsData.cs` [NEW]
- Holds all volume, sensitivity, input overrides, and graphics quality index. Implements `ISerializationCallbackReceiver`.

#### `Assets/1_Scripts/Core/Settings/ISettingsConsumer.cs` [NEW]
- Interface declaring `OnSettingsUpdated(SettingsData)` method.

#### `Assets/1_Scripts/Core/Settings/SettingsManager.cs` [NEW]
- Central Singleton manager for lifecycle, persistence, and event dispatching. Fixed type conversion compiler error by querying `.Name` property on target `Mic.Device`.

#### `Assets/1_Scripts/Audio/VoiceSettingsConsumer.cs` [NEW]
- Bridges volume, microphone hot-swapping, and VAD sensitivity levels dynamically.

#### `Assets/1_Scripts/Player/Controller/InputSettingsConsumer.cs` [NEW]
- Handles control rebinding overrides with the new Unity Input System.

#### `Assets/1_Scripts/UI/SettingsUIPresenter.cs` [NEW]
- Presenter script to bind UI sliders/dropdowns and render live micro RMS levels with thread-safety.

#### `Assets/1_Scripts/Audio/UniVoiceMirrorSetupSample.cs`
- Removed namespace wrapper to put the class in the global namespace. Also added `using Adrenak.UniVoice;` to restore visibility of `IAudioServer`, `ClientSession`, and `SimpleVad` which are no longer automatically visible from parent namespace nesting after removing the namespace wrapper.

#### `Assets/1_Scripts/Audio/MouthAnimator.cs`
- Removed `using Adrenak.UniVoice.Samples;` reference.

#### `Assets/1_Scripts/Audio/UniVoicePlayerAudio.cs`
- Removed `using Adrenak.UniVoice.Samples;` reference.

#### `documentation/Gameplay/Settings_System.md` [NEW]
- Technical documentation detailing the modular, extensible Settings Manager system, core component responsibilities, VAD details, and Unity Editor setup steps. Added note explaining why `UniVoiceMirrorSetupSample` is locally copied.

## [2026-07-01] - Modular UI Page Navigation System

### Technical Justification & Details
- **Reusable Prefab Architecture**: Designed a decoupled UI workflow where individual menu panels (e.g. Settings Panel, Main Menu, Pause Menu) handle their own show/hide/animation logic and remain independent. This allows the Settings Panel to be turned into a Prefab and dropped into both Main Menu and In-Game Pause canvas hierarchies without duplication.
- **Visual Panel Transitions**: Implemented `UIPanelController.cs` which manages opacity fading (`DOFade`) and scale scaling (`DOScale` using `Ease.OutBack`/`Ease.InBack`) dynamically using DOTween. Sets `SetUpdate(true)` to guarantee animations play when the game is paused (timeScale = 0).
- **Navigation Groups**: Implemented `UINavigationGroup.cs` to orchestrate mutually exclusive panels and maintain history stack tracking for back button traversal.
- **Pause Menu Key Gating**: Implemented `InGameMenuController.cs` supporting both legacy `Input.GetKeyDown` and New Input System `Keyboard.current` to capture Escape key and toggle the pause panel. If settings are open, it closes them first.

### Code Modified/Added

#### `Assets/1_Scripts/UI/UIPanelController.cs` [NEW]
- Manages show/hide lifecycle, raycast blocking, and DOTween transitions for individual canvas group panels.

#### `Assets/1_Scripts/UI/UINavigationGroup.cs` [NEW]
- Coordinates active panel swapping in a group and handles back operations.

#### `Assets/1_Scripts/UI/InGameMenuController.cs` [NEW]
- Listens for Escape key to toggle pause menus and manage nested panel visibility.

#### `documentation/Gameplay/UI_Navigation_System.md` [NEW]
- Comprehensive design document detailing the modular UI layout, component roles, code structure, and guide for editor setup.

#### `Assets/1_Scripts/UI/SettingsUIPresenter.cs`
- Added offline fallback check to `UpdateVolumeIndicator()` comparing live RMS values directly against the threshold slider value when `LocalVad` is null (e.g. in the Main Menu), allowing test/preview of color changing thresholds offline.
- Added `SettingsManager.Instance.SaveToDisk()` call inside `OnDisable()` to safely persist changes when the UI panel is closed.
- Subscribed to `VoiceSettingsConsumer.OnMicInputSwapped` to automatically re-subscribe the RMS local mic frame analyzer callback (`OnLocalMicFrameReady`) onto the newly instantiated `ClientSession.Input` device, preventing the dynamic volume indicator level from locking or stopping after a hot-swap.

#### `Assets/1_Scripts/Core/Settings/SettingsManager.cs`
- Removed synchronous `PlayerPrefs.Save()` disk flush from the hot update path (`SaveSettings()`) to prevent I/O blocking lag during active UI slider dragging.
- Added `SaveToDisk()` method and hooked it to `OnApplicationQuit()` and `OnApplicationPause()` to safely write changes to disk on application cycle events.

#### `Assets/1_Scripts/Audio/VoiceSettingsConsumer.cs`
- Added caching system (`_lastAppliedDevice`, `_lastAppliedSensitivity`, `_lastAppliedMasterVolume`, `_lastAppliedVoiceVolume`) in `OnSettingsUpdated` to completely avoid invoking costly OS audio driver list checks (`Mic.AvailableDevices`) and VAD reflection changes during slider drag ticks.
- Added `Update()` monitoring loop to automatically detect when `UniVoiceMirrorSetupSample.ClientSession` is initialized, invalidating the local cache and applying the player's saved parameters to the active VoIP session dynamically.
- Declared and triggered public static event `OnMicInputSwapped` when hot-swapping `ClientSession.Input` with a new `UniMicInput` instance.

#### `Assets/1_Scripts/Player/Controller/InputSettingsConsumer.cs`
- Added caching field (`_lastAppliedBindingsJson`) to skip redundant JSON parsing and binding overrides instantiation during slider drag updates.

#### `Assets/1_Scripts/Audio/UniVoiceMirrorSetupSample.cs`
- Updated `SetupClientSession()` to initialize using the saved `ActiveMicrophoneDevice` name from `SettingsManager.Instance.CurrentSettings` instead of defaulting statically to the first microphone index in the system.

## [2026-07-02] - IDE Configuration & Autocomplete Repair

### Technical Justification & Details
- **Self-Reference DLL Bug in Project Generator**: Fixed a compiler-blocking bug in `ProjectGeneration.cs` where assemblies (such as `Mirror.Components.csproj`) were being configured to reference their own pre-compiled DLL binaries located under `Library/ScriptAssemblies/`. This self-reference caused duplicate type definitions at compilation, generating CS0121 ambiguity errors (e.g. `'PredictedSyncDataReadWrite.ReadPredictedSyncData' is ambiguous between ...`). These compilation failures broke the Roslyn Analyzer/LSP, blocking autocomplete for Unity-specific APIs globally. Fix applied by adding `assembly.name` to `referencedNames` immediately, ensuring the generator skips adding the assembly's own compiled output as a dependency.
- **Extension Conflict Resolution**: Cleaned up the IDE's extensions directory and updated `extensions.json` and `.obsolete` list. Removed `muhammad-sammy.csharp` (conflicts with DotRush), `zlorn.vstuc` (redundant debugger bridge), and `november.clover-unity` (redundant Unity integration), leaving `nromanov.dotrush` and `antigravity-unity` as the single unified C# and Unity support stack to prevent language server conflicts and performance issues.
- **Visual Studio Aesthetics Match**: Configured global user settings in `settings.json` to match the exact aesthetics of Visual Studio C#, setting the theme to `Visual Studio Dark` (`vs-dark`), font to `Consolas`, and enabling autocomplete, parameter hints, and enter-to-commit preferences.
- **Unity External Script Editor Distinction**: Modified [AntigravityScriptEditor.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Packages/com.antigravity.ide/Editor/AntigravityScriptEditor.cs) to dynamically differentiate between "Antigravity" and "Antigravity IDE" depending on their executable paths. This allows selecting the correct executable in the Unity Preferences dropdown list.
- **Clover Extension Restoration**: Re-installed `november.clover-unity` v1.0.5 in the IDE via CLI to restore the "1 meta reference", "Unity Script", and "Unity Serialized Field" CodeLens annotations.
- **Workspace-wide SDK Pinning**: Added a [global.json](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/global.json) file at the root of the workspace to force the use of stable `.NET 9` SDK (`9.0.315`), resolving MSBuild incompatibilities on preview .NET 10 systems.
- **DotRush .NET 9 Runtime Override**: Configured the `DotRush.runtimeconfig.json` of the re-installed DotRush version `26.6.179` to target `.NET 9` (TFM `net9.0`, runtime `9.0.17`), and registered it in `extensions.json` to bypass .NET 10 preview runtime MSBuild crash bugs.
- **Package.json Activation Cleanup**: Removed the wildcard `*` from the activationEvents array in the Unity extension package.json to eliminate performance warnings in the IDE problems list.

### Code Modified/Added

#### [MODIFY] [AntigravityScriptEditor.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Packages/com.antigravity.ide/Editor/AntigravityScriptEditor.cs)
- Replaced hardcoded `EditorName` references with path checks to dynamically return `"Antigravity IDE"` or `"Antigravity"`.

#### [MODIFY] [package.json (extension)](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Packages/com.antigravity.ide/antigravity-unity-extension~/package.json)
- Removed wildcard `*` activation entry.

#### [MODIFY] [DotRush.runtimeconfig.json](file:///c:/Users/celestin/.antigravity-ide/extensions/nromanov.dotrush-26.6.179-win32-x64/extension/bin/LanguageServer/DotRush.runtimeconfig.json)
- Override `tfm` to `"net9.0"` and `version` to `"9.0.17"`.

#### [NEW] [global.json](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/global.json)
- Configured to pin .NET SDK version to `9.0.315`.

### Environment Changes
- Patched DotRush 26.6.179, forcing its execution environment to the stable .NET 9 CLR runtime, and pinned the workspace to .NET 9 using `global.json` to resolve the MSBuild loading crash.

## [2026-07-02] - Local VAD Loopback & Teardown Cleanup

### Technical Justification & Details
- **Singleton Teardown Leak Fix**: Resolved Unity warning `Some objects were not cleaned up when closing the scene. (Did you spawn new GameObjects from OnDestroy?)`. In Unity, singletons accessed inside `OnDestroy()` or `OnDisable()` during scene teardown can inadvertently instantiate a new singleton GameObject if the singleton has already been destroyed. Added a `HasInstance` property to `SettingsManager.cs` and an `_isQuitting` safety flag in the `Instance` getter. Updated all settings consumers (`VoiceSettingsConsumer.cs`, `InputSettingsConsumer.cs`, `SettingsUIPresenter.cs`) to check `HasInstance` before trying to unregister or flush settings on destruction.
- **Local Microphone Loopback (Gated Preview)**: Implemented a Discord-style local microphone test toggle. Added `LocalLoopbackFilter` implementing `IAudioFilter` to intercept PCM frames directly from the microphone after VAD processing but before Concentus Opus compression. This allows players to hear their own voice gated by the threshold value. Added `_micTestToggle` in `SettingsUIPresenter.cs` to trigger the local loopback preview dynamically.

### Code Modified/Added

#### `Assets/1_Scripts/Core/Settings/SettingsManager.cs`
- Added `HasInstance` static property and `_isQuitting` check to the `Instance` getter to block GameObject spawning during teardown.

#### `Assets/1_Scripts/Audio/VoiceSettingsConsumer.cs`
- Changed `OnDestroy()` to check `SettingsManager.HasInstance`. Implemented the nested class `LocalLoopbackFilter` and the static methods `SetLocalLoopback`, `SetupLoopbackFilter`, and `TeardownLoopbackFilter` to inject loopback preview after VAD.
- Optimized VAD sensitivity mappings in `ApplyGateSensitivity()` from a wide 2..32 dB SNR range to a more precise 2..18 dB SNR range.
- Reduced the VAD release hangover timer (`ReleaseMs`) from 1000ms to 300ms, and `NoDropWindowMs` to 200ms to ensure highly responsive, snappy voice cuts.

#### `Assets/1_Scripts/Player/Controller/InputSettingsConsumer.cs`
- Changed `OnDestroy()` to check `SettingsManager.HasInstance` before unregistering.

#### `Assets/1_Scripts/UI/SettingsUIPresenter.cs`
- Added `_micTestToggle` field and bound it to toggle the local preview audio loopback. Changed `OnDisable()` to check `SettingsManager.HasInstance` and automatically disable local preview when closing.
- Implemented **Peak-Hold (Instant-Attack, Slow-Decay)** visual meter logic in `UpdateVolumeIndicator()`. If a new audio frame peak is higher than the smoothed value, the jauge jumps to it instantly instead of being slowed down by interpolation (Lerp). The meter then decays slowly.
- Aligned visual level indicator logarithmically to display the **live Signal-to-Noise Ratio (SNR) in dB** instead of raw linear RMS. It queries the active noise floor `_noiseRms` dynamically via reflection from `SimpleVad`, computes `20 * log10(signal / noise)`, and maps the result to the precise 2..18 dB SNR range of the sensitivity slider. This ensures the voice indicator crosses the slider handle to the exact pixel whenever the noise gate opens.

## [2026-07-02] - Per-Peer Volume Slider (Lobby)

### Technical Justification & Details
- **Feature Request**: Each player in the lobby should be able to independently adjust the volume of each other player's voice (from 0% to 200%).
- **Architecture**: `SettingsData` already held a `Dictionary<int, float> PeerVolumeMultipliers` (key = Mirror `ConnectionId`) that was already serialized and already read by `VoiceSettingsConsumer.ApplyVoiceVolumes()`. However, no UI script existed to write into this dictionary.
- **Slider Range**: The slider uses `[0, 2]` (not `[0, 1]`). The float value is used directly as a multiplier in `baseVolume * peerMultiplier`. `1.0 = 100%`, `2.0 = 200%`, `0 = muted`.
- **Immediate Application**: Instead of waiting for the `SettingsManager` ‚Üí `ISettingsConsumer` propagation loop (which only fires when MasterVolume or VoiceVolume changes), a new static method `VoiceSettingsConsumer.ApplyPeerVolume(int peerId, float multiplier)` directly touches the target peer's `UnityAudioSource.volume` for zero-latency feedback while the user is dragging the slider.
- **Local Player Card Handling**: When `LobbyController` instantiates a `PlayerListItem`, it checks if the card belongs to the local player and if so, disables and hides the volume slider (`volumeSlider.SetConnectionId(id, isLocalPlayer: true)`). Adjusting your own outgoing voice for yourself is meaningless.
- **Persistence**: On slider change, `SettingsManager.UpdateSettings()` writes to `PeerVolumeMultipliers[connectionId]`. On startup/reconnect, `PlayerVolumeSlider.RefreshFromSettings()` restores the slider position from saved data.

### Code Modified/Added

#### [NEW] `Assets/1_Scripts/UI/PlayerVolumeSlider.cs`
- MonoBehaviour placed on each `PlayerListItem` prefab.
- Exposes `SetConnectionId(int, bool)` to bind itself to a Mirror peer by ConnectionId.
- Listens to `onValueChanged`, persists via `SettingsManager.UpdateSettings`, and calls `VoiceSettingsConsumer.ApplyPeerVolume` for real-time volume control.
- Hides the slider entirely when `isLocalPlayer = true`.

#### [MODIFY] `Assets/1_Scripts/Audio/VoiceSettingsConsumer.cs`
- Added `public static void ApplyPeerVolume(int peerId, float multiplier)` ‚Äî directly applies `baseVolume * multiplier` to the peer's `UnityAudioSource` for immediate feedback without waiting for `OnSettingsUpdated`.

#### [MODIFY] `Assets/1_Scripts/Networking/Lobby/LobbyController.cs`
- Both `CreateHostPlayerItem()` and `CreateClientPlayerItem()` now call `volumeSlider.SetConnectionId(player.ConnectionId, isLocal)` after each `PlayerListItem` instantiation.

## [2026-07-02] - Custom UI Cursor (Follower Architecture)

### Technical Justification & Details
- **Feature Request**: Hide the default system cursor and display a custom circle/disc shape cursor. The setup must avoid multi-scene canvas registration issues (e.g. cameras going missing during DontDestroyOnLoad transitions) and work cleanly in all rendering modes (Overlay and Screen Space Camera).
- **Implementation**: 
  - **Decoupled Architecture**: Upgraded `MouseManager.cs` to serve solely as a persistent global coordinator (`DontDestroyOnLoad`). It hides the default hardware cursor and exposes `ShouldShowCursor` (based on `Cursor.lockState`) and `MousePosition`.
  - **Local Follower Component**: Created `CustomCursorFollower.cs`. You can place the custom cursor prefab inside any local Canvas of any scene. The local follower reads the global `MouseManager` values, automatically adapts to the local Canvas scaler, handles camera lookups locally on the active Canvas, and handles visibility automatically.

### Code Modified/Added

#### [NEW] `Assets/1_Scripts/UI/CustomCursorFollower.cs`
- Local follower script to place on local custom cursor UI prefabs.
- Handles ScreenSpace camera-relative point translation locally, adapting instantly to any screen resolution, scale factor, or rendering mode.

#### [MODIFY] `Assets/1_Scripts/UI/MouseManager.cs`
- Stripped UI Canvas positioning logic.
- Serves as persistent, clean global mouse coordinate provider and hardware cursor suppression system.




## Codebase Audit - Phase 1: Core & Audio Systems (July 2026)
- **Modified Files**: IEntity.cs, ISettingsConsumer.cs, SettingsData.cs, SettingsManager.cs, MouthAnimator.cs, UniVoiceMirrorSetupSample.cs, UniVoicePlayerAudio.cs, VacuumAudioController.cs, VoiceSettingsConsumer.cs.
- **Why**: Enforcement of strict code standards. Missing architectural context made future maintenance risky.
- **Problem solved**: Added exhaustive XML <summary> tags detailing Description, Context, and Justification for all methods and properties. Added explicit [Tooltip] attributes with Role, Use Case, and Justification to all serialized variables to ensure clear intent directly in the Unity Inspector.


## Codebase Audit - Phase 2: Gameplay & Physics Systems (July 2026)
- **Modified Files**: Collectible.cs, ProceduralTubePhysics.cs, VacuumSuctionZone.cs.
- **Why**: Enforcement of strict code standards and improved observability for level designers.
- **Problem solved**: Added exhaustive XML <summary> tags detailing Description, Context, and Justification for all methods and properties. Added explicit [Tooltip] attributes with Role, Use Case, and Justification to all serialized variables, which is particularly critical for the heavily math-based ProceduralTubePhysics and VacuumSuctionZone scripts.


## Codebase Audit - Phase 3: Player Systems (July 2026)
- **Modified Files**: InputSettingsConsumer.cs, PhysicalHeadController.cs, PlayerArmsController.cs, PlayerController.cs, PlayerInputHandler.cs, PlayerInventory.cs, PlayerJumpComponent.cs, PlayerLookComponent.cs, PlayerMovementComponent.cs, PlayerVacuumController.cs, Eye.cs, PlayerCustomization.cs, PlayerViewRange.cs, Wheels.cs, ModelMigrator.cs.
- **Why**: Ensure standard practices across the heavily-populated player control domain. Complex networked input handlers require thorough explanation.
- **Problem solved**: Added XML summaries (Description, Context, Justification) and Tooltips (Role, Use Case, Justification) to demystify complex procedural physics (Arms/Head) and network sync logic (Vacuum/Customization). This reduces onboarding time for developers modifying player mechanics.


## Codebase Audit - Phase 4: Networking (July 2026)
- **Modified Files**: MyNetworkManager.cs, SteamLobby.cs, LobbyController.cs, LobbyCustomizationUI.cs, PlayerListItem.cs, PlayerObjectController.cs.
- **Why**: Solidify the networking core. Multiplayer synchronization is the most fragile part of the project.
- **Problem solved**: Added XML summaries and Tooltips to map out the Steamworks-to-Mirror handshake, Lobby UI refresh cycles, and [SyncVar] hooks. This ensures future modifications to the lobby don't accidentally break Steam integration or client-host state mismatch.

## Codebase Audit - Phase 5: UI Systems (July 2026)
- **Modified Files**: ColorButtonUI.cs, CustomTextButton.cs, UICustomButtonBase.cs, UIColorsPalettes.cs, InGameMenuController.cs, UIPanelController.cs, UINavigationGroup.cs, OpenURLButton.cs, CustomCursorFollower.cs, MouseManager.cs, PlayerVolumeSlider.cs, SettingsUIPresenter.cs.
- **Why**: Finalize the strict code standards implementation on the last remaining subsystem: the User Interface. The custom vector graphics UI and menu navigation controllers are highly customized and require thorough explanation for future maintainability.
- **Problem solved**: Added strict XML summaries (Description, Context, Justification) and Tooltips (Role, Use Case, Justification) across all UI scripts. Created `documentation/UI_System.md` to provide a high-level architectural overview of the vector UI integrations (Shapes), menu navigation, and input helpers, thereby successfully concluding the complete codebase audit.

## [2026-07-03] - Key Rebinding System & Multi-Menu Navigation

### Technical Justification & Details
- **Feature Request**: Implement key binding settings menu to let players customize their keyboard and mouse inputs, and support multi-category settings sub-menus.
- **Architecture**:
  - Designed `ControlRebindUIPresenter.cs` to coordinate a list of `RebindRowUI.cs` row entries.
  - Enhanced `InputSettingsConsumer.cs` to support interactive rebinding callbacks (onComplete, onCancel) and Escape-key cancellation flow.
  - Upgraded `UINavigationGroup.cs` to natively listen to the Escape key to close active sub-panels dynamically (allowing Escape to act as "Back").
  - Fixed duplicate field declarations in `CustomCursorFollower.cs` to restore compiling project state.
- **Persistence**: Rebinding overrides continue to be serialized to JSON and saved in `SettingsData` via `SettingsManager.UpdateSettings`.

### Code Modified/Added
- [NEW] [RebindRowUI.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/RebindRowUI.cs)
- [NEW] [ControlRebindUIPresenter.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/ControlRebindUIPresenter.cs)
- [MODIFY] [InputSettingsConsumer.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Controller/InputSettingsConsumer.cs) (Interactive overloads)
- [MODIFY] [UINavigationGroup.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/UINavigationGroup.cs) (Escape key back navigation)
- [MODIFY] [CustomCursorFollower.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/CustomCursorFollower.cs) (Fixed duplicate fields compilation error)
- [NEW] [walkthrough.md](file:///C:/Users/celestin/.gemini/antigravity-ide/brain/d23a534e-7777-4755-8e9f-c4cada1843ed/walkthrough.md) (Editor setup walkthrough)

## [2026-07-06] - Key Rebinding Upgrades, Left/Right Menu Sides & Duplicate Conflict Warnings

### Technical Justification & Details
- **Feature Request**: Resolve NullReferenceException on CustomTextButton, implement individual key reset, implement duplicate key conflict highlighting in Red, and support Left/Right concurrent panels in the UINavigationGroup.
- **NullReference Fix**: 
  - `OnDisable` triggers `KillActiveTweens()`. If the GameObject starts deactivated or gets deactivated before `Start()`, original states haven't been cached yet (`CacheOriginalStates()` runs in `Start()`). Thus, `_originalChildColors` is null, causing a NullReferenceException when indexing it in `KillActiveTweens()`.
  - Added an `_isCached` boolean flag to guard all original state restorations in `KillActiveTweens()` and `AnimateInteractableTransition()`.
- **Left / Right Multi-Panel Navigation**:
  - Added `PanelSide` Side setting to `UIPanelController` (categories: `Left` and `Right`).
  - Redesigned `UINavigationGroup` history tracking to split left and right panel groups, allowing a Left sub-menu panel (e.g. Settings Category) and a Right content panel (e.g. Controls or Audio) to stay visible simultaneously.
  - Automatically closes any open Right panel when returning back to the default Left panel (e.g. Main Menu).
  - Refactored history navigation to only push/pop Left panels, ensuring the Escape key/Back action always operates on Left panels.
- **Specific Key Reset**:
  - Added `ResetBindingToDefault(string actionName, int bindingIndex)` to `InputSettingsConsumer.cs` to remove single-binding overrides.
  - Hooked up `_rowResetButton` onClick listener in `RebindRowUI.cs` to trigger specific binding resets.
- **Conflict Highlighting**:
  - Implemented `CheckForDuplicateBindings()` in `ControlRebindUIPresenter.cs` that scans for active key conflicts across all rows.
  - Changes the key text label color to **Red** for duplicate/conflicting key assignments.
- **Strict Code Auditing & Comments**:
  - Added XML documentation summaries to all private/internal variables across modified UI scripts.
  - Added detailed code comments inside method bodies in B1 English to clarify algorithms.
- **Logging Gating**:
  - Added `_enableDebugLogs` serialized boolean field (defaulting to `false`) in `InputSettingsConsumer.cs` and guarded all standard `Debug.Log` calls to comply with the project's log reduction standard.

### Code Modified/Added
- [MODIFY] [CustomTextButton.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/CustomTextButton.cs) (Added `_isCached` state safety guards)
- [MODIFY] [UIPanelController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/UIPanelController.cs) (Added PanelSide enum and Side property)
- [MODIFY] [UINavigationGroup.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/UINavigationGroup.cs) (Implemented concurrent Left/Right panel and Left-only history stack)
- [MODIFY] [InputSettingsConsumer.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Controller/InputSettingsConsumer.cs) (Added ResetBindingToDefault method, _enableDebugLogs field, debug guards & B1 comments)
- [MODIFY] [RebindRowUI.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/RebindRowUI.cs) (Added reset button, duplicate coloring, B1 comments and XML variable summaries)
- [MODIFY] [ControlRebindUIPresenter.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/ControlRebindUIPresenter.cs) (Added duplicate scanning logic, B1 comments and XML summaries)
- [MODIFY] [walkthrough.md](file:///C:/Users/celestin/.gemini/antigravity-ide/brain/d23a534e-7777-4755-8e9f-c4cada1843ed/walkthrough.md) (Updated walkthrough)

## [2026-07-07] - Codebase Folder Reorganization & Assets Root Cleanup

### Technical Justification & Details
- **Feature Request**: Reorganize the project's folder structure, especially scripts, and clean up the root `Assets/` directory.
- **Organization & Cleanliness**:
  - Moved generic utility scripts to `Assets/1_Scripts/Utils/`.
  - Subdivided player controllers and visuals into `Assets/1_Scripts/Player/Movement/`, `Assets/1_Scripts/Player/Input/`, and `Assets/1_Scripts/Player/Mechanics/`.
  - Relocated editor utility scripts (such as `ModelMigrator.cs`) into an `Editor/` subdirectory under `Assets/1_Scripts/Player/Editor/`. This ensures that they compile only in editor contexts and prevents build packaging errors.
  - Subdivided UI scripts into `Assets/1_Scripts/UI/Core/`, `Assets/1_Scripts/UI/Components/`, and `Assets/1_Scripts/UI/Menus/` to separate core vector graphics math from actual buttons and high-level menu controllers.
  - Separated networking voice scripts from general audio scripts, relocating voice scripts to `Assets/1_Scripts/Audio/Voice/` and general game audio script and animator script to `Assets/1_Scripts/Audio/Controllers/`.
  - Moved loose Physic Materials (`ArmPart`, `NoFrixion`) from the `Assets/` root to a new dedicated `Assets/6_Physics/` directory.
  - Relocated the Input Actions asset from `Assets/` root to a clean configuration folder under `Assets/Input/`.
- **References Preservation**:
  - Moved script files and their associated `.meta` files simultaneously to keep Unity GUID references intact across all scenes and prefabs.
  - Updated the compile references inside `Assembly-CSharp.csproj` and `Assembly-CSharp-Editor.csproj` to match the new file locations on disk, allowing successful compiler build verification. Added exclusions for first-pass plugin scripts to prevent duplicate compilation.

### Code Modified/Added
- [NEW] [6_Physics](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/6_Physics) (New folder for physics materials)
- [NEW] [Input](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/Input) (New folder for input actions configuration)
- [MODIFY] [ArmPart.physicMaterial](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/6_Physics/ArmPart.physicMaterial) (Moved from Assets/ root)
- [MODIFY] [NoFrixion.physicMaterial](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/6_Physics/NoFrixion.physicMaterial) (Moved from Assets/ root)
- [MODIFY] [InputSystem_Actions.inputactions](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/Input/InputSystem_Actions.inputactions) (Moved from Assets/ root)
- [MODIFY] [InfiniteRotate.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Utils/InfiniteRotate.cs) (Moved)
- [MODIFY] [PlayerController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerController.cs) (Moved)
- [MODIFY] [PlayerMovementComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerMovementComponent.cs) (Moved)
- [MODIFY] [PlayerJumpComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerJumpComponent.cs) (Moved)
- [MODIFY] [PlayerLookComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerLookComponent.cs) (Moved)
- [MODIFY] [PhysicalHeadController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PhysicalHeadController.cs) (Moved)
- [MODIFY] [PlayerInputHandler.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Input/PlayerInputHandler.cs) (Moved)
- [MODIFY] [InputSettingsConsumer.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Input/InputSettingsConsumer.cs) (Moved)
- [MODIFY] [PlayerArmsController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Mechanics/PlayerArmsController.cs) (Moved)
- [MODIFY] [PlayerVacuumController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Mechanics/PlayerVacuumController.cs) (Moved)
- [MODIFY] [PlayerInventory.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Mechanics/PlayerInventory.cs) (Moved)
- [MODIFY] [ModelMigrator.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Editor/ModelMigrator.cs) (Moved to Editor folder)
- [MODIFY] [UICustomButtonBase.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Core/UICustomButtonBase.cs) (Moved)
- [MODIFY] [MouseManager.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Core/MouseManager.cs) (Moved)
- [MODIFY] [CustomCursorFollower.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Core/CustomCursorFollower.cs) (Moved)
- [MODIFY] [ColorButtonUI.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/ColorButtonUI.cs) (Moved)
- [MODIFY] [CustomTextButton.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/CustomTextButton.cs) (Moved)
- [MODIFY] [OpenURLButton.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/OpenURLButton.cs) (Moved)
- [MODIFY] [RebindRowUI.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/RebindRowUI.cs) (Moved)
- [MODIFY] [UIColorsPalettes.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UIColorsPalettes.cs) (Moved)
- [MODIFY] [InGameMenuController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/InGameMenuController.cs) (Moved)
- [MODIFY] [SettingsUIPresenter.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/SettingsUIPresenter.cs) (Moved)
- [MODIFY] [ControlRebindUIPresenter.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/ControlRebindUIPresenter.cs) (Moved)
- [MODIFY] [UIPanelController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/UIPanelController.cs) (Moved)
- [MODIFY] [UINavigationGroup.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/UINavigationGroup.cs) (Moved)
- [MODIFY] [PlayerVolumeSlider.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/PlayerVolumeSlider.cs) (Moved)
- [MODIFY] [UniVoiceMirrorSetupSample.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Audio/Voice/UniVoiceMirrorSetupSample.cs) (Moved)
- [MODIFY] [UniVoicePlayerAudio.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Audio/Voice/UniVoicePlayerAudio.cs) (Moved)
- [MODIFY] [VoiceSettingsConsumer.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Audio/Voice/VoiceSettingsConsumer.cs) (Moved)
- [MODIFY] [VacuumAudioController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Audio/Controllers/VacuumAudioController.cs) (Moved)
- [MODIFY] [MouthAnimator.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Audio/Controllers/MouthAnimator.cs) (Moved)
- [MODIFY] [Assembly-CSharp.csproj](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assembly-CSharp.csproj) (Updated script paths & filtered plugin duplicates)
- [MODIFY] [Assembly-CSharp-Editor.csproj](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assembly-CSharp-Editor.csproj) (Updated script paths)

## [2026-07-07] - Rebind Mouse Buttons & Concurrent Rebind Row Lock Fix

### Technical Justification & Details
- **Bug Fix (Mouse Rebinding)**:
  - Allowed Left Click and Right Click to be bound during rebinding by removing the coarse `.WithControlsExcluding("Mouse")` filter in `InputSettingsConsumer.cs`.
  - Excluded only mouse axes that could accidentally trigger a rebind upon simple cursor movement: `.WithControlsExcluding("<Mouse>/position")`, `.WithControlsExcluding("<Mouse>/delta")`, and `.WithControlsExcluding("<Mouse>/scroll")`.
  - Introduced a one-frame safety delay using a Coroutine in `InputSettingsConsumer.cs` before starting the `PerformInteractiveRebinding` operation. This ensures that the mouse click event which triggered the "Rebind" button is completely processed and cleared from the Input System event queue, preventing it from immediately registering and auto-completing the rebind.
- **Bug Fix (Concurrent Row Lock)**:
  - Exposed the public `IsListening` state on `RebindRowUI.cs`.
  - Added `IsAnyRowRebinding()` in `ControlRebindUIPresenter.cs` to check if any managed row is currently waiting for input.
  - Guarded `StartRebindingProcess` in `RebindRowUI.cs` so that if another row is already actively waiting for input, the click is ignored, preventing concurrent overlapping "...Press Key..." states.

### Code Modified/Added
- [MODIFY] [InputSettingsConsumer.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Input/InputSettingsConsumer.cs) (Allowed mouse button inputs, filtered mouse movements/scroll, and added one-frame coroutine delay)
- [MODIFY] [RebindRowUI.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/RebindRowUI.cs) (Exposed IsListening property and added concurrent rebind lock check)
- [MODIFY] [ControlRebindUIPresenter.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/ControlRebindUIPresenter.cs) (Implemented IsAnyRowRebinding check across all active rows)

## [2026-07-07] - Custom Shapes-Based Toggle Component & Settings UI Integration

### Technical Justification & Details
- **Feature Request**: Remplacer les boutons bascule (Toggle) d'Unity classiques par des composants de type Shapes (m√©thode hybride) dans le menu Audio.
- **UICustomToggle implementation**:
  - Created `UICustomToggle.cs` which inherits from `MonoBehaviour` and implements pointer interaction interfaces (`IPointerClickHandler`, `IPointerEnterHandler`, `IPointerExitHandler`).
  - Utilizes `Shapes.Rectangle` for the track background and `Shapes.Disc` for the slider knob/handle.
  - Applies smooth horizontal local movement to the handle using DOTween's `DOLocalMoveX` and morphs the track color using a generic `DOTween.To` tween to avoid direct extension method dependency conflicts.
  - Supports instant snapping (`animate = false`) for programmatic updates (e.g. menu setup on initialization) to prevent visual sliding artifacts when first opening the settings panel.
- **Presenter Integration**:
  - Replaced native `Toggle` fields (`_micTestToggle`, `_autoVadToggle`) in `SettingsUIPresenter.cs` with `UICustomToggle`.
  - The API calls (`isOn` and event listener bindings) map perfectly to our custom class, ensuring minimal refactoring overhead and zero behavioral differences.

### Code Modified/Added
- [NEW] [UICustomToggle.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomToggle.cs) (New custom vector Shapes-based toggle component)
- [MODIFY] [SettingsUIPresenter.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/SettingsUIPresenter.cs) (Changed toggle fields to UICustomToggle)
- [MODIFY] [Assembly-CSharp.csproj](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assembly-CSharp.csproj) (Added UICustomToggle compile target reference)

## [2026-07-07] - Custom Toggle Raycast Target Fix

### Technical Justification & Details
- **Bug Fix**:
  - uGUI's `EventSystem` requires a component inheriting from `UnityEngine.UI.Graphic` (like `Image`) with `raycastTarget` set to `true` to detect mouse hovers and click inputs. Because the custom shapes are drawn via the Shapes package rather than standard uGUI meshes, pointer events were not being triggered.
  - Added an `Awake()` validation check in `UICustomToggle.cs` that automatically checks for a `Graphic` component on the Toggle's GameObject. If missing, it dynamically attaches a transparent `Image` (`Color(0,0,0,0)`) with `raycastTarget = true`. This mirrors the behavior of `UICustomButtonBase.cs` and guarantees that mouse click inputs are captured immediately without requiring colliders or manual inspector configuration.

### Code Modified/Added
- [MODIFY] [UICustomToggle.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomToggle.cs) (Added UnityEngine.UI import and automatic transparent Image generation on Awake)

## [2026-07-07] - Custom Toggle Visual & Animation Updates

### Technical Justification & Details
- **Toggle Customizations**:
  - Re-mapped the toggle state colors to morph the **handle disc** (`_handle.Color`) instead of the track background.
  - Replaced the handle scale animation on hover with a **track height expansion animation** (`_track.Height`). The script caches the original track height on `Start()` and tweens it to `_originalTrackHeight + _trackHoverHeightOffset` using DOTween.
  - Adjusted the default `_handleLocalXOffset` from `0.4f` (suited for meter-scale world objects) to `25.0f` (pixels) to work beautifully inside uGUI coordinates.

### Code Modified/Added
- [MODIFY] [UICustomToggle.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomToggle.cs) (Re-engineered hover height animations, handle-based color changes, and uGUI-adapted offset values)

## [2026-07-07] - Custom Toggle Handle Center Alignment Fix

### Technical Justification & Details
- **Bug Fix**:
  - Overwriting the handle's absolute horizontal coordinate with `_handleLocalXOffset` caused alignment issues if the handle's pivot or design center in the editor was not exactly `X = 0`.
  - Updated `UICustomToggle.cs` to cache the initial local X coordinate of the handle (`_initialHandleX`) during `Start()`.
  - Transition offsets are now computed relative to this cached design center: `_initialHandleX + _handleLocalXOffset` for the active state and `_initialHandleX - _handleLocalXOffset` for the inactive state. This ensures that the handle slides symmetrically relative to its editor layout design.

### Code Modified/Added
- [MODIFY] [UICustomToggle.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomToggle.cs) (Cached initial handle local position and made target slider X calculations relative to it)

## [2026-07-07] - Custom Shapes-Based Slider Integration

### Technical Justification & Details
- **Feature Request**: Replace standard Unity UI Sliders in the settings menu with a custom vector Shapes-based slider (`UICustomSlider.cs`).
- **UICustomSlider implementation**:
  - Implements `IPointerDownHandler`, `IDragHandler`, `IPointerEnterHandler`, `IPointerExitHandler`.
  - Converts pointer screen points to local RectTransform coordinates using `RectTransformUtility.ScreenPointToLocalPointInRectangle`.
  - Supports modular configurations: handles cases where `_fill` (Rectangle) is null (handle-only, like sensitivity threshold) and where `_handle` (Disc) is null (fill-only, like live mic volume indicator).
  - Exposes `fillColor` property to allow dynamic scripting changes to the fill Rectangle's color.
- **Presenter Integration**:
  - Replaced the five native `Slider` fields (`_masterVolumeSlider`, `_voiceVolumeSlider`, `_micSensitivitySlider`, `_micLevelIndicator`, `_autoVadSensitivitySliderRef`) with `UICustomSlider`.
  - Removed `_micLevelFillImage` from the fields and updated the live voice indicator code to set `fillColor` on `_micLevelIndicator` directly, simplifying the inspector layout.

### Code Modified/Added
- [NEW] [UICustomSlider.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomSlider.cs) (New custom vector Shapes-based slider component)
- [MODIFY] [SettingsUIPresenter.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/SettingsUIPresenter.cs) (Changed slider fields to UICustomSlider and simplified level fill color mapping)
- [MODIFY] [Assembly-CSharp.csproj](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assembly-CSharp.csproj) (Added UICustomSlider compile target reference)
## [2026-07-08] - Custom Shapes-Based Slider Line Transition & Handle Realignment

### Technical Justification & Details
- **Slider Track & Fill Migration**:
  - Replaced the Shapes `Rectangle` components for `_track` and `_fill` with `Shapes.Line` components in `UICustomSlider.cs`.
  - Rectangle elements draw relative to their center/pivot which caused them to scale outwards in both directions when their width changed. By switching to `Line`, we define explicit `Start` and `End` local points, allowing the fill to grow cleanly from left-to-right.
  - Adjusted hover state animations to manipulate `Line.Thickness` instead of `Rectangle.Height`.
- **Coordinate System Alignment & Handle Correction**:
  - Realigned track, fill, and handle positioning to compute coordinates relative to the same source: the `RectTransform` local bounding box (`rectTransform.rect`).
  - The track line now stretches from `xMin` to `xMax`.
  - The fill line starts at `xMin` and ends at `Mathf.Lerp(xMin, maxX, pct)`.
  - The handle sits at the same `Mathf.Lerp(xMin, maxX, pct)` coordinate (with custom handle margins applied).
  - This solves the issue where the handle was misaligned relative to the track bounds and appeared in the middle of the slider when the value was at maximum.

### Code Modified/Added
- [MODIFY] [UICustomSlider.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomSlider.cs) (Re-engineered track and fill using Shapes Line, adapted hover thickness tweens, and realigned coordinate math to match uGUI boundaries)

## [2026-07-08] - Custom Shapes-Based Toggle Track Background Rect

### Technical Justification & Details
- **Toggle Customization**:
  - Added a new `Rectangle` reference `_trackBackground` in `UICustomToggle.cs` to act as the fill/background inside the toggle's border/track.
  - Caches the initial height of the track background (`_originalTrackBackgroundHeight`) on `Start()`.
  - Animates the height of `_trackBackground` symmetrically with the main `_track` component during hover enter and hover exit transitions (leveraging DOTween).

### Code Modified/Added
- [MODIFY] [UICustomToggle.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomToggle.cs) (Added background track shape and synchronized its hover animations with the outer track)

## [2026-07-08] - Custom Shapes-Based Slider Hover & Drag Polish

### Technical Justification & Details
- **Slider Handle Polish**:
  - Implemented the `IPointerUpHandler` interface in `UICustomSlider.cs` to accurately detect release events.
  - Caches the initial handle `Color` (`_originalHandleColor`) and `Radius` (`_originalHandleRadius`) on `Start()`.
  - Added visual configuration fields `_handleDragBloomMultiplier` (defaults to 1.5f) and `_handleHoverRadiusMultiplier` (defaults to 1.2f).
  - Configured `OnPointerEnter` and `OnPointerExit` to animate `_handle.Radius` using DOTween to simulate hover expansion.
  - Configured `OnPointerDown` and `OnPointerUp` to animate `_handle.Color` by applying/resetting the drag bloom multiplier, creating a premium glowing juice effect.

### Code Modified/Added
- [MODIFY] [UICustomSlider.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomSlider.cs) (Resolved slider TODO comments: implemented hover radius scaling and drag bloom multiplier animations using DOTween)

## [2026-07-08] - Custom Shapes-Based Toggle Hover & Transition Bloom

### Technical Justification & Details
- **Toggle Handle Polish**:
  - Caches the initial handle `Radius` (`_originalHandleRadius`) on `Start()`.
  - Added visual configuration fields `_handleHoverRadiusMultiplier` (defaults to 1.2f) and `_handleTransitionBloomMultiplier` (defaults to 1.5f).
  - Configured `OnPointerEnter` and `OnPointerExit` to animate `_handle.Radius` using DOTween to simulate hover expansion.
  - Configured `UpdateVisuals` (when animating transitions) to briefly flash the handle's `Color` with the HDR bloom multiplier during the horizontal slide translation, settling back down to the target ON/OFF color at the end.

### Code Modified/Added
- [MODIFY] [UICustomToggle.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomToggle.cs) (Added hover handle radius scaling and a DOTween sequence to flash HDR bloom on the handle color during state transitions)

## [2026-07-08] - Custom Shapes-Based Slider Disabled State (Grey out)

### Technical Justification & Details
- **Slider Typo Fix**:
  - Removed the compile-breaking typo `"e sois gris√©."` that was accidentally appended to the end of `UICustomSlider.cs`.
- **Disabled State Visual Transition**:
  - Added visual configuration fields `_disabledTrackColor`, `_disabledFillColor`, and `_disabledHandleColor` in `UICustomSlider.cs` to allow full inspector styling for non-interactable states.
  - Caches the initial/active track and fill colors (`_originalTrackColor`, `_originalFillColor`) on `Start()`.
  - Refactored `fillColor` property so setting fill color dynamically while disabled preserves the configured value in cache and only applies it visually upon slider re-activation.
  - Implemented `UpdateInteractableVisuals` which animates (with DOTween) or instantly sets the components' colors to their respective disabled or active values when the `interactable` property is toggled.

### Code Modified/Added
- [MODIFY] [UICustomSlider.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomSlider.cs) (Cleaned up trailing typo, cached original track/fill colors, added disabled state color configurations, and hooked up DOTween visual transitions on interactable state changes)

## [2026-07-08] - Custom Shapes-Based ListView / ScrollView

### Technical Justification & Details
- **ScrollRect & Shapes Integration (KISS)**:
  - Created a hybrid UI architecture leveraging Unity's native `ScrollRect` for stable physics (inertia, masking, touch dragging) alongside Freya Holm√©r's Shapes library for premium vector visuals.
- **Custom Scrollbar Component (`UICustomScrollbar.cs`)**:
  - Independent vector graphic scrollbar supporting horizontal and vertical directions.
  - Dynamically calculates the handle size relative to the scrollable content proportion (`size` property).
  - Includes hover states (thickness expansion) and active dragging states (HDR color bloom on click/drag) mapped via `IPointerDownHandler`, `IDragHandler`, etc.
  - Automatically hides the track and handle when `size >= 1f` (content fits perfectly).
- **Master ListView Component (`UICustomScrollView.cs`)**:
  - Automatically links to the sibling `ScrollRect` and overrides default `verticalScrollbar` and `horizontalScrollbar` mapping to prevent standard Unity graphic conflicts.
  - Uses `LateUpdate` to continually measure content vs viewport sizes, synchronizing the dynamic handle sizes directly to the custom scrollbars safely within Unity's layout pass flow.
  - Two-way binding for normalized scroll values between the `ScrollRect` logic and our `UICustomScrollbar` UI scripts.

### Code Modified/Added
- [NEW] [UICustomScrollbar.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomScrollbar.cs) (Custom vector scrollbar rendering logic and event handlers).
- [NEW] [UICustomScrollView.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomScrollView.cs) (Coordinator script linking standard ScrollRect to custom Shapes scrollbars).

## [2026-07-09] - Custom Shapes-Based Simple Button
### Technical Justification & Details
- **Dynamic Size Synchronization**:
  - Implemented the script with the `[ExecuteAlways]` attribute so it automatically updates the Shapes `Rectangle` components' width and height to match the `RectTransform` bounds, providing real-time UI feedback inside the Unity Editor without playing the scene.
- **Outward Growth Calculation**:
  - Developed a mathematical size compensation formula during hover expansion: when the rectangle border thickness increases, the width and height are padded by the exact thickness delta. This keeps the inner bounds of the button perfectly locked in place while the border expands purely outwards.
- **Infinite Dash Rotation**:
  - Configured the button to transition into a dashed border style (`Dashed = true`) on hover.
  - Implemented seamless frame-rate independent rotation of the dash offset within the `Update()` lifecycle using standard modulo `1.0f` math.
- **Tactile Click Feedback**:
  - Transferred the high-fidelity DOTween click sequence from `CustomTextButton.cs`, implementing a snappy transform scale pulse (1.15x) paired with a high-intensity white bloom flash, fast blackout, and holographic flickering return sequence.
- **Disabled State Handling**:
  - Implemented the `OnInteractableChanged` event handler override to grey out the button text and rectangle shape components when `Interactable` is toggled.

### Code Modified/Added
- [NEW] [UICustomSimpleButton.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomSimpleButton.cs) (New modular shape-based button supporting size-sync, hover dash rotation, outward growth, and snappy click sequence).

## [2026-07-09] - Rebind Row UI Custom Button Integration
### Technical Justification & Details
- **Polymorphic Custom Button Support**:
  - Refactored `RebindRowUI.cs` serialization fields `_rebindButton` and `_rowResetButton` to use the parent base class `UICustomButtonBase` instead of the standard UGUI `Button`. This allows assigning either `CustomTextButton` or `UICustomSimpleButton` modularly in the inspector.
- **Interactable API Alignment**:
  - Updated button interactivity state changes inside `RebindRowUI.cs` to invoke the public `Interactable` property (capital I) rather than the standard UGUI `interactable` field, ensuring correct DOTween transitions and disabling sequences run dynamically.

### Code Modified/Added
- [MODIFY] [RebindRowUI.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/RebindRowUI.cs) (Refactored serialization to UICustomButtonBase and aligned interactivity calls to use the capital Interactable property).

## [2026-07-09] - UI Shape Size Synchronization Helper
### Technical Justification & Details
- **Reusable Size Sync Component**:
  - Created `UIShapeSizeSync.cs` as a generic helper component for Shapes `Rectangle` components.
  - Implements the `[ExecuteAlways]` attribute to automatically capture the parent `RectTransform` dimensions and apply them to the `Rectangle` shape's width and height.
  - Avoids code repetition (SSOT) across custom UI components and simplifies layout designs without manual sizing configurations inside the Unity Editor.

### Code Modified/Added
- [NEW] [UIShapeSizeSync.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UIShapeSizeSync.cs) (Utility component for automatic synchronization of Shapes Rectangle bounds with local RectTransform dimensions).

## [2026-07-09] - Auto VAD Settings Serialization
### Technical Justification & Details
- **Settings Serialization Support**:
  - Added `_isAutoVad` serialized boolean field and a public `IsAutoVad` property to `SettingsData.cs`. This allows persisting the Auto VAD toggle state to disk (via JSON PlayerPrefs serialization) along with the rest of the game settings.
- **Consumer State Synchronization**:
  - Modified `VoiceSettingsConsumer.cs`'s `OnSettingsUpdated` method to check for changes to `IsAutoVad`. If the setting has changed, it updates the local state and triggers the `_onAutoVadChanged` action callback to restore default or manual VAD configurations.
- **UI & Manager Propagation**:
  - Refactored `VoiceSettingsConsumer.SetAutoVad` to update and save the settings state via `SettingsManager.Instance.UpdateSettings` when the SettingsManager is available, ensuring immediate disk flushing and synchronized consumer propagation.

### Code Modified/Added
- [MODIFY] [SettingsData.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Core/Settings/SettingsData.cs) (Added serialized field and property for IsAutoVad settings).
- [MODIFY] [VoiceSettingsConsumer.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Audio/Voice/VoiceSettingsConsumer.cs) (Synchronized IsAutoVad loading and saving through SettingsManager).

## [2026-07-09] - Control Rebind UI Custom Reset Button & Simple Button Polish
### Technical Justification & Details
- **Polymorphic Reset Button Support**:
  - Refactored `ControlRebindUIPresenter.cs`'s field `_resetButton` from standard UGUI `Button` to the base class `UICustomButtonBase`. This allows assigning any custom vector button component (e.g. `UICustomSimpleButton`) modularly to reset bindings in the Controls UI.
- **Visual State and Color Resets**:
  - Fixed color stuck bugs in `UICustomSimpleButton.cs` when spam clicking or hover exiting. Updated `KillActiveTweens()` to safely reset the rectangle and text color back to their active baseline (`_originalRectColor`, `Color.white`) or disabled baseline colors depending on the `Interactable` state.
- **Smooth Dotted Transition Animation**:
  - Replaced the immediate basic boolean toggle of the dashed border on hover with a smooth, 0.2s duration float tween of `_rect.DashSpacing`. The border is now kept dashed by default at runtime with `DashSpacing` initialized to `0f` (rendering as a continuous line), and is animated to `_dashSpacing` on hover enter and back to `0f` on hover exit.
- **OnEnable Visual Caching Reset**:
  - Implemented the `OnEnable` callback in `UICustomSimpleButton.cs` to call `InitializeDefaultVisuals()`, ensuring that any disabled buttons (such as when the Settings panel is closed) reset cleanly to their base visual states when reopened.

### Code Modified/Added
- [MODIFY] [ControlRebindUIPresenter.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/ControlRebindUIPresenter.cs) (Refactored reset button serialization to UICustomButtonBase).
- [MODIFY] [UICustomSimpleButton.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomSimpleButton.cs) (Polished visual state caching, implemented OnEnable resets, fixed stuck colors on tween cancels, and implemented smooth 0.2s dash spacing transitions).

## [2026-07-09] - Rebind Row Interactivity & Typewriter Optimizations
### Technical Justification & Details
- **Micro-Animation Click Preservation**:
  - Removed the `Interactable = false` lock on the active rebind button in `RebindRowUI.cs` when starting a key rebinding sequence. Disabling the button instantly cut off the click animation sequence mid-run. Since double-clicking or clicking other rows is already prevented programmatically via input state variables, removing the UGUI interactivity toggle allows the snappy scale and bloom click animation to execute fully.
- **Duplicate Typewriter Animation Prevention**:
  - Refactored `RebindRowUI.RefreshDisplay()` to perform a string comparison (`_bindingButtonText.text != newText`) before assigning the key label. Setting the text string on a TextMeshPro component automatically re-triggers any attached Febucci TextAnimator typewriter. Checking for changes prevents the typewriter animation from playing on all rows when resetting defaults or editing a single key.
- **Stuck Hover Visual state Fix**:
  - Modified the `Interactable` property setter in `UICustomButtonBase.cs` to instantly reset `_isHovered` to `false` when the button is disabled. This prevents the button from remembering a stale hover state if it is disabled while hovered.
  - Modified `UICustomSimpleButton.cs`'s `AnimateInteractableTransition()` to reset hover variables (`_rect.DashSpacing = 0f`, `_rect.Thickness`, etc.) when `isInteractable` is `false`, ensuring visual parameters return to normal.

### Code Modified/Added
- [MODIFY] [RebindRowUI.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/RebindRowUI.cs) (Optimized text refreshed comparison and bypassed button disabling during listening).
- [MODIFY] [UICustomButtonBase.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Core/UICustomButtonBase.cs) (Cleared hover state flag on interactability change).
- [MODIFY] [UICustomSimpleButton.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomSimpleButton.cs) (Reset visual thickness and dash spacing parameters when disabled).

## [2026-07-09] - Typewriter Caching & Hover Color Fixes
### Technical Justification & Details
- **Local Text Caching (`RebindRowUI.cs`)**:
  - Implemented a private `_lastAssignedBindingText` string variable. Rather than checking TMPro's raw text (which can return formatting tags or be altered by TextAnimator), we compare the retrieved binding string with this local cache.
  - TextMeshPro only receives text assignments when the binding text actually changes, preventing redundant TextAnimator typewriter triggers across all other rows.
  - Cleared this cache inside `StartRebindingProcess` to allow immediate redraws if the user rebinds the same key or cancels the rebind.
- **Conflict Text Hover Color Fix (`UICustomSimpleButton.cs`)**:
  - Removed the `_buttonText.color = Color.white` overwrite from `KillActiveTweens()`. Setting text color to white on every pointer enter/exit overrode the duplicate key conflict highlight (red color). Visual color switches between active and disabled states are now correctly isolated inside `AnimateInteractableTransition()`.

### Code Modified/Added
- [MODIFY] [RebindRowUI.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/RebindRowUI.cs) (Added local string caching to block redundant typewriter triggers and cleared it on rebind start).
- [MODIFY] [UICustomSimpleButton.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomSimpleButton.cs) (Removed text color overrides from the KillActiveTweens cleanup routine).

## [2026-07-10] - Custom Shapes-Based Dropdown

### Technical Justification & Details
- **Vector Shapes Dropdown Integration**:
  - Created `UICustomDropdown.cs` and `UICustomDropdownItem.cs` utilizing the Freya Holm√©r Shapes library to render premium vector outlines, backgrounds, and drop-down containers.
- **Header Button Visual Mirroring**:
  - Implemented border outline hover animations on the header `Rectangle` to match `UICustomSimpleButton.cs` exactly (outward thickness growth, dash spacing scaling, and infinite dash rotation).
  - Maintained a static, non-animated background shape for the header area.
  - Fetches the Febucci typewriter player from children to animate header selection updates.
- **Dropdown List Unfolding & Border Animations**:
  - Configured the list template container `_templateContainer` to unfold smoothly using a DOTween scale Y transition (0 to 1) with an `OutCubic` ease.
  - Attached a dedicated border outline `Rectangle` `_listBorder` that mimics the simple button's hover border animation while the dropdown is open (dashes rotate, thickness expands, and dash spacing increases).
- **Interactive Option Elements**:
  - Created option items that inherit from `UICustomButtonBase`, animating the background rectangle color on hover and triggering their child Febucci typewriter player.
  - Spawns option instances dynamically from the item template, populates labels, binds click events, and automatically closes the dropdown upon selection.
- **Click-Outside Blocker**:
  - Implemented an automatic blocker generator: when opened, it creates an invisible, fullscreen raycast blocker in the root Canvas to dismiss the dropdown when the player clicks outside the list container.
- **Settings Presenter Support**:
  - Swapped standard `TMP_Dropdown` with `UICustomDropdown` in `SettingsUIPresenter.cs`. The custom dropdown implements the exact same API signature (`ClearOptions()`, `AddOptions(List<string>)`, `value`, and `onValueChanged` event), enabling a seamless transition.

### Code Modified/Added
- [NEW] [UICustomDropdown.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomDropdown.cs) (Custom vector shapes dropdown header, panel blocker, item populator, and opening transitions).
- [NEW] [UICustomDropdownItem.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomDropdownItem.cs) (Dropdown option element controller handling background hover color transitions and typewriter relaunching).
- [MODIFY] [SettingsUIPresenter.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Menus/SettingsUIPresenter.cs) (Replaced standard TMP_Dropdown with UICustomDropdown for active microphone settings).
- [MODIFY] [Assembly-CSharp.csproj](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assembly-CSharp.csproj) (Added compile references for UICustomDropdown.cs and UICustomDropdownItem.cs).
- [x] Update DEVELOPMENT_LOG.md and todo.md to mark tasks as completed.

## [2026-07-10] - Custom Shapes-Based Toggle Handle Loading Fix
### Technical Justification & Details
- **Toggle State Loading Race Condition**:
  - Solved a startup visual initialization bug where setting `isOn` programmatically (e.g. from disk saves loading during early lifecycle cycles) before the toggle's `Start()` runs would result in the toggle handle shifting incorrectly.
  - The setter `isOn` triggered `UpdateVisuals()`, translating the handle using the uninitialized `_initialHandleX` (which is `0f`). Subsequently, when Unity's `Start()` hook fired, it cached the already offset coordinate (`35f` or `-35f`) as `_initialHandleX`, skewing all future target X calculations.
- **Lazy Caching System**:
  - Implemented `CacheOriginals()` and a private `_hasCachedOriginals` boolean state flag in `UICustomToggle.cs`.
  - The new method reads the initial coordinate `localPosition.x` of the handle exactly once, either from `Start()` or from `UpdateVisuals()` (whichever is executed first).
  - This guarantees that the correct reference center is captured regardless of early load orders.
- **Accessibility & Signature Validation**:
  - All modified fields and new helper methods are strictly private, preventing external visibility pollution.
  - Checked properties and types, assuring perfect backward compatibility and zero API surface changes.

### Code Modified/Added
- [MODIFY] [UICustomToggle.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomToggle.cs) (Added lazy caching mechanism to safely retrieve initial geometry configurations).

## [2026-07-12] - Custom Dropdown Editor Support
### Technical Justification & Details
- **Dropdown Configuration Serialization**:
  - Exposed options `_options` and active value `_value` fields in `UICustomDropdown.cs` via `[SerializeField]` and customized properties. This allows game designers to customize standard options lists and defaults inside Unity's Inspector.
- **Edit-Mode Visual Synchronization**:
  - Implemented `OnValidate()` in `UICustomDropdown.cs` to handle property modifications in the editor. It clamps index selection variables and updates TextMeshPro text references so the active value string updates instantly in the Scene view.
- **Unified Custom Inspector**:
  - Created `UICustomDropdownEditor.cs` under `Assets/1_Scripts/UI/Editor/` to organize complex dropdown visual properties into tidy collapsible Foldout groups (Header visual components, Template settings, Animation variables, Options configuration).
- **Hierarchy Validation Warning Checks**:
  - Added real-time error/warning check boxes inside the Custom Editor GUI. Displays explicit suggestions when essential components (outline rectangle shapes, text label components, template list bodies) are left empty.
- **Fast-Spawn Menu Integration**:
  - Implemented a hierarchy menu item `GameObject -> UI -> Shapes-Based Dropdown`. Generates, scales, nests, and pre-wires all necessary shapes, content layout groups, text elements, and template components under the current Canvas in one click.
- **Nested Foldout Warning Resolution**:
  - Replaced `EditorGUILayout.BeginFoldoutHeaderGroup` with standard `EditorGUILayout.Foldout` in `UICustomDropdownEditor.cs`. This prevents GUI layout warnings when displaying list/array properties (which have internal foldouts) inside visual groups.
- **Dynamic Template Auto-Sizing Layout**:
  - Configured `UpdateDimensions()` in `UICustomDropdown.cs` to dynamically adjust `_templateContainer`'s height based on `_itemParent.rect.height` at runtime and edit time.
  - Corrected `Content` layout parent anchoring (anchorMin: top-left, anchorMax: top-right, pivot: top-center) in `UICustomDropdownEditor.cs` menu helper to isolate vertical layout calculations and prevent circular size loops.

## [2026-07-12] - Premium Visuals & Dotted Hover Outline Animations
### Technical Justification & Details
- **Invisible-to-Visible Dotted Hover Outlines**:
  - Refactored both the dropdown header and the item template to keep their dotted outlines completely invisible (alpha 0) when not hovered.
  - On hover enter, they transition smoothly using DOTween to full opacity and animate their `DashSpacing` from 0f to target space value in 0.2s, mimicking `UICustomSimpleButton.cs`.
- **Item-Specific Outline & Caching**:
  - Extended `UICustomDropdownItem.cs` to support an outline `_rect` component. Updates dimensions inside `Update()` and animates dash spacing, thickness, and size offset on pointer hover.
- **Click-Only Background Transitions**:
  - Disabled item background color changes on pointer hover. The background now transitions to the selection color `_hoverColor` only when clicked, providing instant click feedback.
- **Hierarchy Creator Wiring**:
  - Modified `UICustomDropdownEditor.cs`'s GameObject menu builder to automatically instantiate, position, and bind the new item outline rectangle for the template.

## [2026-07-12] - Blockerless Custom Dropdown & New Input System Fixes
### Technical Justification & Details
- **New Input System Compatibility**:
  - Replaced legacy `Input.GetMouseButtonDown(0)` and `Input.mousePosition` references with `Mouse.current.leftButton.wasPressedThisFrame` and `Mouse.current.position.ReadValue()` to fix the `InvalidOperationException` crash when clicking or moving the mouse.
- **Independent Header Hover Behavior**:
  - Removed the `_isListOpen` lock inside `AnimateHoverExit()` so the header outline transition (solid vs dotted) matches exactly when the mouse pointer leaves or enters its physical boundaries, even while the dropdown list is open.
- **Blockerless outside-click detection**:
  - Removed the `Dropdown Blocker` GameObject entirely. Used coordinate checks on click (via `Mouse.current`) to close the list if clicked outside the header and template container bounds.

### Code Modified/Added
- [MODIFY] [UICustomDropdown.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomDropdown.cs) (Added UnityEngine.InputSystem imports, replaced legacy Input calls with Mouse.current checks, and removed list open checks on hover exit).

## [2026-07-13] - Dropdown Arrow Morph Animation
### Technical Justification & Details
- **Chevron vector morphing**:
  - Implemented a vector morphing animation for the dropdown chevron arrow, which is composed of two `Shapes.Line` components.
  - Closed chevron points down: Line 1 goes from (-17, 17) to (0, 0); Line 2 goes from (17, 17) to (0, 0).
  - Open chevron points up: Line 1 goes from (0, 0) to (17, -17); Line 2 goes from (0, 0) to (-17, -17).
  - Transition speed and ease curves are fully configurable in the Unity Inspector using exposed variables `_arrowAnimDuration` and `_arrowAnimEase`.
  - Morph is processed using DOTween for smooth runtime playback and snaps instantly during editor-time `OnValidate()` updates or initializations.

### Code Modified/Added
- [MODIFY] [UICustomDropdown.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomDropdown.cs) (Added serialized fields for arrow lines, duration, and ease; added AnimateArrow helper method; hooked morph triggers into Open, Close, OnValidate, and InitializeDefaultVisuals).

## [2026-07-13] - Dropdown Custom Inspector & KISS Cleanup
### Technical Justification & Details
- **Inspector Field Drawing (Editor Serialization)**:
  - Custom editor classes override the standard Inspector drawing. I added the arrow properties (`_arrowLine1`, `_arrowLine2`, `_arrowAnimDuration`, and `_arrowAnimEase`) to `UICustomDropdownEditor.cs`, exposing and drawing them under the "Animation Settings" Foldout group.
- **KISS Menu Creator Removal**:
  - Removed the complex hierarchy menu item creator shortcut method `CreateShapesBasedDropdown` (and associated child setups) from `UICustomDropdownEditor.cs` entirely to eliminate boilerplate code and simplify future asset maintenance.

### Code Modified/Added
- [MODIFY] [UICustomDropdownEditor.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Editor/UICustomDropdownEditor.cs) (Added serialized properties and drawn layouts for arrow animations inside OnInspectorGUI; removed the GameObject creation shortcut method).

## [2026-07-13] - Dropdown Arrow Line Size X & Y Parameterization
### Technical Justification & Details
- **Chevron Line Size X & Y Parameterization**:
  - Replaced the hardcoded coordinate values (17f / -17f) inside `AnimateArrow()` with two independent variables: `_arrowLineSizeX` and `_arrowLineSizeY` (both defaulting to 17f).
  - Exposed and drew `_arrowLineSizeX` and `_arrowLineSizeY` inside the custom editor `UICustomDropdownEditor.cs` under the "Arrow Animations" foldout group.

### Code Modified/Added
- [MODIFY] [UICustomDropdownEditor.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Editor/UICustomDropdownEditor.cs) (Added _arrowLineSizeXProp and _arrowLineSizeYProp serialization and drew them under the Arrow Animations group).

## [2026-07-13] - Dropdown Arrow Parent Offset Translation
### Technical Justification & Details
- **Chevron Parent Translation**:
  - Implemented vertical Y offset translation on open/close for the chevron parent RectTransform `_arrowParent`.
  - Closed dropdown: moves parent down by `_arrowParentOffsetY` relative to its baseline.
  - Open dropdown: moves parent up by `_arrowParentOffsetY` relative to its baseline.
  - Base Y coordinate `_originalArrowParentY` is cached on startup inside `CacheOriginalStates()`.
  - Exposed and serialized `_arrowParent` and `_arrowParentOffsetY` variables in the inspector using `UICustomDropdownEditor.cs`.

### Code Modified/Added
- [MODIFY] [UICustomDropdown.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Components/UICustomDropdown.cs) (Added _arrowParent and _arrowParentOffsetY fields, cached original position on start, and animated translation inside AnimateArrow).
- [MODIFY] [UICustomDropdownEditor.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/UI/Editor/UICustomDropdownEditor.cs) (Added properties serialization and drew fields inside Arrow Animations section of OnInspectorGUI).

## [2026-07-13] - Player Bone Bridge Architecture
### Technical Justification & Details
- **Decoupling Skeletal Mesh**:
  - Implemented the `PlayerBoneBridge` architecture to serve as a single source of truth (SSOT) for the player's skeletal bones. Control scripts (like `PlayerArmsController`, `PhysicalHeadController`) and physics joint systems now bind to static Bone Bridge transforms rather than model-specific bones.
  - At Awake, `PlayerBoneBridge` scans the child visual mesh for any `SkinnedMeshRenderer` and rebinds their `.bones` array and `rootBone` to the Bone Bridge transforms by name match, deforming the visual mesh using physics/bones animation output.
  - Added a follower script mechanism `RuntimeFollower` for non-skinned objects (e.g., wheels) to copy positions/rotations of Bone Bridge bones at runtime.
- **Dynamic Controls Wiring**:
  - Modified `PlayerCustomization.cs` to expose `ModelRenderer` as a public property.
  - `PlayerBoneBridge` detects the main renderer on the imported mesh at startup and assigns it to `PlayerCustomization.ModelRenderer`, which automatically reinstalls and instances the customized materials.
- **Editor Validation & Automation**:
  - Developed a custom editor `PlayerBoneBridgeEditor.cs` with validation checkers that report matching bone counts by name, and an auto-detector utility that populates custom followers (e.g. wheels) matching specific keywords.

### Code Modified/Added
- [NEW] [PlayerBoneBridge.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/PlayerBoneBridge.cs) (Manages runtime re-binding of skinned/non-skinned bones by name, and wires ModelRenderer to PlayerCustomization).
- [NEW] [PlayerBoneBridgeEditor.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Editor/PlayerBoneBridgeEditor.cs) (Custom inspector featuring bone name matching validators and keyword-based follower configuration tools).
- [MODIFY] [PlayerCustomization.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Visuals/PlayerCustomization.cs) (Exposed ModelRenderer property and added material instancing hooks on re-assignment).

## [2026-07-13] - Mouth Animator 3-Bone Thickness Preserving Scaling
### Technical Justification & Details
- **3-Bone Scaling Mechanics**:
  - Implemented 3-bone scaling support inside `MouthAnimator.cs` to match the modeler's armature layout designed to preserve thickness during mouth size changes.
  - When the target scale factor $S$ changes, the scale change vector is calculated as `change = targetScale - Vector3.one`. Each bone scales using a baseline scale of `Vector3.one` plus the scale change vector scaled by the bone's independent scale multiplier:
    - Bone 1 Scale = `1 + change * _bone1Multiplier` (scales with 100% of the mouth scale change, e.g. goes from 1 to 2)
    - Bone 2 Scale = `1 + change * _bone2Multiplier` (scales with 75% of the mouth scale change, e.g. goes from 1 to 1.75)
    - Bone 3 Scale = `1 + change * _bone3Multiplier` (scales with 50% of the mouth scale change, e.g. goes from 1 to 1.50)
  - Exposes 3 separate `Transform` fields (`_mouthBone1`, `_mouthBone2`, `_mouthBone3`) and their respective multipliers in the inspector to allow the user to easily configure the scaling ratios.
  - Implemented a backward-compatible check: if `_mouthBone1` is not set, it cleanly falls back to scaling the single `_mouthTransform` object, preventing inspector setup errors from breaking existing assets.

### Code Modified/Added
- [MODIFY] [MouthAnimator.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Audio/Controllers/MouthAnimator.cs) (Added bone fields and scale multipliers, updated scale application math in Update, and added backward compatibility fallback).

## [2026-07-13] - Remote Player Multiplayer Look & Wheels Sync
### Technical Justification & Details
- **Kinematic Wheel Speed Estimation**:
  - Since remote player clones have `Rigidbody.isKinematic = true` (to allow smooth Mirror `NetworkTransform` positioning without physics collisions dragging them), they have a default velocity of zero. This caused remote players' wheels to never rotate or steer.
  - Modified `WheelSteering` in `Wheels.cs` to dynamically compute estimated velocity using position changes over time (`(transform.position - _lastPosition) / Time.deltaTime`) when the Rigidbody is kinematic. This ensures wheels rotate and pivot realistically for all remote players.
- **Camera Look Pitch Synchronization**:
  - Yaw (turning left/right) is naturally synchronized because the root GameObject rotates, which is synced by the root's `NetworkTransform`.
  - Pitch (looking up/down) only updated the local camera localRotation on `isLocalPlayer`. Since remote players had their camera gameobjects deactivated, their pitch remained static at 0 degrees, meaning their heads never nodded/tilted and their procedural arms aimed straight ahead on other clients.
  - Added a `_syncedCameraPitch` `[SyncVar]` and a `CmdSyncCameraPitch` `[Command]` in `PlayerLookComponent.cs`. Local players stream their look pitch to the server when it changes by > 0.5 degrees, and other clients apply this synced pitch to the remote player's camera transform, allowing `PhysicalHeadController` to nod/aim physically.

### Code Modified/Added
- [MODIFY] [PlayerLookComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerLookComponent.cs) (Synced camera pitch look direction on the network via Command/SyncVar).
- [MODIFY] [Wheels.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Visuals/Wheels.cs) (Added kinematic estimated velocity fallback to steer wheels of remote players).

## [2026-07-14] - Snappy Shoulder Rotation Animation
### Technical Justification & Details
- **Snappy Shoulder Rotation**:
  - Implemented smooth, snappy shoulder rotation animations triggered automatically when individual arms are extended.
  - When the Left Arm is extended, the Left Shoulder rotates by +90 degrees on the Y-axis.
  - When the Right Arm is extended, the Right Shoulder rotates by -90 degrees on the Y-axis.
  - The rotation returns to 0 degrees when the respective arm is retracted.
  - Uses DOTween to animate local rotation. The transition utilizes `Ease.OutBack` by default (configurable in the inspector) to exceed/overshoot the target rotation slightly before settling, providing a snappy, responsive feel.
  - Pre-kills existing tweens (`shoulder.DOKill()`) to support rapid input spamming safely.
  - On startup (`Start()`), initial positions snap instantly without playing animations to match the starting extension states.
  - Triggered in the Mirror SyncVar hooks `OnLeftArmStateChanged` and `OnRightArmStateChanged`, ensuring the visual shoulder animations play synchronously across all clients in multiplayer.

### Code Modified/Added
- [MODIFY] [PlayerArmsController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Mechanics/PlayerArmsController.cs) (Added shoulder fields, snap initialization, and DOTween rotation animation triggers inside SyncVar hooks).

## [2026-07-14] - Player Arm Retraction Rest Float Physics & Joint Optimization
### Technical Justification & Details
- **Arm Colliders Separation (Self/Body Collision Bypass)**:
  - Dynamically disabled physics collisions on startup (`Start()`) between left/right arm structures and the player's main chassis, head, wheels, and other arm.
  - Ignored self-collisions between segments of the same arm to prevent mechanical lockup.
  - Decoupled arm physics from the player's core movement, completely fixing linear velocity bottlenecks (blocks/lags) and massive FPS drops under quick movement.
- **Dynamic Joint Stiffening & Locking**:
  - Automatically configured all `ConfigurableJoint` and `Rigidbody` components on start to enforce a high-stiffness, zero-stretch preset (`_jointSpringForce = 1500f`, `_jointDamping = 100f`, projection distance/angle bounds, and `angularXMotion` locked to prevent twisting on itself).
  - Configured Rigidbody `angularDamping` (Unity 6 drag replacement) dynamically to dampen rapid oscillations.
- **T-Pose Rest Targeting & Decaying Retraction Forces**:
  - Cached design-time local rest positions/rotations of the hands on startup relative to the player root.
  - Modified physics simulation to continuously attract hands back to their local T-pose coordinates when retracted, preventing them from sagging to the floor.
  - Implemented dynamic force/torque scaling based on elapsed time since release (`_retractTransitionDuration = 0.5s`):
    - **Transient Phase (Strong)**: Immediately after release, applies a strong force (`_releaseTransientForce`) to quickly pull the arm back to the T-pose.
    - **Resting Phase (Weak)**: Gradually decays to a weaker resting force (`_releaseRestForce`) and torque (`_releaseRestTorque`), keeping the arm suspended above the ground loosely without making it look rigidly frozen.
- **Distance-Based Fade-Out (Anti-Vibration & Anti-Wrist-Curve)**:
  - Added a `_restFadeDistance` parameter (default `0.35m`).
  - Inside `ApplyArmPhysicsForces`, if the arm is retracted, we calculate a `distanceFactor` which scales down to `0` linearly as the hand reaches the target rest position.
  - This eliminates jitter/vibrations at the equilibrium rest state since attraction forces drop to zero, and prevents the hand/wrist from being artificially torque-forced to align horizontally, letting the arm hang naturally aligned with the preceding joints without curving up.
- **Physics Solver & Deadzone Stabilizers (Continuous Hand Jitter Fix)**:
  - Added a strict `_restDeadzone` radius (default `0.05m`) to cut all external manual forces/torques to exactly `0` when the hand reaches rest.
  - Automatically configured `solverIterations = 25` and `solverVelocityIterations = 15` on all arm Rigidbody components dynamically on startup. This increases joint simulation precision, preventing the native joint spring calculations from oscillating/vibrating at high spring coefficients.

### Code Modified/Added
- [MODIFY] [PlayerArmsController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Mechanics/PlayerArmsController.cs) (Implemented collision ignoring, runtime joint tuning, rest pose caching, transient-to-rest force interpolation, distance-based fade-out with a strict deadzone, and high solver iterations on all arm rigidbodies).

## [2026-07-15] - Progressive Neck Curvature & Look Centralization
### Technical Justification & Details
- **Centralized Look State & Component-like Decoupling**:
  - Decoupled view pitch and yaw logic from physical movements by establishing `PlayerLookComponent.cs` as the Single Source of Truth (SSOT).
  - Exposed `CurrentPitch` and `CurrentYaw` public properties on `PlayerLookComponent.cs` to serve both local input and synced remote client states.
  - Refactored `PhysicalHeadController.cs` to read pitch and yaw directly from `PlayerLookComponent` rather than recalculating them independently, simplifying the component logic.
- **4-Bone Neck Chain Procedural Curvature**:
  - Implemented progressive relative local rotation for a 4-bone neck chain (`_neckBones`) in `PhysicalHeadController.cs`.
  - Distributes the centralized look rotation using configurable local rotation multipliers (`_neckRotationWeights`).
- **Progressive Backward Translation (Local -Z Receding)**:
  - Bending or turning the neck now drives a progressive backward translation of the neck bones along their own rotated local Z axis (`_neckBones[i].localRotation * Vector3.back`).
  - The recede distance scales dynamically based on local pitch and yaw magnitude and configurable factors (`_neckBackwardFactors`), which prevents mesh stretching and clipping against the torso.
- **Physical Head Target Tracking & Rotation Unlock**:
  - Detaches the physical head bone on startup and tracks the end of the procedurally bent neck chain.
  - Caches the starting relative offset of the head relative to the body root (`_headStartLocalPosInOriginalParent`) on `Start()`.
  - Calculates the ConfigurableJoint targetPosition offset dynamically relative to this initial position (`targetPosition = -offset`), preventing the head from collapsing downwards on startup.
  - Re-injects the crouch height offset (`_crouchYOffset`) directly on top of the target joint translation.
  - Dynamically configures the joint's rotation limits and drives on `Start()` to allow target rotation to rotate the head freely: sets `rotationDriveMode = RotationDriveMode.Slerp` and unlocks angular motions by setting `angularXMotion`, `angularYMotion`, and `angularZMotion` to `Free`.
- **Robust Automatic Inspector Fallback**:
  - Implemented automatic upward hierarchy traversal in `Start` starting from the head's parent to auto-populate the 4 neck bones in order (`Neck_01` to `Neck_04`) if they are left unassigned in the inspector.
- **Enabled Head Collision Physics & Weight Stabilization**:
  - Removed the `Physics.IgnoreCollision` setup between the head collider and body/arm colliders to let the head physically contact and collide with the body instead of passing through it.
  - Added serialized properties `_headMass` (default `0f`), `_positionSpring`, `_positionDamping`, `_rotationSpring`, and `_rotationDamping` to `PhysicalHeadController.cs` to dynamically configure physical weight resistance and spring stiffness. Setting mass to 0f allows Unity to use the minimum positive mass value, bypassing movement drag and body self-collision issues.
- **Torso Bone Rotation Control (Yaw decoupling)**:
  - Added a serialized `_torsoBone` field to `PlayerLookComponent.cs` along with network synchronization variables (`_syncedTorsoYaw` SyncVar and `CmdSyncTorsoYaw` command).
  - When a torso bone is assigned, yaw input rotates the torso bone (`_torsoBone.localRotation`) instead of rotating the whole player root transform.
  - Added support for camera nesting: if the camera is a child of the torso bone, it automatically inherits the torso's yaw; otherwise, yaw is applied to the camera transform directly. Remote players smoothly interpolate this torso yaw.
- **Vision Range Auto-Discovery Fallback**:
  - Refactored `PlayerViewRange.cs` to automatically auto-discover the player's main camera on startup and assign it to `_viewReference` if it was left unassigned in the inspector, restoring the vision cone orientation dynamically.

### Code Modified/Added
- [MODIFY] [PhysicalHeadController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PhysicalHeadController.cs) (Added neck configuration fields, cached initial transforms, added automatic fallback discovery, refactored ApplyJointTargetState to drive neck bones and joint targets using Atan2 projections and relative offsets, removed collision ignore logic, added serialized physics settings to override head mass/joint drives, and unlocked angular motion limits dynamically on Start).
- [MODIFY] [PlayerLookComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerLookComponent.cs) (Added _torsoBone serialized field and network sync variables, exposed CurrentPitch and CurrentYaw properties, refactored HandleRotation and Update to apply yaw to the torso bone and camera nesting, and cached original torso rotation offset).
- [MODIFY] [PlayerViewRange.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Visuals/PlayerViewRange.cs) (Implemented auto-discovery camera fallback for _viewReference on start local player).

## [2026-07-15] - Torso Pivoting, Decoupled Movement & Clean Physics Setup
### Technical Justification & Details
- **Auto-Discovery of Torso Bone**:
  - Implemented automatic child hierarchy search in `PlayerLookComponent.cs` on startup. If `_torsoBone` is left unassigned, it automatically scans for transforms matching names "torso", "chest", "spine", or the parent of "Neck_01", making the inspector setup plug-and-play.
- **Torso-Relative Movement Direction**:
  - Refactored `PlayerMovementComponent.cs` to calculate horizontal movement vectors relative to `PlayerLookComponent.CurrentYaw` rather than the player root's forward/right vectors.
  - This allows the wheelchair-like wheels base to remain horizontally static while the player still moves forward/sideways in the direction their torso is looking.
- **Coordinate Space Correction for Head Joint**:
  - Fixed a critical coordinate space mismatch in `PhysicalHeadController.cs`. The ConfigurableJoint's target position and target rotation are relative to the connected body Rigidbody.
  - Caches `_bodyRoot` (the joint's `connectedBody` Transform, or falls back to the player parent Rigidbody/root).
  - Converts all desired world coordinates of the neck tip into `_bodyRoot` space rather than the neck parent space (`_originalParent`).
  - This allows the head to pivot cleanly and follows the torso's yaw rotations without collapsing or collapsing downwards.
- **Simplification of Procedural Neck Bending**:
  - Simplified the procedural neck bone bending to distribute pitch (nodding) only.
  - Removed local yaw bending since the torso already rotates on yaw, allowing the neck and head to stay perfectly aligned as a single solid horizontal unit when looking around.

### Code Modified/Added
- [MODIFY] [PlayerLookComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerLookComponent.cs) (Implemented automatic fallback detection of the torso bone in child hierarchy on startup).
- [MODIFY] [PlayerMovementComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerMovementComponent.cs) (Modified movement vector calculations to align with look yaw instead of the root transform).
- [MODIFY] [PhysicalHeadController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PhysicalHeadController.cs) (Cached the connectedBody/player root as `_bodyRoot`, converted all joint target calculations to `_bodyRoot` space, and simplified progressive neck bending to only distribute look pitch).

## [2026-07-15] - KISS Clean Setup & Reference Simplification
### Technical Justification & Details
- **Removed Auto-Discovery Fallback Guess-Work**:
  - Removed torso bone auto-discovery from `PlayerLookComponent.cs`.
  - Removed camera auto-discovery from `PlayerViewRange.cs`.
  - Removed neck bone auto-discovery from `PhysicalHeadController.cs`.
  - T- **Direct ConnectedBody Coordinate Space & Torso Joint Rigging**:
  - Simplified `_bodyRoot` resolution in `PhysicalHeadController.cs`. It now uses `_joint.connectedBody.transform` directly if assigned in the editor, and falls back to `_originalParent` otherwise.
  - This completely solves joint yaw snapping when looking past 180 degrees. Because the joint rotates w- **Unified Agnostic Physical Coordinates**:
  - Removed all dynamic Rigidbody attachments, visual counter-rotation band-aids, and custom look conversions.
  - Simplified `PlayerLookComponent.cs` to handle mouse inputs and rotate the `_torsoBone` transform. If a Rigidbody is attached to the torso bone in the Unity Editor (kinematic or physical), the script automatically uses `MoveRotation` in `FixedUpdate` to drive it smoothly, avoiding out-of-sync physics solver jitters.
  - Refactored `PhysicalHeadController.cs` to be completely reference-space agnostic. It resolves the joint's target coordinates in the space of `_joint.connectedBody` directly, meaning it adapts automatically to whichever Rigidbody/bone the user assigns in the Editor.
  - Removed joint motion limits overriding from the script, allowing the user to configure limits (`angularXMotion`, `angularYMotion`, etc.) directly in the Unity Inspector.

### Code Modified/Added
- [MODIFY] [PlayerLookComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerLookComponent.cs) (Cleaned torso yaw rotation to support standard transform or Rigidbody MoveRotation in FixedUpdate).
- [MODIFY] [PlayerMovementComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerMovementComponent.cs) (Used Look Component yaw relative vectors for movement calculations).
- [MODIFY] [PhysicalHeadController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PhysicalHeadController.cs) (Simplified body root resolving to use connectedBody directly, relative target calculations, and removed hardcoded joint angular limit overrides).

## 2026-07-15: Reconstruction V5 (Camera Alignment & Signed Bending offsets)

### Fix / Feature
- Rebuilt from scratch the look, head, movement direction, and eye tracking scripts to support exact world camera looking, lagged physics head tilting, locked horizontal yaw, signed neck recoil translations, and instant pupil tracking.

### Rationale
- Decoupled physical wiggling from camera look targeting by overriding the camera's world rotation in `LateUpdate` (100% precision), while allowing its position to follow the head Rigidbody.
- Locked the head's horizontal yaw joint (`Angular Y Motion` = Locked) to prevent horizontal twisting/lag, while leaving Pitch and Roll wobbly.
- Replaced the absolute recoil factor with signed recoil (`bonePitch * _neckBackwardFactors[i]`), ensuring correct recoil (backward in `-Z` when looking down, forward in `+Z` when looking up).
- Replaced soft fallback validation checks with loud assertions (throwing explicit exceptions) to make setup issues obvious in the Unity Editor console.

### Code Modified/Added
- [MODIFY] [PlayerLookComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerLookComponent.cs) (World camera rotation override, kinematic assertions).
- [MODIFY] [PlayerMovementComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerMovementComponent.cs) (Torso-look relative movement forces).
- [MODIFY] [PhysicalHeadController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PhysicalHeadController.cs) (Signed neck translations, joint look targeting, kinematic torso check asserts).
- [MODIFY] [Eye.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Visuals/Eye.cs) (Added instant world pupil rotation and 70% slerped eye rotation).

## [2026-07-15] - Reconstruction V5 (Step 0: Scrap Logic)

### Feature / Refactoring
- Emptied all logical method bodies in `PhysicalHeadController.cs` and `PlayerLookComponent.cs` as part of Step 0 of the implementation plan, while preserving all existing comments. Eye tracking scripts (`Eye.cs`) were left untouched.

### Rationale
- Allows rebuilding look, camera world alignment, physical neck bending, and physical head joint features step-by-step from clean files to ensure maximum stability and zero legacy interference.

### Code Modified/Added
- [MODIFY] [PhysicalHeadController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PhysicalHeadController.cs) (Scrapped execution logic in method bodies).
- [MODIFY] [PlayerLookComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerLookComponent.cs) (Scrapped execution logic in method bodies).

## [2026-07-15] - Reconstruction V5 (Step 1: Torso Look & Relative Movement)

### Feature Added
- Reimplemented mouse look logic in `PlayerLookComponent.cs`.
- Kinematic torso bone Rigidbody rotation via `MoveRotation` in `FixedUpdate` (Y-axis Yaw).
- Camera world rotation forced to 100% look accuracy in `LateUpdate` (X-axis Pitch and Y-axis Yaw).
- Strict runtime validations/asserts for torso bone assignment and Rigidbody kinematic setting on startup.

### Rationale
- Decouples torso rotation from movement root and prevents double camera sensitivity by enforcing absolute world rotation on camera.
- Relies on kinematic Rigidbody `MoveRotation` for smooth physics-accurate updates rather than direct transform manipulation, preventing visual jitters.

### Code Modified/Added
- [MODIFY] [PlayerLookComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerLookComponent.cs) (Reimplemented Look methods, cursor locking, and kinematic Rigidbody updates).

## [2026-07-15] - Reconstruction V5 (Step 2: Neck Bending & Signed Offset)

### Feature Added
- Reimplemented neck bending logic in `PhysicalHeadController.cs`.
- Iterates over all cached neck bone transforms, rotating them on the vertical Pitch axis relative to their initial local orientation based on the camera's Pitch.
- Applies a signed translation offset in the local Z axis for bones with a translation factor > 0 (recedes in -Z when looking down, advances in +Z when looking up).

### Rationale
- Creates a smooth visual curvature for the robot's neck that mirrors the vertical look direction.
- Dynamic signed offsets prevent the head mesh from clipping/colliding with the main robot body when looking at extreme angles.

### Code Modified/Added
- [MODIFY] [PhysicalHeadController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PhysicalHeadController.cs) (Reimplemented Start, FixedUpdate, and ApplyJointTargetState for neck bending and translation offsets).

## [2026-07-16] - Reconstruction V5 (Step 1 Update: Decoupled Visuals & Root Player Rotation)

### Feature Added
- Reverted head joint and neck bending logic to Step 0 (empty method bodies in `PhysicalHeadController.cs`).
- Refactored `PlayerLookComponent.cs` to rotate the entire player root Rigidbody horizontally (Yaw) in `FixedUpdate` instead of only rotating the Torso bone.
- Added serialized `_wheelsChassisVisual` field in `PlayerLookComponent.cs` to handle counter-rotation of the wheels chassis.
- In `LateUpdate`, if `_wheelsChassisVisual` is assigned, applies a local rotation of `-targetYaw` to keep the wheels visual stationary in world space.
- Cleaned up and removed all unused `_torsoBone`, `_torsoRb`, and `_originalTorsoLocalRot` fields and checks from `PlayerLookComponent.cs`.

### Rationale
- Rotating the entire player root keeps all child joints and transforms aligned inside a single rotating reference frame, avoiding joint reference frame torsion issues.
- Visual counter-rotation of the wheels visual chassis maintains the aesthetic decoupling of the wheels visual relative to the player's look direction, preserving the original design.
- Cleaning up the torso fields keeps the inspector and code clean according to the KISS principle since the torso bone is no longer rotated independently.

### Code Modified/Added
- [MODIFY] [PhysicalHeadController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PhysicalHeadController.cs) (Reverted method bodies to empty).
- [MODIFY] [PlayerLookComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerLookComponent.cs) (Switched to player root Rigidbody rotation, added wheels chassis visual counter-rotation, and removed torso bone references/validations).

## [2026-07-16] - Active Ragdoll Head Pitch & Neck Bending Control

### Feature Added
- **Active Ragdoll Pitch Control**: Replaced old procedural neck bone bending logic in `PhysicalHeadController.cs` with physical pitch-based active ragdoll joint targeting using Slerp drives.
- **Organic Softbody Reactions**: Intermediate neck bones that are not actively driven (e.g. Neck base, Neck 1, Neck 3) now flex and bend organically in response to the physical motion of the driven bones (Neck 2 and Head).
- **Automatic Physics Configuration**: Dynamically configures all ConfigurableJoint and Rigidbody parameters under an optional neck root transform at Start (stiffening drives, enabling projection, setting solver iterations and high angular drag to prevent jitter/stretch).
- **Collision Separation**: Programmatically configures all neck and head colliders to ignore collisions with the rest of the player's body to avoid physics glitches.

### Code Modified/Added

#### [MODIFY] [PhysicalHeadController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PhysicalHeadController.cs)
- **Class `PhysicalHeadController`**: Completely rewritten. Features a configurable `_controlledJoints` list mapping bones to pitch multipliers, dynamic active ragdoll setup, collision ignoring, and mathematically precise joint-space target rotation updates in `FixedUpdate`.

### Technical Justification & Details
- **Joint Space Target Rotation Offset**: Standard Unity ConfigurableJoint `targetRotation` operates in joint-space. Transformed the desired pitch rotation offset relative to the starting local rotation into the joint's local axes to guarantee exact alignment.
- **KISS Philosophy**: Completely removed procedural translation curves and camera-lag ratios, relying instead on pure PhysX joint dynamics.
- **Explicit Visibility & Standard compliance**: Followed Allman styling, explicit visibilities, and private `_camelCase` member naming.

## [2026-07-17] - Centralized Player Collision ignoring Manager (SSOT)

### Feature Added
- **Centralized Player Collision Manager**: Created `PlayerCollisionManager.cs` to serve as the Single Source of Truth (SSOT) for all player-internal physics collision ignoring rules.
- **Custom Torso and Arm Collision Interactivity**: 
  - Allows two custom torso colliders (A and B) to ignore collisions with the wheels.
  - Torso Collider A ignores collisions with the arms.
  - Torso Collider B **does not ignore** collisions with the arms, allowing arms to physically collide and interact with this specific torso collider.
  - Centralizes other standard player collision rules (head/neck vs torso/wheels, arms self-collisions, arm vs arm) in one location.

### Code Modified/Added

#### [NEW] [PlayerCollisionManager.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerCollisionManager.cs)
- **Class `PlayerCollisionManager`**: Centralizes classification and configuration of physics collision exemptions using `Physics.IgnoreCollision` at startup.

#### [MODIFY] [PlayerArmsController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Mechanics/PlayerArmsController.cs)
- Removed local `IgnorePlayerCollisions()` method and call, delegating arm collision management entirely to the centralized `PlayerCollisionManager`.

#### [MODIFY] [PhysicalHeadController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PhysicalHeadController.cs)
- Removed local `IgnorePlayerCollisions()` method and call, delegating head/neck collision management entirely to the centralized `PlayerCollisionManager`.

### Technical Justification & Details
- **Selective Physics Blocking**: Employs lists and explicit references to handle different interaction parameters on specific torso colliders.

## [2026-07-17] - Dynamic Joint Stiffness & Rest Vibration Damping

### Feature Added
- **State-Dependent Joint Stiffness**: Introduced dynamic spring force adjustments for arm joints. Left and right arm joints now transition spring/damping forces based on extension state.
- **Separate Shoulder and Elbow/Wrist Tuning**: Exposed independent properties for the shoulder joint (stiffer at rest to prevent sagging) and the elbow/wrist joints (softer at rest for loose/relaxed arms).
- **Vibration Damping**: Enabled fine-tuned damping variables for rest states to eliminate high-frequency jitter and trembling in limp limbs.

### Code Modified

#### [MODIFY] [PlayerArmsController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Mechanics/PlayerArmsController.cs)
- Added caching structures and collections for left/right shoulder joints and left/right elbow/wrist joints at initialization.
- Modified `ConfigureArmJointsPhysics` to skip setting hardcoded slerp drives.
- Added `UpdateJointDrives` method to dynamically update ConfigurableJoint spring/damping drives.
- Updated `FixedUpdate` to refresh joint drives when state changes.

## [2026-07-24] - Robot Arm Physics Shoulder Lag Diagnostic & Architectural Solutions

### Technical Justification & Details
- **Shoulder Physics Lag Diagnosis**:
  - Analyzed the physics update loop and coordinate hierarchies for the robot arms.
  - The shoulder lag (delayed translation during movement) occurs because the shoulder's root transform has a Rigidbody (often kinematic) that is nested inside the player movement root Rigidbody.
  - In Unity, nested Rigidbodies (where a child Rigidbody is translated by a parent Rigidbody) face synchronization conflicts. PhysX updates the parent Rigidbody's position at the end of the physics solver loop, while the child's transform is resolved differently. If interpolation is active on the parent and not on the child (or vice versa), it causes a visual "rubber band" lag during movements.
  - Furthermore, if the shoulder is connected via a `ConfigurableJoint` to the torso, PhysX solvers allow slight constraint stretching (joint stretching) under fast accelerations, causing the shoulders to drift.
- **Architectural Proposals**:
  - **Solution A (Recommended)**: Remove the Rigidbody from the shoulder joint (making it a static child Transform of the Torso) and anchor the first physical segment (Upper Arm) directly to the Torso Rigidbody via a `ConfigurableJoint` offset.
  - **Solution B (Runtime Unparenting)**: Unparent the arm root hierarchy at `Start()` (setting `transform.parent = null;`), keeping it non-kinematic and connecting the shoulder to the Torso Rigidbody via a fully translation-locked `ConfigurableJoint` with interpolation enabled.

### Code Modified/Added
- None (Diagnostic and architectural analysis).

## [2026-07-25] - Cone-based Arm Vacuum Suction & Vortex Animation (KISS)

### Technical Justification & Details
- **Trigger Collider Abandonment**:
  - Replaced the old trigger collider-based detection in `VacuumSuctionZone.cs` with a precise `Physics.OverlapSphere` manual query combined with a forward dot product angle filter.
  - This eliminates standard Unity trigger detection issues (unreliable collision triggers at high velocities) and avoids declaring a physical trigger collider on the arm segment.
- **Occlusion Handling & Surface Visibility**:
  - Added a multi-raycast system targeting the center and lateral boundaries (15cm offset) of the collectible.
  - Visibly scales the force applied based on occlusion (0/3, 1/3, 2/3, or 3/3 clear paths) so collectibles hiding behind walls or other objects are not sucked up until the path is cleared, ensuring high physical realism.
- **Vortex Rotation & Centripetal Alignment**:
  - Added a centripetal force that dynamically pulls the collectible towards the central axis line of the nozzle forward direction, preventing items from getting stuck on the nozzle outer edges.
  - Added torque physics (`AddTorque`) to spin the object dynamically around the nozzle axis to create a realistic physical vortex.
- **Visual Scale Smoothing**:
  - Replaced the distance-locked scale calculation with a time-based Lerp approach.
  - When suction is lost or deactivated, collectibles regrow back to their original size and resume normal gravity behavior smoothly.

### Code Modified/Added
- [MODIFY] [VacuumSuctionZone.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Physics/VacuumSuctionZone.cs) (Rebuilt the entire class to support manual cone queries, visibility calculations, centripetal alignment, torque spin, and smoothed bi-directional scale transitions; corrected local suction axis to be configurable via a serialized `LocalAxis` enum in the Unity Inspector).

## [2026-08-05] - 4-Wheel Pyramid Suspension Physics & Jump Refactor (SSOT)

### Technical Justification & Details
- **4-Wheel Pyramid Suspension Physics**:
  - Implemented `WheelSuspensionController.cs` to act as the Single Source of Truth (SSOT) for configuring and driving 4-wheel suspension joints.
  - Programmatically configures `ConfigurableJoint` components on all 4 wheels on Awake:
    - Translation in X and Z is locked (`xMotion = Locked`, `zMotion = Locked`).
    - Downward Y translation is limited (`yMotion = Limited`, bounded by `_maxSuspensionDistance`).
    - Rotational degrees of freedom are fully locked (`angularXMotion = Locked`, `angularYMotion = Locked`, `angularZMotion = Locked`) to form a rigid pyramid spring structure from the hips/torso.
  - Initial wheel positions in editor are treated as the minimal (highest / fully compressed) baseline position. Wheels extend downwards toward the ground.
- **Resting Elevation Push Force**:
  - Added resting elevation spring push force system (`_defaultPushForce`, `_springStiffness`, `_restExtensionDistance`).
  - Automatically computes per-wheel ground raycasts and applies upward forces on the chassis and downward forces on wheels to keep the body resting floating in the air upon game start.
- **Dynamic Jump Takeoff & Landing Suspension Sequence**:
  - Refactored `PlayerJumpComponent.cs` to query grounding from `WheelSuspensionController` and trigger upward impulse.
  - During jump takeoff, `WheelSuspensionController` forces joint target positions to `_maxSuspensionDistance` for `_jumpRetractDelay` seconds, visually demonstrating suspension extension lag as the body rises while wheels briefly stay extended near ground.
  - Mid-air, wheels retract toward the body.
  - Upon landing, wheels touch ground first and slam/compress upward against the ground, absorbing shock seamlessly via spring dampers.
- **Internal Collision Exemption**:
  - Updated `PlayerCollisionManager.cs` to apply `IgnoreSelfCollisions(_wheelsColliders)` and prevent wheel-to-wheel physics lockups.

### Code Modified/Added
- [NEW] [WheelSuspensionController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/WheelSuspensionController.cs) (Centralized 4-wheel pyramid suspension physics, ground elevation push forces, multi-wheel raycast grounding, and jump takeoff/landing dynamics).
- [MODIFY] [PlayerJumpComponent.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerJumpComponent.cs) (Integrated with WheelSuspensionController for ground state checks and jump takeoff suspension triggers).
- [MODIFY] [PlayerCollisionManager.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/PlayerCollisionManager.cs) (Added wheel self-collision ignoring rules).

## [2026-08-05] - Fix Infinite Bounce & Add Scene View Gizmos (WheelSuspensionController)

### Technical Justification & Details
- **Infinite Bounce Root Cause & Fix**:
  - Removed manual `AddForceAtPosition` from `FixedUpdate()`, which was double-applying force on top of PhysX `ConfigurableJoint` `yDrive`. Compounding forces had created infinite energy gain / spring oscillation.
  - Rely exclusively on PhysX `ConfigurableJoint`'s `yDrive` (with `positionSpring` and `positionDamper` = 60f) for smooth non-bouncing shock absorption and resting height elevation.
- **Ignore Self-Colliders on Raycast Ground Check**:
  - `CheckWheelGrounded` now dynamically filters out all colliders belonging to the player hierarchy using `HashSet<Collider>`, preventing raycasts from falsely hitting the player's own wheels or body.
- **Scene View Visual Debug Gizmos (`OnDrawGizmosSelected`)**:
  - **Ground Check Raycasts**: Rendered as vertical lines from each wheel down to `_groundCheckDistance` with a sphere indicator (**Green** when grounded, **Red** when airborne).
  - **Max Suspension Distance**: Rendered as a **Cyan** line and wire cube showing the maximum downward extension range (`_maxSuspensionDistance`).
  - **Rest Extension Height**: Rendered as a **Magenta** wire cube indicating the target resting float position (`_restExtensionDistance`).
  - **Pyramid Connection Structure**: Rendered as **Yellow** lines connecting the main body center to each of the 4 wheel mounts.

### Code Modified/Added
- [MODIFY] [WheelSuspensionController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/WheelSuspensionController.cs) (Refactored to rely on PhysX `yDrive` spring damper, exclude player self-colliders from ground check, add `OnValidate()` range clamping, and render 4-color Scene view Gizmos).

## [2026-08-05] - Baseline Alignment & Scaled Suspension Ranges Fix (WheelSuspensionController)

### Technical Justification & Details
- **Baseline Position & Editor Mount Point Alignment**:
  - Re-aligned Gizmos calculation to compute `baselineMountPos` from `joint.connectedBody.transform.TransformPoint(joint.connectedAnchor)` (the bottom plate of the robot body).
  - This guarantees that Gizmo bounds (Rest Height Magenta, Max Travel Cyan) render starting directly from the robot's lower body plate rather than floating meters below in the air.
- **Scaled Suspension Travel Range Defaults**:
  - Reduced default `_maxSuspensionDistance` from `0.6m` to `0.25m` (Range `0.02m` to `1.0m`) to match the small scale of the vacuum caster wheels.
  - Reduced default `_restExtensionDistance` from `0.4m` to `0.10m` (Range `0.01m` to `0.8m`) so the body floats slightly off the ground when driving/rolling without unnatural gap height.
  - Reduced default `_groundCheckDistance` to `0.18m` for precise grounding.

### Code Modified/Added
- [MODIFY] [WheelSuspensionController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/WheelSuspensionController.cs) (Updated `OnDrawGizmosSelected` to measure downward extension from joint connectedAnchors on the body base; scaled default range parameters for small caster wheels).

## [2026-08-05] - Procedural Raycast Suspension Refactor (Fluid Physics & Zero Friction)

### Technical Justification & Details
- **Procedural Raycast Suspension Architecture (Industry Standard)**:
  - Replaced physical ConfigurableJoints and child wheel Rigidbodies scraping on the floor with pure Procedural Raycast Spring Suspension.
  - In Unity character controller physics, attaching physical Rigidbodies and Colliders to 4 wheels scraping against the floor causes massive ground friction drag, rotational torque resistance, and joint constraint fighting that destroys movement fluidness and turning performance.
  - With Procedural Raycast Suspension, the main player Rigidbody remains the Single Source of Truth (SSOT) for all horizontal physics and turning. Child wheel Rigidbodies are automatically set to `isKinematic = true` and their physical colliders are converted to triggers (`isTrigger = true`).
- **Spring Force Equation ($F = k \cdot \text{compression} - c \cdot v$)**:
  - Performs 4 downward raycasts from the wheel baseline mount points under the lower body plate.
  - Applies upward spring forces directly to the main body Rigidbody (`_bodyRb.AddForceAtPosition`) when grounded.
  - Procedurally drives the visual local Y positions of the wheel Transforms between 0 (minimal / top position) and `_maxSuspensionDistance` (`0.422m` default) with visual smoothing (`_visualLerpSpeed = 25f`).
- **Dynamic Jump Takeoff & Landing Visuals Preserved**:
  - Retained jump takeoff visual delay (`_jumpRetractDelay = 0.12s`) where visual wheels stretch downward to `_maxSuspensionDistance` during takeoff, then pull up to minimal position (0) mid-air, and compress back to rest height (`0.097m` default) upon landing.

### Code Modified/Added
- [MODIFY] [WheelSuspensionController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/WheelSuspensionController.cs) (Rebuilt with Procedural Raycast Spring Suspension physics, setting child wheel rigidbodies to kinematic and colliders to triggers, eliminating all physical friction and turning resistance while preserving 100% of visual suspension dynamics).

## [2026-08-05] - Zero-Friction Physical Wheel Colliders & Compilation Fix (WheelSuspensionController)

### Technical Justification & Details
- **Physical Wheel Colliders (`isTrigger = false`)**:
  - Restored physical collision (`isTrigger = false`) on all 4 wheel colliders so that wheels physically support the robot on the floor and elevate the body when `_restExtensionDistance` is increased.
- **Dynamic Zero-Friction `PhysicsMaterial`**:
  - Dynamically creates and assigns a `PhysicsMaterial` ("WheelZeroFriction") with `dynamicFriction = 0`, `staticFriction = 0`, and `frictionCombine = Minimum` to all wheel colliders.
  - This allows vertical ground support and elevation while removing 100% of horizontal ground drag and rotational friction, allowing the player to turn on themselves effortlessly.
- **CS1061 Compilation Fix**:
  - Removed invalid `joint.enabled = false` call (since `UnityEngine.Joint` does not inherit from `Behaviour`).

### Code Modified/Added
- [MODIFY] [WheelSuspensionController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/WheelSuspensionController.cs) (Updated `DiscoverAndConfigureWheelJoints` to apply dynamic zero-friction PhysicsMaterial to physical wheel colliders; resolved compilation errors).

## [2026-08-05] - Wheels Angular Y Motion & Counter-Rotation Fix (WheelSuspensionController)

### Technical Justification & Details
- **Root Cause Analysis**:
  - `ConfigurableJoint.angularYMotion` was set to `ConfigurableJointMotion.Locked`, forcing PhysX to override the wheel Rigidbodies' Y rotation to match the body's yaw rotation.
  - This prevented `PlayerLookComponent.cs` from counter-rotating `_wheelsChassisVisual` (`_wheelsChassisVisual.localRotation = _originalWheelsLocalRot * Quaternion.Euler(0f, -parentYaw, 0f)`), causing the wheels and chassis plate to rotate along with the player's look yaw instead of remaining stationary in world orientation.
- **Fix**:
  - Changed `joint.angularYMotion = ConfigurableJointMotion.Free` on all 4 wheel `ConfigurableJoint`s while keeping tilt angles (`angularXMotion` and `angularZMotion`) locked.
  - This allows the wheel chassis plate and wheels to counter-rotate freely on the Y-axis, restoring the stationary chassis base simulation during torso yaw turns.

### Code Modified/Added
- [MODIFY] [WheelSuspensionController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/WheelSuspensionController.cs) (Changed `angularYMotion` to `ConfigurableJointMotion.Free` in `ApplyJointPhysicsConfiguration()` so `PlayerLookComponent` counter-rotation functions unimpeded).

## [2026-08-05] - Elimination of Nested Rigidbody Loop & Pure Procedural Raycast Suspension

### Technical Justification & Details
- **Nested Rigidbody & Joint Feedback Loop Diagnosis**:
  - In Unity PhysX, placing child `Rigidbody` components with `ConfigurableJoint`s under a parent `Transform` (`_wheelsChassisVisual` / `Wheel_FL`) that is being manually rotated via script in `Update()` / `LateUpdate()` (`PlayerLookComponent` counter-rotation and `WheelSteering` caster orientation) creates a circular physics constraint conflict:
    `Body Rigidbody` -> parent of `_wheelsChassisVisual` -> parent of `Wheel Rigidbody` -> connected via `ConfigurableJoint` back to `Body Rigidbody`.
  - This circular loop caused violent physics feedback, spinning wheel visual glitches ("roues qui tournent dans tous les sens"), and broken turning.
- **Single Source of Truth (SSOT) Solution**:
  - `WheelSuspensionController.cs` automatically detects and destroys any child `ConfigurableJoint` or `Rigidbody` components on wheel GameObjects at `Awake()`, eliminating nested Rigidbody loops completely.
  - The root player `Rigidbody` is the single SSOT physics body.
  - Raycast spring forces ($F = k \cdot \text{compression} - c \cdot v$) push upward directly on the root Rigidbody at each wheel mount position, elevating the body physically at rest height (`0.097m` default).
  - `WheelSuspensionController` animates **only** `wheel.localPosition.y` for visual suspension extension/compression. It **never** modifies `wheel.localRotation`.
  - This leaves `PlayerLookComponent` 100% in control of chassis counter-rotation (`_wheelsChassisVisual.localRotation`) and `WheelSteering` 100% in control of caster orientation (`WheelSteering.cs`) with ZERO PhysX interference!

### Code Modified/Added
- [MODIFY] [WheelSuspensionController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/WheelSuspensionController.cs) (Rebuilt to auto-destroy child Rigidbodies/Joints, apply raycast spring forces to root Rigidbody, animate only local Y position, and preserve rotation SSOT for PlayerLookComponent and WheelSteering).

## [2026-08-05] - Automatic Critical Damping & Elevated Raycast Origin Fix (WheelSuspensionController)

### Technical Justification & Details
- **Floor Spawn Clipping Fix (`_rayStartUpOffset = 0.5m`)**:
  - Previously, raycasts originated at `mountOrigin + Vector3.up * 0.02m`. When spawning flat on the ground or clipped 2-3cm into the floor, raycasts originated inside the floor geometry and failed to detect ground, causing wheels to pass through the floor and get stuck.
  - Elevating raycast origins to `mountOrigin + Vector3.up * 0.5m` ensures ground is detected even if the player spawns clipped into the floor. The raycast calculates `distanceToMount = minHitDistance - 0.5m` (negative when clipped), applying immediate upward spring force to pop the player up onto their wheels smoothly at spawn.
- **Infinite Spring Oscillation Elimination (Critical Damping $c = 2\sqrt{m \cdot k}$)**:
  - Added automatic mathematical critical damping calculation based on body mass and spring stiffness per wheel ($c_{\text{crit}} = 2\sqrt{(M_{\text{body}} / 4) \cdot k}$).
  - Dampens downward and upward vertical velocity efficiently, preventing spring force overshooting and guaranteeing 0 infinite bouncing when the robot is resting or landing.

### Code Modified/Added
- [MODIFY] [WheelSuspensionController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/WheelSuspensionController.cs) (Added `_useAutoCriticalDamping`, `_dampingMultiplier`, and `_rayStartUpOffset = 0.5m` to eliminate infinite spring bouncing and ground spawn clipping).

## [2026-08-05] - Base Height Offset Parameter Addition (WheelSuspensionController)

### Technical Justification & Details
- **Base Height Offset (`_baseHeightOffset`)**:
  - Added serialized float parameter `_baseHeightOffset` under Suspension Travel Parameters.
  - Applied `_baseHeightOffset` to all baseline mount points (`initialLocalPos + Vector3.up * _baseHeightOffset`), Raycast spring physics evaluations, visual wheel local Y animations (`baseLocalPos.y + _baseHeightOffset - _animatedWheelY[i]`), and Scene view Gizmos bounds.
  - Allows designers to adjust the overall vertical origin pivot of the suspension system in the Inspector if the model origin pivot is placed too low or too high.

### Code Modified/Added
- [MODIFY] [WheelSuspensionController.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Movement/WheelSuspensionController.cs) (Added `_baseHeightOffset` field and integrated it across physics, visual animation, and Gizmos calculations).









## [2026-08-14] - Strict Yaw Locking & Inspector Override Fix (PlayerV2_Head)

### Technical Justification & Details
- **Inspector Value Override Fix**:
  - The values for `AlignmentTorque` and `AlignmentDamping` were not updating in the Editor Play Mode because Unity preserved the old serialized values from the Prefab.
  - Renamed variables to `HeadAlignmentTorque` and `HeadAlignmentDamping` to force Unity to serialize them as new fields, ensuring the high muscle-like default values (`1500f`, `100f`) are adopted instantly.
- **Strict Yaw/Roll Physics Locking**:
  - Despite the alignment torque, the head was still lagging on the Yaw axis because the underlying `ConfigurableJoint`s were set to `angularYMotion = Limited`, allowing the physics solver to bend the neck during fast Torso rotations.
  - Set `angularYMotion` and `angularZMotion` to `ConfigurableJointMotion.Locked` in `PlayerV2_Head.Start()`.
  - The neck joints now act as strict hinges that only permit Pitch (X-axis) bending. The Head now inherits the Torso's Yaw instantly and immaculately, while remaining physically springy when looking up and down.

### Code Modified/Added
- [MODIFY] [PlayerV2_Head.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/PlayerV2/PlayerV2_Head.cs) (Renamed alignment variables; Locked Y and Z joint motions).

## [2026-08-14] - Eyes Targeting System & Proportional Pitch (PlayerV2)

### Technical Justification & Details
- **Proportional Pitch System**:
  - The head cannot inherit 100% of the input Pitch without clipping its chin or neck into the torso mesh at extreme angles.
  - Implemented `PitchMultiplier` in `PlayerV2_Head.cs` (default 70%) to limit the physical joint rotation to only 70% of the player's camera pitch.
  - Updated `PlayerV2_Look.cs` to apply the remaining 30% local pitch directly to the `CameraTransform`, ensuring the actual aim remains 100% faithful to the mouse input while maintaining a realistic head pose.
- **Eyes & Pupil Targeting (75% / 100%)**:
  - Updated `Eye.cs` to use weighted target interpolation (`_eyeTargetWeight = 0.75f` and `_pupilTargetWeight = 1.0f`).
  - The outer eye mesh now aims 75% toward the target object, while the internal pupil bone aims at 100%. This creates a highly organic, saccadic tracking feel.
- **Gizmos Added**:
  - Added visual debug rays in `PlayerV2_Gizmos.cs`:
    - **Yellow**: Head physical target (70% input).
    - **Red**: Head current physical state.
    - **Cyan**: Camera aim (100% input).
    - **Magenta**: Eye tube aim (75% target tracking).
    - **Green**: Pupil aim (100% target tracking).

### Code Modified/Added
- [MODIFY] [Eye.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Player/Visuals/Eye.cs) (Added Slerp weighting logic and accessors).
- [MODIFY] [PlayerV2_Head.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/PlayerV2/PlayerV2_Head.cs) (Added `PitchMultiplier`).
- [MODIFY] [PlayerV2_Look.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/PlayerV2/PlayerV2_Look.cs) (Compensates for the 70% head pitch by adding the remaining 30% to the camera).
- [MODIFY] [PlayerV2_Gizmos.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/PlayerV2/PlayerV2_Gizmos.cs) (Added Camera, Eye, and Pupil rays).

## [2026-08-14] - Anti-Vomit Camera Fluidity & Gizmos Refactoring (PlayerV2)

### Technical Justification & Details
- **"Anti-Vomit" Camera Smoothing & Anticipation**:
  - Previously, the Camera's rotation was strictly inheriting the Head's physical rotation. Any slight physics-based overshoot or bounce of the Head during fast Torso yaw rotations caused the Camera to bounce identically, inducing motion sickness ("vomito" effect).
  - Modified `PlayerV2_Look.cs` to fully decouple the Camera's *World Rotation* from the Head's physics using a clean, linear slider (`HeadMovementBlend`).
  - At `0.0`, the camera calculates a mathematically pure target look rotation based entirely on mouse input (`targetStable`), completely bypassing the physical head's joint limits and micro-bounces.
  - At `1.0`, the camera is fully attached to the physical bouncing head.
  - **CRITICAL HOTFIX**: Removed the `Time.deltaTime` Slerp smoothing entirely. Because the Camera is physically parented to the Head, using a time-based Slerp caused it to "drag" behind the physics updates, creating an unwanted lag where the camera inherited the head's bounce even at `0.0`, and causing framerate saccades. The camera now explicitly and instantly sets its World Rotation to the interpolated target every `LateUpdate`, providing flawless FPS fluidity.
- **Gizmos Modularization**:
  - Replaced the generic 3 toggles in `PlayerV2_Gizmos.cs` with 7 specific, granular toggles: `ShowViewRangeGizmos`, `ShowVacuumArmGizmos`, `ShowShooterArmGizmos`, `ShowHeadAnglesGizmos`, `ShowEyesGizmos`, `ShowCameraAnticipationGizmos`, and `ShowSuspensionGizmos`.
  - The Camera's anticipated view vector is now independently visualizable to tune the smoothing values.

### Code Modified/Added
- [MODIFY] [PlayerV2_Look.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/PlayerV2/PlayerV2_Look.cs) (Added Slerp smoothing, Anticipation multipliers, and World Space rotation decoupling).
- [MODIFY] [PlayerV2_Gizmos.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/PlayerV2/PlayerV2_Gizmos.cs) (Expanded Gizmo toggles into highly granular inspector parameters and added Anticipation rendering).

















# #   2 4   A o u t   2 0 2 6   -   M u l t i j o u e u r   &   S y n c h r o n i s a t i o n 
 -   F i x   [ P l a y e r V 2 _ D y n a m i c C r o s s h a i r . c s ]   :   M a s q u a g e   d u   H U D   p o u r   l e s   c l i e n t s   d i s t a n t s . 
 -   F i x   [ P l a y e r V 2 _ C o n t r o l l e r . c s   /   P l a y e r V 2 _ M o v e m e n t . c s ]   :   A j o u t   d ' u n e   S y n c V a r   I s C r o u c h i n g   p o u r   d i f f u s e r   l ' e t a t   d ' a c c r o u p i s s e m e n t   a u x   a u t r e s   c l i e n t s ,   d e c l e n c h a n t   l a   p h y s i q u e   d e   l a   t e t e . 
 -   F i x   [ P l a y e r V 2 _ A r m s . c s   /   P l a y e r V 2 _ L o o k . c s ]   :   A j o u t   S y n c V a r   p o u r   l e   P i t c h   d e   l a   c a m e r a   e t   l ' e x t e n s i o n   d e s   b r a s ,   p e r m e t t a n t   a   l ' I K   B e z i e r   d e   t o u r n e r   l o c a l e m e n t   c h e z   t o u t   l e   m o n d e . 
 -   F i x   [ M o u t h A n i m a t o r . c s ]   :   F a l l b a c k   r e s e a u   a v e c   u n   S y n c V a r   d e   v o l u m e ,   p o u r   f o r c e r   l ' a n i m a t i o n   d e   l a   b o u c h e   d u   h o s t   m e m e   s i   l ' i d   U n i V o i c e   b u g .  
 
## [2026-08-24] - Multiplayer Arm Curvature Fix (PlayerV2_Look)

### Technical Justification & Details
- **Arm Curve Exaggeration Bug (Remote Clients)**:
  - In multiplayer, the arm curvature (Bezier physics in PlayerV2_Arms.cs) was heavily exaggerated for remote clients (looking like an elephant scratching).
  - **Root Cause**: PlayerV2_Arms.cs uses the Camera's orward vector to aim the arms. On remote clients, PlayerV2_Look.cs was applying SyncPitch as a localRotation directly to the Camera. However, since the Camera is parented to the physically-simulated Head (which already bends by 70% pitch due to PlayerV2_Head.cs), the localRotation stacked on top of the physical rotation, resulting in a **170% total pitch angle**.
  - This caused the Camera's forward vector to point wildly up or down, sending the arms' control points into extreme trajectories.
- **Fix**: Removed the localRotation override in OnSyncPitchChanged. LateUpdate() now evaluates the **World Rotation** of the camera for ALL clients, correctly combining the physical head's orientation and the remaining mathematical pitch based on SyncPitch and _controller.IsCrouching for remote players. The camera's forward vector is now perfectly identical on all clients, ensuring consistent arm physics.

### Code Modified/Added
- [MODIFY] [PlayerV2_Look.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/PlayerV2/PlayerV2_Look.cs) (Enabled LateUpdate for remote clients, correctly Slerping World Rotation from synced variables).

## [2026-08-24] - Multiplayer Wheel Suspension Lag Fix (PlayerV2_Suspension)

### Technical Justification & Details
- **Wheel Visual Lag (Remote Clients)**:
  - In multiplayer, the remote player's Hips are translated via NetworkTransform (interpolation), which is non-physical. The wheel Rigidbodies, attached via ConfigurableJoints, relied on the physics solver to catch up to the moving anchor. Because the solver uses iterations to resolve the Locked X/Z axes, the wheels were visually left trailing behind the main body during movement.
- **Fix**:
  - Enabled JointProjectionMode.PositionAndRotation with a projectionDistance of  .02f (2cm) on the wheels' ConfigurableJoints.
  - This forces the physics engine to instantly snap/teleport the wheels horizontally under the robot if they start drifting due to network interpolation, completely eliminating the visual disconnect while preserving the vertical (Y) limited suspension bounce.

### Code Modified/Added
- [MODIFY] [PlayerV2_Suspension.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/PlayerV2/PlayerV2_Suspension.cs) (Added Joint Projection configuration in ApplySuspensionSettings).
