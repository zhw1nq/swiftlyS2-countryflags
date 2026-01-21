<div align="center">
  <img src="https://pan.samyyc.dev/s/VYmMXE"/>
  <h1>SwiftlyS2 Country Flags</h1>
  <p>Display country flags on the CS2 scoreboard based on player IP location</p>
</div>

<p align="center">
  <a href="https://developer.valvesoftware.com/wiki/Source_2"><img src="https://img.shields.io/badge/Source%202-orange?style=for-the-badge&logo=valve&logoColor=white" alt="Source 2"></a>
  <a href="https://github.com/zhw1nq/swiftlyS2-countryflags/releases"><img src="https://img.shields.io/badge/Version-1.0.0-blue?style=for-the-badge" alt="Version"></a>
  <a href="https://dotnet.microsoft.com/"><img src="https://img.shields.io/badge/.NET-10.0-purple?style=for-the-badge&logo=dotnet&logoColor=white" alt=".NET"></a>
</p>

---

## Platform Support

| Platform | Status      |
| -------- | ----------- |
| Windows  | Maintenance |
| Linux    | Maintenance |

## Requirements

- [SwiftlyS2](https://github.com/swiftly-solution/swiftlys2) v1.0.0+

## Installation

1. Download the [latest release](https://github.com/zhw1nq/swiftlyS2-countryflags/releases)
2. Extract plugin to `addons/swiftlys2/plugins/swiftlyS2-countryflags/`
3. Restart the server
4. Configure `config.jsonc` (optional)

> **Note:** GeoLite2-Country.mmdb is already included in the `data/` folder.

## Configuration

| Option                    | Type   | Default | Description                              |
| ------------------------- | ------ | ------- | ---------------------------------------- |
| `Enabled`                 | bool   | `true`  | Enable/disable the plugin                |
| `Debug`                   | bool   | `false` | Enable debug logging                     |
| `DefaultStatus`           | bool   | `true`  | Show flag by default for new players     |
| `EnableToggleCommand`     | bool   | `true`  | Allow players to toggle flag display     |
| `CacheExpiryHours`        | int    | `168`   | Hours before refreshing country (7 days) |
| `AutoSaveIntervalSeconds` | int    | `120`   | Auto-save cache interval (0 = disable)   |
| `DefaultBadgeId`          | int    | `1079`  | Badge ID for unknown countries           |
| `CountryBadges`           | object | `{...}` | Country code to badge ID mapping         |

## Commands

| Command | Description                 |
| ------- | --------------------------- |
| `!flag` | Toggle country flag display |
| `!cf`   | Alias for `!flag`           |

## Supported Languages

<p align="center">
  <img src="https://flagcdn.com/24x18/gb.png" title="English">
  <img src="https://flagcdn.com/24x18/vn.png" title="Vietnamese">
</p>

## Country Badge Mappings

The plugin includes 70+ pre-configured country-to-badge mappings including:

- 🇪🇺 **Europe:** NL, FR, GB, RU, DE, PL, SE, NO, FI, ES, IT, GR, TR, UA, CZ, AT, CH, BE, etc.
- 🇺🇸 **North America:** US, CA
- 🇧🇷 **South America:** BR, AR, CL, CO, PE, MX
- 🇯🇵 **Asia:** CN, JP, KR, TH, ID, MY, VN, IN, KZ
- 🇸🇦 **Middle East:** SA, AE, IL, LB
- 🇦🇺 **Oceania:** AU
- 🇪🇬 **Africa:** EG, TN, DZ, MA

You can customize these mappings in `config.jsonc`.

## Building from Source

### Requirements

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [SwiftlyS2.CS2 NuGet Package](https://www.nuget.org/packages/SwiftlyS2.CS2)

### Build

```bash
git clone https://github.com/zhw1nq/swiftlyS2-countryflags.git
cd swiftlyS2-countryflags
dotnet restore
dotnet build
```

### Publish

```bash
dotnet publish -c Release
```

Output directory: `build/publish/swiftlyS2-countryflags/`

## Credits

- [zhw1nq](https://github.com/zhw1nq) - Author
- [SwiftlyS2](https://github.com/swiftly-solution/swiftlys2) - Framework
- [MaxMind GeoLite2](https://dev.maxmind.com/geoip/geolite2-free-geolocation-data) - GeoIP Database
- [swiftly-solutions/countryflags](https://github.com/swiftly-solutions/countryflags) - Original SwiftlyS1 plugin

## License

This project is licensed under the MIT License.
