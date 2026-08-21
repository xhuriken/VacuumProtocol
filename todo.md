# TODO - Résolution du Drift de Mouvement

- [x] Analyse de la mécanique physique du PlayerV2 (Hips, Joints, Forces).
- [x] Identification de la racine du problème de dérive et de ralentissement (Collisions entre Bras et Torso B).
- [x] Comprendre l'échec de l'application de la 3ème Loi de Newton (Le `connectedMassScale` bloquait le transfert de force, causant une asymétrie de la force opposée).
- [x] Modification de `PlayerV2_CollisionManager.cs` pour ajouter les règles d'ignorance de collision entre les Bras et Torso B.
- [x] Mise à jour du `DEVELOPMENT_LOG.md`.
- [x] Explication claire au joueur.

