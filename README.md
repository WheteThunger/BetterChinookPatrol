## Features

- Properly randomizes the order in which chinooks visit monuments
- Allows customizing which monuments chinooks will visit
- Allows increasing the number of crates that each chinook will drop
- Support custom monuments (monument markers)

## Configuration

```json
{
  "Min crate drops per chinook": 1,
  "Max crate drops per chinook": 1,
  "Disallow safe zone monuments": true,
  "Disallowed monument types": [
    "Cave",
    "WaterWell"
  ],
  "Disallowed monument tiers": [
    "Tier0"
  ],
  "Disallowed monument prefabs (partial match)": [],
  "Disallowed monument prefabs (exact match)": [],
  "Force allow monument prefabs (partial match)": [],
  "Force allow monument prefabs (exact match)": []
}
```

- `Min crate drops per chinook` -- The minimum number of crates that each chinook can drop.
- `Max crate drops per chinook` -- The maximum number of crates that each chinook can drop.
- `Disallow safe zone monuments` (`true` or `false`) -- While `true`, chinooks will never visit safe zone monuments (e.g., outpost, bandit camp, fishing villages), same as vanilla behavior. While `false`, chinooks may visit safe zone monuments, depending on the other configuration options. Disabling this may make sense for hardcore servers.
- `Disallowed monument types` -- Chinooks will never visit monuments with these types. Allowed values: `Cave`, `Airport`, `Building`, `Town`, `Radtown`, `Lighthouse`, `WaterWell`, `Roadside`, `Mountain`, `Lake`.
- `Disallowed monument tiers` -- Chinooks will never visit monuments with these tiers. Allowed values: `Tier0`, `Tier1`, `Tier2`.
- `Disallowed monument prefabs (partial match)` -- Chinooks will never visit monuments whose prefab name (or monument marker name) contains any of these keywords.
- `Disallowed monument prefabs (exact match)` -- Chinooks will never visit monuments whose prefab name (or monument marker name) is an exact match for any of these values.
- `Force allow monument prefabs (partial match)` -- Chinooks may visit monuments whose prefab name (or monument marker name) contains any of these keywords, even if the monument would be disallowed by other settings.
- `Force allow monument prefabs (exact match)` -- Chinooks may visit monuments whose prefab name (or monument marker name) is an exact match of any of these values.

## FAQ

#### How do extra crate drops work?

If you configure the plugin to allow chinooks to drop multiple crates, the chinook will drop one crate at a time per drop-eligible monument. If the chinook has already visited all visit-eligible monuments and still has crates remaining, it will revisit previous monuments in the same order. When the chinook has dropped all of its crates, the chinook will leave the map.

#### At which monuments will chinooks drop crates?

Chinooks will drop crates at monuments that have a drop zone. The following vanilla monuments have drop zones.

- Power plant
- Train yard
- Water treatment plant
- Airfield
- Dome

Map developers can create additional drop zones using the `assets/bundled/prefabs/modding/dropzone.prefab`. The [Monument Addons](https://umod.org/plugins/monument-addons) plugin also allows creating additional drop zones in monuments via the `maprefab dropzone` command.

## Developer Hooks

```cs
object OnBetterChinookPatrol(CH47HelicopterAIController chinook)
```

- Called when this plugin wants to affect a chinook
- Returning `false` will prevent the plugin from affecting the chinook
- Returning `null` will allow the plugin to affect the chinook, unless another plugin returns `false`
