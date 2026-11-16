# Nexus Defense

**Nexus Defense** is a multiplayer cooperative tower-defense game developed in Unity 6. Players choose unique character classes, defend their base against waves of enemies, build defensive structures, and upgrade their abilities to survive increasingly difficult encounters.

## 🎮 About the Game

Nexus Defense combines cooperative survival gameplay with class-based progression, strategic building, and networked multiplayer.

Players can specialize in different roles, contribute to the team's defense, and adapt their strategies as enemy waves become more challenging.

## 🧙 Character Classes

The game features three playable classes:

- **Mage** — Uses spells and magical abilities to fight enemies. Players can upgrade their spells and customize their spell loadout.
- **Gunner** — Specializes in ranged combat using weapons with upgradeable stats and progression.
- **Builder** — Constructs defensive structures and turrets to protect the team and control the battlefield.

Each class has its own mechanics and progression system.

## ⚔️ Core Features

- Cooperative multiplayer gameplay.
- Host-based networking.
- Character class selection.
- Class-specific mechanics and progression.
- Wave-based enemy spawning.
- Enemy AI and navigation.
- Upgradeable weapons and spells.
- Building and turret placement.
- Networked structures and projectiles.
- Shared player upgrade currency.
- Wave voting system.
- Multiplayer lobby with player readiness and match start management.

## 🛠️ Technologies

| Technology | Purpose |
|---|---|
| Unity 6 | Game engine |
| C# | Gameplay programming |
| Netcode for GameObjects | Multiplayer networking |
| Unity Relay | Network connectivity |
| Unity Authentication | Player authentication |
| Unity Input System | Player input |
| Unity AI Navigation | Enemy navigation |
| ScriptableObjects | Game data and configuration |
| TextMeshPro | UI text rendering |

## 🏗️ Architecture Highlights

The project focuses on modular and network-aware gameplay systems.

- **Class Mechanic System** — Initializes and manages mechanics depending on the selected character class.
- **Network Authority** — Important gameplay operations are validated and executed on the server.
- **Progression System** — Shared player currency is combined with class-specific upgrade systems.
- **Wave Manager** — Controls enemy spawning, wave progression, rewards, and voting.
- **Building System** — Handles building placement, validation, purchasing, upgrades, and network spawning.
- **Lobby System** — Manages player registration, class selection, readiness, and match startup.

## 🚀 Project Status

**In Development**

The project is actively being developed. Gameplay systems, multiplayer functionality, progression, UI, and balancing are being implemented and improved.

## 📷 Screenshots & Gameplay

Screenshots and gameplay footage will be added as development progresses.

## 👨‍💻 Developer

Developed as a personal game development project to explore and improve skills in:

- Unity game development.
- C# programming.
- Multiplayer game architecture.
- Server-authoritative networking.
- Gameplay systems and modular architecture.
- Game mechanics and progression design.

---

*Thank you for checking out Nexus Defense!*
