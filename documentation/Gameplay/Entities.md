# Entités et Gameplay (Entities)

Ce document définit les interactions centrales entre les objets physiques du monde et le système de détection/aspiration du joueur.

## 1. Philosophie et Conception

Le projet repose sur l'interface standardisée `IEntity` qui rend tout objet "détectable". Cela permet aux systèmes de ciblage visuel (eye-tracking) et à l'aspirateur d'interagir de manière unifiée avec des objets très divers (autres joueurs, items, débris, etc.).

---

## 2. L'Interface IEntity ([IEntity.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Core/IEntity.cs))

C'est le contrat de base que tout objet interactif doit respecter.
* **`Name`** : Le nom affiché de l'objet (utile pour les futurs labels UI).
* **`PriorityLevel`** : Un entier utilisé pour le tri visuel. Si le joueur regarde dans une direction contenant plusieurs objets, celui avec la priorité la plus haute (ex: 2 pour un item de quête vs 1 pour un débris) recevra le focus.
* **`gameObject`** : Accès direct au GameObject Unity sous-jacent.
* **`LookAtPoint`** : Le point spatial précis vers lequel la tête du joueur doit se tourner. Par défaut, c'est la racine du Transform, mais cela peut être surchargé pour cibler un visage ou un point d'accroche spécifique.

---

## 3. Implémentation : Collectible ([Collectible.cs](file:///c:/Users/celestin/Unity%20Games/VacuumProtocol/Assets/1_Scripts/Gameplay/Collectible.cs))

Le script `Collectible` est l'implémentation concrète de base pour les objets de l'environnement qui peuvent être manipulés physiquement ou aspirés.

### A. Propriétés Physiques et Aspiration
* **Résistance à l'Aspiration (`PullResistance`)** : Cette variable permet de décorréler la masse physique de l'objet (utilisée par Unity) de sa résistance à la force de l'aspirateur. Ainsi, on peut avoir un objet très lourd qui roule difficilement mais s'aspire facilement, ou un petit objet "collé" au sol qui nécessite une grande force d'aspiration.
* **Mise en cache du Rigidbody (`Rb`)** : Pour optimiser les calculs dans les boucles `FixedUpdate` (notamment lors de l'aspiration), la référence au `Rigidbody` est mise en cache dès le `Awake()`.

### B. Gestion des Déformations Visuelles (Shrinking)
Lorsqu'un `Collectible` s'approche de la buse de l'aspirateur, il subit une déformation visuelle (réduction d'échelle) pour donner l'illusion qu'il s'engouffre dans le tube.
* **Mise en cache de l'échelle (`OriginalScale`)** : L'échelle d'origine de l'objet est sauvegardée au démarrage.
* **Récupération (`ResetScale()`)** : Si l'objet sort de la zone d'aspiration (par exemple si le joueur arrête d'aspirer avant que l'objet n'atteigne le point d'absorption), cette méthode est appelée pour lui redonner immédiatement sa taille normale, évitant ainsi des déformations permanentes dans le niveau.
