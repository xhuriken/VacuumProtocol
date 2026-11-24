# VacuumProtocol

Jeu coop multijoueur Unity 6 (6000.3.10f1, URP 17.3). Robots-aspirateurs physiques, phantoms IA à aspirer.
Langue : français pour les docs `documentation/`, les commits et `DEVELOPMENT_LOG.md`. **Anglais pour tout le code** : commentaires, `/// <summary>` (Description / Context / Justification), `[Tooltip("Role: ...")]`, messages de log.

@.agents/AGENTS.md

## Stack
- Réseau : Mirror + FizzySteamworks (Steam P2P), kcp2k. Voix : UniVoice/Concentus. Test multi-instance : ParrelSync.
- Libs : Odin Inspector, Febucci TextAnimator, Shapes, HierarchyDecorator, ProBuilder, AI Navigation, Input System.
- Code jeu : `Assets/1_Scripts/` (Player, PlayerV2, Gameplay/Phantoms, Networking, Mechanics, UI, Audio, Core). Pas d'asmdef : tout compile dans `Assembly-CSharp`.
- Scène principale : `Assets/3_Scenes/MECHA.unity`.
- Docs techniques : `documentation/` (par domaine). Historique : `DEVELOPMENT_LOG.md`.

## Piloter l'Editor (Unity CLI + package com.unity.pipeline)
L'Editor doit être ouvert. Préférer la CLI au MCP (moins de tokens).
- `unity status` : Editor connecté ?
- `unity recompile` : compile et renvoie les erreurs. **Obligatoire après toute modif C#** avant de dire « fini ».
- `unity list` / `unity command` : outils exposés par l'Editor (console, scène, play mode, capture...).
- `unity test` : tests EditMode/PlayMode.
- `unity docs <Classe>` : doc Unity à la bonne version.
Skill `unity:using-unity` (plugin officiel) pour les bonnes pratiques Unity.

## Règles
- Mirror : logique d'autorité côté serveur (`[Server]`, `[Command]`), visuels côté client. Vérifier `isServer` / `isLocalPlayer` / `isOwned` avant d'agir. Les SyncVar ne changent que côté serveur.
- Ne pas éditer à la main `.unity` / `.prefab` sauf petit changement ciblé : passer par l'Editor (`unity command`). Ne jamais toucher `Library/`, `Temp/`, `obj/`, ni les `.csproj`/`.sln` (générés).
- Chaque nouvel asset a son `.meta` : ne jamais le supprimer ni le régénérer à la main.
- Ne pas modifier les packages tiers (`Assets/Mirror`, `Assets/Plugins`, `Assets/Shapes`, `Assets/ParrelSync`...).
- Après une feature : entrée dans `DEVELOPMENT_LOG.md` (format Goal / Changes / Justification) et doc dans `documentation/` si système nouveau.
- Commits : conventional commits (`feat(scope): ...`, `fix(scope): ...`) avec un simple `git commit -m "..."`. Aucune ligne `Co-Authored-By` ni mention d'IA.
- Branches : une branche par sujet (`feat/...`, `fix/...`, `refactor/...`, `chore/...`), mergée ensuite dans la branche parente.
