# TODO

- [x] Planifier la refonte physique V2 avec l'approche Multi-Body (Hips + Torso séparés).
- [ ] Créer les scripts V2 (`PlayerV2_Controller`, `PlayerV2_Look`, `PlayerV2_Movement`, `PlayerV2_Suspension`).
- [ ] Guider l'utilisateur pour la configuration du Prefab V2 dans Unity (Assignation des Rigidbody, Joints).
- [ ] Tester le comportement physique de base (gravité, suspension).
- [x] Tester le mouvement et la rotation (Tourelle) - Bugs physiques résolus (FreezeRotation, friction, vitesse max).
- [x] Simplifier le script Wheels.cs (KISS) suite au bug du lock angularYMotion de la suspension.
- [x] Implémenter la tête/cou physique (ragdoll + piloté) avec `PlayerV2_Head.cs`.
- [x] Fix Head drift/offset contre la rotation du Torso en appliquant une force d'alignement.
- [x] Implémenter le ciblage des yeux (75%), pupilles (100%) et la répartition du pitch (Tête 70%, Caméra 100%).
- [x] Fix effet vomito avec lissage et anticipation de la caméra ; refonte des toggles Gizmos.
- [x] Ajouter le paramètre de Speed pour le smooth slerp des pupilles.
- [x] Faire cibler le point central regardé par la caméra quand les yeux n'ont aucune cible précise.
- [x] Remplacer JumpForce par ForceMode.VelocityChange pour un feeling instinctif indépendant de la masse.
- [x] Ajouter l'animation procédurale de rétractation asynchrone des roues au saut.
- [x] Implémenter le multiplicateur de gravité en chute (Hollow Knight style).
- [x] Adapter le code UniVoice et MouthAnimator pour le Player_V2 (ConnectionId, InputHandler 2-clics, Suivi 3D des Hips, Correction Scale Bouche).
- [x] Corriger le suivi de pitch des yeux (utilisation de la rotation directe de la caméra pour contrer le multiplicateur de la tête).
