<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE" />
  <h2><strong>NoBlock</strong></h2>
  <h3>Global noblock for X seconds on command + grenade noblock + ladder support</h3>
</div>

<p align="center">
  <img src="https://img.shields.io/badge/version-1.0.0-blue" alt="Version">
  <img src="https://img.shields.io/github/license/SyntX/NoBlock" alt="License">
</p>

## Features

- **Global NoBlock Command** - Players can use `!noblock` to enable noblock for themselves and all other players for a configurable duration
- **Grenade NoBlock** - All grenade types pass through players globally:
  - HE Grenade
  - Flashbang
  - Smoke Grenade
  - Molotov / Incendiary
  - Decoy
- **Ladder Support** - Players on ladders automatically receive noblock until they leave the ladder
- **Cooldown System** - Prevents spam with a configurable cooldown between uses

## Usage

- Type `!noblock` in chat to activate global noblock for the configured duration

## Configuration

Edit `config/plugins/NoBlock/config.jsonc` to customize:

| Setting | Default | Description |
|---------|---------|-------------|
| `HEGrenade` | true | Enable HE grenade noblock globally |
| `Flashbang` | true | Enable flashbang noblock globally |
| `SmokeGrenade` | true | Enable smoke grenade noblock globally |
| `Molotov` | true | Enable molotov/incendiary noblock globally |
| `Decoy` | true | Enable decoy grenade noblock globally |
| `NoBlockTimer` | 5.0 | Duration of noblock in seconds when command is used |
| `NoBlockCooldownTimer` | 10.0 | Cooldown in seconds before command can be used again |
| `Ladder` | true | Enable noblock while on ladders |
| `ChatPrefix` | ` [NoBlock] ` | Prefix for plugin chat messages |

## Author

**SyntX34**

## Building

```bash
dotnet build
```

## Publishing

```bash
dotnet publish -c Release
```