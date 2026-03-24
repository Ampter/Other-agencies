# Other Agencies Configuration

This page documents everything currently configurable in `agencies.cfg`.

## Config File Location

The mod loads this file at runtime:

1. `GameData/Other-Agencies/agencies.cfg`
2. `GameData/OtherAgencies/agencies.cfg` (fallback)

If loading fails or no valid agencies are found, built-in defaults are used.

## Full File Schema

```cfg
OTHER_AGENCIES
{
    SETTINGS
    {
        checkIntervalSeconds = 5
        nearExpiryThresholdKerbinDays = 0.5
        offerAgeThresholdKerbinDays = 3
        minTakeoverChance = 0.08
        maxTakeoverChance = 0.30
        lateGameStartKerbinYears = 3
        endGameStartKerbinYears = 6
    }

    PREFERENCE
    {
        id = launch_orbit
        keywords = launch, orbit, sub-orbital, satellite, first launch
    }

    AGENCY
    {
        name = OrbitCorp
        preference = satellite_comms
        aggression = 0.48
        completionFlavor = OrbitCorp optimized this network deployment before you.
    }
}
```

`OTHER_AGENCIES` is recommended but optional. If omitted, the root node is used directly.

## SETTINGS Node

`SETTINGS` is optional. If missing or partially invalid, defaults are used per value.

Supported keys:

- `checkIntervalSeconds`
  - Default: `5`
  - Unit: real seconds between evaluation ticks
  - Valid range: `0.2` to `300`

- `nearExpiryThresholdKerbinDays`
  - Default: `0.5`
  - Unit: Kerbin days
  - A contract is eligible when remaining time is `> 0` and `<=` this threshold
  - Valid range: `0` to `1000`

- `offerAgeThresholdKerbinDays`
  - Default: `3`
  - Unit: Kerbin days
  - A contract is eligible when its offer age is `>=` this threshold
  - Valid range: `0` to `1000`

- `minTakeoverChance`
  - Default: `0.08`
  - Unit: probability `0..1`
  - Valid range: `0` to `1`

- `maxTakeoverChance`
  - Default: `0.30`
  - Unit: probability `0..1`
  - Valid range: `0` to `1`
  - If `max < min`, values are swapped automatically

- `lateGameStartKerbinYears`
  - Default: `3`
  - Unit: Kerbin years
  - Enables `duna_late_game` preference after this time
  - Valid range: `0` to `1000`

- `endGameStartKerbinYears`
  - Default: `6`
  - Unit: Kerbin years
  - Enables `outer_planets_end_game` preference after this time
  - Valid range: `0` to `1000`

## PREFERENCE Node (Keyword Overrides)

`PREFERENCE` nodes are optional and can appear multiple times.

Supported keys:

- `id` (required)
  - Preference ID to override
- `keywords` (required)
  - Comma-separated keyword list
  - Matching is case-insensitive against contract title and description

Example:

```cfg
PREFERENCE
{
    id = science
    keywords = science, experiment, lab, analysis, sample return
}
```

If a `PREFERENCE` node is invalid, it is ignored.

## AGENCY Node

Each `AGENCY` node defines one rival agency.

Supported keys:

- `name` (required)
- `preference` (required, must be a valid preference ID)
- `aggression` (optional float; invalid values become `0.35`)
- `completionFlavor` (optional text shown as second on-screen message)

Rules:

- Missing `name` or invalid `preference` skips that agency.
- `aggression` is clamped to `0..1` internally.

## Valid Preference IDs

- `launch_orbit`
- `satellite_comms`
- `mun_minmus`
- `duna_late_game`
- `science`
- `part_test`
- `exploration`
- `outer_planets_end_game`
- `rescue_transport`
- `urgent`

Default keyword sets shipped by the mod:

- `launch_orbit`: `launch, orbit, sub-orbital, satellite, first launch`
- `satellite_comms`: `satellite, relay, antenna, comms, commnet`
- `mun_minmus`: `mun, minmus`
- `duna_late_game`: `duna, interplanetary, transfer window`
- `science`: `science, experiment, temperature, crew report, goo, materials bay`
- `part_test`: `test, engine, part, altitude, activate`
- `exploration`: `explore, flyby, first, discover, reach`
- `outer_planets_end_game`: `jool, eeloo, outer, tylo, vall, bop, pol`
- `rescue_transport`: `rescue, passenger, tourist, crew, transport`
- `urgent`: no keyword list (uses near-expiry timing)

## Runtime Behavior

Evaluation:

1. Only `Offered` contracts are considered.
2. A contract becomes eligible if either:
   - near expiry threshold passes, or
   - offer age threshold passes.
3. Each offer instance (`ContractGuid`) is attempted at most once.

Winner selection:

1. Agencies with matching preference are collected.
2. Their order is shuffled.
3. For each agency:
   - `chance = clamp(aggression * relevance, minTakeoverChance, maxTakeoverChance)`
   - random roll decides success
4. First success wins.

Current relevance value is effectively `1.0` in normal flow.

## Takeover Effects

When a rival takes a contract:

1. The contract is declined via `contract.Decline()`.
2. KSP applies the same reputation/funds/science consequences as manual decline.
3. On-screen message: `<AgencyName> completed: <ContractTitle>`
4. Optional second on-screen message: `completionFlavor`
5. Console log line:
   - `[OtherAgencies] <AgencyName> took '<ContractTitle>'. State is now <State>.`


## Defaults and Fallbacks

Fallback to built-in defaults happens when:

- `agencies.cfg` file is missing
- config cannot be parsed
- no valid `AGENCY` nodes are found

Built-in default agencies are the same entries provided in the shipped `agencies.cfg`.
