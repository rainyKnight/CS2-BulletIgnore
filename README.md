# CS2-BulletIgnore

**English** | [简体中文](README.zh-CN.md)

A **ModSharp** plugin for CS2 that prevents human players standing in the line of fire from causing additional bullet penetration damage loss against zombies behind them.

## Features

- Removes CT player pawns from the bullet hit chain.
- Repairs the starting surface of subsequent hit segments so human flesh material is no longer used for penetration calculations.
- Preserves normal penetration processing for walls, props, and other targets.
- Does not compensate through damage multipliers or globally increase weapon damage.
- Provides an enable switch and runtime statistics, with no database or additional resource packs required.

## How It Works

The plugin intercepts the native `FireBullet` path after the hit chain is built and before penetration and damage are calculated. It removes the player hits to be ignored and repairs the remaining segments, allowing the engine to continue processing targets behind those players without the additional human-body penetration loss.

The design rationale behind the hit-chain compaction and segment repair is documented in [docs/THEORY.md](docs/THEORY.md) (Simplified Chinese).

## Scope

This version targets modes where **humans are always CT and zombies are always T**.

The implementation filters CT player pawns without comparing the shooter's team with the target's team. It is therefore not a general-purpose implementation of teammate pass-through for both teams.

## Requirements

| Component | Requirement |
|---|---|
| Server platform | Linux x64 |
| Framework | ModSharp 2.1.169 |
| Runtime | .NET 10 |

The plugin depends on version-specific native signatures and structure layouts. Compatibility must be checked after CS2 updates. Hook installation is refused unless the signature matches exactly once. See the [native compatibility notes](docs/NATIVE-COMPATIBILITY.md) (Simplified Chinese) for implementation details.

## Installation

Merge the `sharp/` directory from the plugin archive into the server's `game/sharp/` directory, preserving any existing configuration:

```text
game/sharp/
├── modules/
│   └── BulletIgnore/
│       ├── BulletIgnore.dll
│       └── BulletIgnore.deps.json
└── configs/
    └── bullet-ignore.json
```

Load the plugin using ModSharp's standard module loading procedure.

Do not enable it alongside another plugin that hooks the same native location, including the original ZM integration if its pass-through feature is still enabled.

## Configuration

Configuration file: `game/sharp/configs/bullet-ignore.json`.

Disabled by default:

```json
{
  "enabled": false,
  "ct_only_policy_acknowledged": false
}
```

| Option | Description |
|---|---|
| `enabled` | Enables the feature |
| `ct_only_policy_acknowledged` | Acknowledges that this version specifically filters CT players |

Example with the feature enabled:

```json
{
  "enabled": true,
  "ct_only_policy_acknowledged": true
}
```

Configuration is read when the module loads. Reload the module after making changes. Missing configuration leaves the feature disabled; invalid configuration or a missing CT-only acknowledgment prevents it from being enabled.

## Building

Requires .NET SDK **10.0.301**:

```sh
dotnet build CS2-BulletIgnore.slnx -c Release --disable-build-servers --maxcpucount:1
dotnet test CS2-BulletIgnore.slnx -c Release --disable-build-servers --maxcpucount:1
python scripts/package.py
```

Output is written to `artifacts/`:

- `CS2-BulletIgnore-0.1.0-source.zip`: source archive.
- `CS2-BulletIgnore-0.1.0-plugin.zip`: plugin archive with the sample configuration disabled by default.
- `SHA256SUMS`: file checksums.

## Support

If this project is useful to you, consider supporting its development and maintenance. Sponsorship is entirely optional and does not affect your right to use the project.

Scan a QR code with the corresponding payment app. Click an image to view it at full size.

<table>
  <tr>
    <th align="center">WeChat Pay · 1</th>
    <th align="center">WeChat Pay · 2</th>
  </tr>
  <tr>
    <td align="center"><a href="assets/sponsorship/wechat-1.jpg"><img src="assets/sponsorship/wechat-1.jpg" width="260" alt="WeChat Pay 1 sponsorship QR code"></a></td>
    <td align="center"><a href="assets/sponsorship/wechat-2.jpg"><img src="assets/sponsorship/wechat-2.jpg" width="260" alt="WeChat Pay 2 sponsorship QR code"></a></td>
  </tr>
  <tr>
    <th align="center">Alipay · 1</th>
    <th align="center">Alipay · 2</th>
  </tr>
  <tr>
    <td align="center"><a href="assets/sponsorship/alipay-1.jpg"><img src="assets/sponsorship/alipay-1.jpg" width="260" alt="Alipay 1 sponsorship QR code"></a></td>
    <td align="center"><a href="assets/sponsorship/alipay-2.jpg"><img src="assets/sponsorship/alipay-2.jpg" width="260" alt="Alipay 2 sponsorship QR code"></a></td>
  </tr>
</table>

## License

This project is licensed under the **GNU Affero General Public License v3.0 (AGPL-3.0-only)**. See [LICENSE](LICENSE).

Retained MIT notices and dependency information are provided in [LICENSE-NOTICES.md](LICENSE-NOTICES.md) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) (Simplified Chinese).
