# SWISH 🏀

**Swish** is a one-touch street basketball arcade game built with Unity and C#.
Drag to aim, release to shoot, and chase perfect swishes as the hoop starts moving
and the day slowly fades into night.

▶ **Play in your browser:** https://romaisamustabshirah.github.io/Swish/

## How to Play
- **Drag** back from the ball to aim, **release** to shoot.
- **Swish** (nothing but net) = **2 points**
- **Rim shot** (touches rim or board) = **1 point**
- Miss and you lose a life. You have **3 lives**.

## Features
- **Moving hoop:** after 5 points the hoop starts drifting, and it gets wider and faster as your score climbs.
- **Day-to-night progression:** the court moves from afternoon to sunset to a starlit night as you score.
- **Custom ball physics:** rim, backboard, wall and floor bounces with a reactive net.
- **Ball shop:** unlock 12 patterned ball skins with coins.
- **Game feel:** score popups, screen shake, sound effects and background music (toggleable).
- **Best score** saved between sessions.
- Mobile-friendly touch controls and a WebGL build.

## Built With
- Unity (Universal Render Pipeline)
- C#
- WebGL + GitHub Pages

## Project Structure
| Script | Purpose |
|---|---|
| `SwishGame.cs` | Core game loop, physics, scoring, UI |
| `CourtView.cs` | Court, hoop and ball rendering |
| `BackDrop.cs` | Animated day/night sky and scenery |
| `Store.cs` | Ball skins, coins and unlocks |
| `Cfg.cs` | Game constants and colour theme |
| `Fx.cs` / `Audio.cs` / `SwishMusic.cs` | Effects, sound and music |

## Developer
Designed and developed by **Romaisa Mustabshirah**.

## Status
Playable. Still being polished.
