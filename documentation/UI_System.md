# Documentation : Système UI (Interface Utilisateur)

Ce document décrit l'architecture et le fonctionnement des composants d'interface utilisateur du projet Vacuum Protocol. Le système UI est conçu pour offrir une expérience premium, interactive et fluide, en s'appuyant fortement sur des formes vectorielles et des animations procédurales.

## 1. Architecture Visuelle et Vectorielle (Shapes Integration)

L'UI repose de manière significative sur la bibliothèque **Shapes** de Freya Holmér pour générer des éléments vectoriels nets (lignes, rectangles, disques) qui réagissent dynamiquement aux interactions de l'utilisateur.

### 1.1 CustomTextButton
- **Rôle** : Bouton complexe contenant du texte (`TextMeshPro`) et des décorations géométriques (Lignes, Rectangles, Disques).
- **Fonctionnement** :
  - Intègre des animations `DOTween` haute-fidélité pour les états de survol (Hover), de clic (Down) et de relâchement (Up).
  - Inclut des sécurités "anti-spam" en annulant proprement les tweens en cours avant d'en démarrer de nouveaux.
  - S'intègre avec `Febucci.UI` pour déclencher des effets de machine à écrire sur le texte.

### 1.2 ColorButtonUI
- **Rôle** : Bouton de sélection de couleur (utilisé principalement dans le lobby).
- **Fonctionnement** :
  - Composé d'un contour animé et d'un carré intérieur coloré.
  - **Attraction Magnétique** : Calcule la proximité de la souris et attire doucement le carré intérieur vers le curseur pour une sensation organique et premium.
  - Dispose d'outils de diagnostic (`RunDiagnosticTracker`) pour tracer les raycasts UGUI bloqués.

### 1.3 UIColorsPalettes
- **Rôle** : Gestionnaire de palette de couleurs pour le lobby.
- **Fonctionnement** : 
  - Maintient une liste de 16 couleurs.
  - Assigne automatiquement les couleurs aux `ColorButtonUI` enfants et lie leurs actions de clic au système de customisation réseau.
  - Contient un utilitaire de génération de gradient quantifié (Niveaux de gris + spectre HSV) accessible depuis l'inspecteur.

### 1.4 UICustomButtonBase
- **Rôle** : Classe de base pour les boutons personnalisés.
- **Fonctionnement** :
  - Centralise l'implémentation des interfaces `IPointer` d'Unity EventSystem (`IPointerEnterHandler`, etc.).
  - Gère l'état d'interactivité (`Interactable`) et fournit un hook virtuel `OnInteractableChanged` pour permettre aux classes dérivées d'animer leur état grisé.

## 2. Navigation et Menus

Le système de menus permet d'empiler des fenêtres et de gérer proprement les ouvertures et fermetures avec des transitions fluides.

### 2.1 UIPanelController
- **Rôle** : Contrôleur d'animation pour les panneaux d'interface.
- **Fonctionnement** :
  - Utilise un `CanvasGroup` pour modifier l'alpha et bloquer les raycasts.
  - Anime l'apparition/disparition avec `DOTween` (Fade et Scale avec un effet *Bounce*).
  - Expose des événements `OnPanelOpened` et `OnPanelClosed`.

### 2.2 UINavigationGroup
- **Rôle** : Gestionnaire de groupe de panneaux mutuellement exclusifs (ex: Main, Settings, Credits).
- **Fonctionnement** :
  - Maintient une pile (`Stack`) de l'historique de navigation.
  - La méthode `GoBack()` permet de fermer le sous-menu actuel et de revenir au précédent proprement.

### 2.3 InGameMenuController
- **Rôle** : Intercepteur de touche `Échap` (Escape) en jeu.
- **Fonctionnement** :
  - Gère la fermeture hiérarchique : si le panneau des paramètres est ouvert, `Échap` le ferme en premier au lieu de fermer tout le menu de pause.

### 2.4 OpenURLButton
- **Rôle** : Raccourci utilitaire pour ouvrir des liens externes.
- **Fonctionnement** : Se connecte automatiquement au `Button` UGUI attenant et appelle `Application.OpenURL()`.

## 3. Utilitaires et Entrées (Souris / Volumes)

### 3.1 MouseManager
- **Rôle** : Point d'accès global (Singleton) pour les coordonnées et l'état de la souris.
- **Fonctionnement** :
  - Encapsule le *New Input System* pour obtenir la position de la souris.
  - Détermine si le curseur doit être visible en fonction du `Cursor.lockState`.
  - Masque le curseur matériel (`Hardware Cursor`) si l'option est activée (pour le remplacer par le curseur personnalisé).

### 3.2 CustomCursorFollower
- **Rôle** : Curseur personnalisé dessiné par le Canvas.
- **Fonctionnement** :
  - Suit les coordonnées fournies par `MouseManager`.
  - S'adapte au mode de rendu du Canvas (`ScreenSpaceOverlay` vs `ScreenSpaceCamera`) pour garantir un positionnement au pixel près.

### 3.3 SettingsUIPresenter
- **Rôle** : Pont entre l'interface utilisateur des paramètres et le `SettingsManager`.
- **Fonctionnement** :
  - Lie les curseurs (Sliders) et listes déroulantes (Dropdowns) aux paramètres de volume (Général, Voix, Sensibilité du micro).
  - Inclut un **Indicateur de Volume RMS en direct** pour le micro, qui s'abonne aux trames audio brutes `UniVoice` et met à jour une jauge visuelle avec lissage.
  - Gère le basculement du mode "Auto VAD" (Voice Activity Detection), désactivant la jauge manuelle si l'algorithme automatique est sélectionné.

### 3.4 PlayerVolumeSlider
- **Rôle** : Curseur de volume individuel pour chaque joueur (affiché dans le lobby).
- **Fonctionnement** :
  - Utilise l'`ID Steam64` comme clé de persistance pour sauvegarder les réglages d'une session à l'autre.
  - Utilise le `ConnectionId` Mirror au runtime pour appliquer immédiatement le multiplicateur au composant audio `UniVoice` de la cible.
  - Se désactive et se masque automatiquement si la carte joueur correspond au joueur local.
