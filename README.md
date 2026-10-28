<h1 align="center">Match & Flame</h1>

<p align="center">
  <img src="https://img.shields.io/badge/Made%20with-Unity%206-black?style=for-the-badge&logo=unity" alt="Made with Unity 6">
  <img src="https://img.shields.io/badge/Platform-Android%20%7C%20WebGL-blue?style=for-the-badge" alt="Platform">
  <img src="https://img.shields.io/badge/Genre-Puzzle-brightgreen?style=for-the-badge" alt="Genre">
  <img src="https://img.shields.io/badge/License-MIT%20(code)-lightgrey?style=for-the-badge" alt="License">
</p>

<p align="center">
  <b>Match & Flame</b> is a relaxing but challenging puzzle game: arrange the matches so that one spark sets off a chain reaction that burns the whole board.<br>
  <i>Головоломка, где нужно расставить спички так, чтобы одна искра запустила цепную реакцию и сожгла всё поле.</i>
</p>

<p align="center">
  <a href="https://play.google.com/store/apps/details?id=com.aixdev.matchandflame">Google Play</a> ·
  <a href="https://github.com/ars6nlp/Match_and_Flame">Source code</a>
</p>

<hr>

## Gameplay

**[EN]**
1. **Drag & drop** your match onto the wooden board.
2. **Tap** to rotate it by 90°.
3. Fire spreads only from the *head* of a burning match to the *tail* of the next one — line them up precisely.
4. **Ignite** and watch the flame run across the level.

**[RU]**
1. **Перетащите** спичку на игровое поле.
2. **Нажмите** на неё, чтобы повернуть на 90°.
3. Огонь переходит только от *головки* горящей спички к *хвосту* следующей — выстраивайте их точно.
4. **Подожгите** и смотрите, как пламя проходит весь уровень.

## Features

- **Hand-made levels** with golden matches (coins), moving obstacles, water drops, fans and immune matches.
- **The Hearth** — a home for your little flame: feed it embers and it grows from a spark to a sun, with outfits, room decor, a matchbox collection, daily tasks, a weekly goal and holiday events.
- **Daily level** — a new puzzle every day with its own reward.
- **Shop** with match skins bought for in-game coins (no paid purchases).
- **RU / EN** localization, music and SFX toggles, effects quality setting.
- **Juicy burning** — procedural flame, sparks, smoke and ash, door transitions between scenes.

## Tech

- **Engine:** Unity 6 (6000.2), C#, Universal Render Pipeline 2D.
- **UI:** uGUI + TextMeshPro; the Hearth screens are built from code (`Assets/Scripts/Retention/`).
- **Art pipeline:** procedural sprites drawn with Python + Pillow (`Tools/`), packed into a sprite atlas by editor tools.
- **Ads:** Google Mobile Ads + UMP consent (Android). **WebGL:** PluginYourGames 2 for Yandex Games.
- **Editor tooling:** level lab and simulator, UI layout auditor, play-mode checks, release build with pre-checks (`Assets/Editor/`).

## Project structure

| Path | What |
|---|---|
| `Assets/Scripts/` | game code: levels, matches, burning, shop, ads, localization |
| `Assets/Scripts/Retention/` | the Hearth: daily level, tasks, collection, flame character, UI |
| `Assets/Levels/`, `Assets/Resources/` | level prefabs, catalogs, settings assets |
| `Assets/Art/`, `Assets/Sounds/`, `Assets/Videos/` | graphics, audio, menu video |
| `Assets/Editor/` | editor tools (level lab, UI audit, release build) |
| `Tools/` | Python scripts that generate procedural art |
| `docs/` | game design notes and the build guide |

## Building

See **[docs/BUILD.md](docs/BUILD.md)**. In short: open the project in Unity 6000.2.13f1, import Google Mobile Ads for Unity, copy `BuildSetup/AdIds.Example.cs` to `Assets/Scripts/AdIds.cs`, and use your own keystore and privacy policy URL.

## License

- **Code** (C# scripts, editor tools, Python tools) — [MIT](LICENSE).
- **Graphics, sounds, music and video** (`Assets/Art/`, `Assets/Sounds/`, `Assets/Videos/` and other art assets) are **not** covered by the MIT license unless they come from a free source with its own license. Treat them as **all rights reserved**: do not reuse them in your own projects without permission.
- Third-party packages keep their own licenses: PluginYourGames 2 (CC0, `Assets/PluginYourGames/LICENSE.txt`), External Dependency Manager (Apache 2.0), TextMesh Pro and Unity packages (Unity Companion License).

The name *Match & Flame* and the published Google Play listing belong to their owner; forks must use their own package name, keystore and store listing.
