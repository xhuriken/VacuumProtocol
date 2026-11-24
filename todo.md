# TODO - Résolution du Drift de Mouvement

- [x] Analyse de la mécanique physique du PlayerV2 (Hips, Joints, Forces).
- [x] Identification de la racine du problème de dérive et de ralentissement (Collisions entre Bras et Torso B).
- [x] Comprendre l'échec de l'application de la 3ème Loi de Newton (Le `connectedMassScale` bloquait le transfert de force, causant une asymétrie de la force opposée).
- [x] Modification de `PlayerV2_CollisionManager.cs` pour ajouter les règles d'ignorance de collision entre les Bras et Torso B.
- [x] Mise à jour du `DEVELOPMENT_LOG.md`.
- [x] Explication claire au joueur.

- [x] Correction de la courbure exag�r�e des bras en multijoueur (suppression de l'addition des rotations locales/physiques de la cam�ra pour les clients distants).

- [x] Correction du d�calage (lag) visuel des roues en multijoueur via l'ajout de la Projection Physique (JointProjectionMode).

- [x] Saccades des yeux appliqu�es sur les entit�s cibl�es + Animation de peur (Priority >= 3) avec tremblements et r�tr�cissement de la pupille (synchronis� en r�seau).

- [x] Intelligence des saccades : Rayon proportionnel � la distance de la cible pour inspecter visuellement les objets (Priority 2) sans regarder dans le vide.

- [x] Correction de l'animation de peur : C'est d�sormais l'�il complet qui grossit (�carquillement) au lieu de la pupille qui r�tr�cit.

## Phantoms (Ennemis IA)
- [x] Architecture de base du système Phantom (PhantomBase.cs + IEntity avec PriorityLevel 3 pour déclencher la peur des robots).
- [x] Contrôleur standard au sol sur NavMesh (PhantomStandard.cs avec gestion multi Mirror et désactivation de l'agent côté client).
- [x] Effet visuel de flottaison / lévitation organique (PhantomFloatingVisual.cs).
- [ ] Création du Prefab Phantom dans Unity et configuration sur le NetworkManager.
- [ ] Bake du NavMeshSurface sur la map de jeu (ex: AmongUsMap.fbx).
- [ ] Implémentation des variantes futures (Téléportation, Traversée de surfaces).

## Dette technique (à corriger)
- [ ] **Double synchro réseau du pitch (PlayerV2_Look / PlayerV2_Head)** : `PlayerV2_Look.SyncPitch` + `CmdSetPitch` (seuil 1°) et `PlayerV2_Head.CurrentTargetPitch` + `CmdSetPitch` (seuil 0.5°) envoient chacun un Command + SyncVar pour quasi la même valeur (Head = pitch Look × `PitchMultiplier`, ou 0 accroupi). Identifier lequel pose problème (caméra / tête distante) puis faire une source unique (SSOT) : un seul pitch synchronisé, l'autre dérivé localement.
