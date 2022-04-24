## Features

- Properly randomizes the order in which chinooks visit monuments
- Allows customizing which monuments chinooks will visit
- Allows increasing the number of crates that each chinook will drop

## Configuration

```json
{
  "Min crate drops per chinook": 1,
  "Max crate drops per chinook": 1,
  "Disallowed monument types": [
    "Cave",
    "WaterWell"
  ],
  "Disallowed monument tiers": [
    "Tier0"
  ],
  "Disallowed monument prefabs (partial match)": []
}
```

- `Min crate drops per chinook` -- The minimum number of crates that each chinook can drop.
- `Max crate drops per chinook` -- The maximum number of crates that each chinook can drop. After dropping the max number of crates, the chinook will leave the map.
- `Disallowed monument types` -- Chinooks will never visit monuments with these types. Allowed values: `Cave`, `Airport`, `Building`, `Town`, `Radtown`, `Lighthouse`, `WaterWell`, `Roadside`, `Mountain`, `Lake`.
- `Disallowed monument tiers` -- Chinooks will never visit monuments with these tiers. Allowed values: `Tier0`, `Tier1`, `Tier2`.
- `Disallowed monument prefabs (partial match)` -- Chinooks will never visit monuments whose prefab name contains any of these keywords.

## Developer Hooks

```cs
object OnBetterChinookPath(CH47HelicopterAIController chinook)
```

- Called when this plugin wants to affect a chinook
- Returning `false` will prevent the plugin from affecting the chinook
- Returning `null` will allow the plugin to affect the chinook, unless another plugin returns `false`
