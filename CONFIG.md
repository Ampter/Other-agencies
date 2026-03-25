# Other Agencies Configuration

This file documents the current `agencies.cfg` schema for both rival contract stealing and the new contract-driven space races.

## Config Location

The mod loads:

1. `GameData/Other-Agencies/agencies.cfg`
2. `GameData/OtherAgencies/agencies.cfg`

If loading fails, built-in defaults are used.

## Top-Level Schema

```cfg
OTHER_AGENCIES
{
    SETTINGS { ... }
    SPACE_RACE_SETTINGS { ... }
    PREFERENCE { ... }
    AGENCY { ... }
    SPACE_RACE { ... }
}
```

`OTHER_AGENCIES` is recommended but optional. If omitted, the root node is used directly.

## SETTINGS

Controls rival takeover timing.

- `checkIntervalSeconds`
  - Default: `5`
  - Valid: `0.2..300`

- `nearExpiryThresholdKerbinDays`
  - Default: `0.5`

- `offerAgeThresholdKerbinDays`
  - Default: `3`

- `minTakeoverChance`
  - Default: `0.08`

- `maxTakeoverChance`
  - Default: `0.30`
  - If lower than `minTakeoverChance`, the values are swapped

- `lateGameStartKerbinYears`
  - Default: `3`

- `endGameStartKerbinYears`
  - Default: `6`

## SPACE_RACE_SETTINGS

Global runtime settings for accepted race contracts.

- `simulationCheckIntervalSeconds`
  - Default: `45`
  - How often the rival simulates funds/science/stage progress

- `playerProgressCheckIntervalSeconds`
  - Default: `2`
  - How often player victory conditions are checked

- `maxCatchUpTicksPerUpdate`
  - Default: `10`
  - Limits catch-up work after long timewarp/load gaps

- `announceStageChanges`
  - Default: `true`

- `announceResearchUnlocks`
  - Default: `true`

- `announceContractRewards`
  - Default: `false`

## PREFERENCE

Overrides keywords for a built-in preference bucket.

Keys:

- `id`
- `keywords`

Example:

```cfg
PREFERENCE
{
    id = science
    keywords = science, experiment, lab, analysis, sample return
}
```

Built-in preference IDs:

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

## AGENCY

Defines a rival agency for contract stealing.

Keys:

- `name` required
- `preference` optional single preference ID
- `preferences` optional comma-separated preference ID list
- `aggression` optional float `0..1`
- `completionFlavor` optional screen message

Notes:

- `preferences` and `preference` are merged
- an agency is skipped if it resolves to zero valid preference IDs
- multi-preference agencies let one rival steal both launch and science work, which is useful for space races

Example:

```cfg
AGENCY
{
    name = KerbalX Industries
    preferences = launch_orbit, science, urgent
    aggression = 0.42
    completionFlavor = KerbalX Industries leveraged fresh R&D data before your team could react.
}
```

## SPACE_RACE

Defines one Mission Control challenge contract plus the rival simulation that starts after the player accepts it.

Core keys:

- `id` required
- `name`
- `enabled`
- `rivalAgency` required
- `playerGoal`
  - Current built-in goal: `crewed_orbit`
- `targetBody`
- `offerAfterUniversalTime`
- `offerAfterCompletedContracts`
- `requireFirstLaunchReached`

Contract presentation/rewards:

- `contractTitle`
- `contractSynopsis`
- `contractDescription`
- `contractNotes`
- `offeredMessage`
- `acceptedMessage`
- `completedMessage`
- `failedMessage`
- `supportFunds`
- `completionFunds`
- `completionScience`
- `completionReputation`
- `failureReputation`
- `failureSciencePenalty`

Stolen-contract reward model:

- `contractScienceBaseMin`
- `contractScienceBaseMax`
- `contractFundsBaseMin`
- `contractFundsBaseMax`
- `contractProgressBase`
- `contractCompletionScienceMultiplier`
- `contractCompletionFundsMultiplier`

Each race needs at least one `RESEARCH_STEP` and one `STAGE`.

## RESEARCH_STEP

Defines a linear rival tech unlock.

Keys:

- `id` required
- `displayName`
- `scienceCost`
- `unlockMessage`

Example:

```cfg
RESEARCH_STEP
{
    id = crewed_orbit_systems
    displayName = Crewed Orbit Systems
    scienceCost = 28
    unlockMessage = KerbalX Industries is now capable of crewed orbital hardware.
}
```

## STAGE

Defines one linear stage in the rival race program.

Keys:

- `id` required
- `title`
- `description`
- `requiredResearch`
  - comma-separated research IDs
- `fundsCost`
- `requiredStolenContracts`
- `requiredContractProgress`
- `completionChance`
- `passiveScienceChance`
- `passiveScienceMin`
- `passiveScienceMax`
- `passiveFundsChance`
- `passiveFundsMin`
- `passiveFundsMax`
- `contractScienceMultiplier`
- `contractFundsMultiplier`
- `contractProgressMultiplier`
- `completionMessage`
- `markerBody`
- `markerAltitude`
- `markerLabel`
- `craftFileName`

Notes:

- `marker*` values are stored as stage metadata for race UI/map follow-up work
- `craftFileName` is documentation/packaging metadata right now; the shipped packager expects the default craft files under `crafts/VAB/`

Example:

```cfg
STAGE
{
    id = orbit
    title = Crewed Orbit
    requiredResearch = crewed_orbit_systems
    fundsCost = 18000
    requiredStolenContracts = 4
    requiredContractProgress = 6
    completionChance = 0.38
    craftFileName = OA_KerbalX_Orbiter.craft
}
```

## Default Craft Names

The shipped default race references:

- `OA_KerbalX_Sounding.craft`
- `OA_KerbalX_Suborbital.craft`
- `OA_KerbalX_Orbiter.craft`

Place them in `crafts/VAB/` before running `create.sh`.

## Runtime Behavior

Contract stealing:

1. Offered contracts are checked on an interval.
2. A contract becomes eligible near expiry or after sitting offered long enough.
3. Matching agencies roll based on aggression and relevance.
4. The winner declines the contract through the stock path.

Space races:

1. The next eligible `SPACE_RACE` appears as a Mission Control contract.
2. Declining or letting it expire marks that race as declined and does nothing else.
3. Accepting starts the rival simulation.
4. The rival gains passive science/funds and extra rewards from contracts it steals.
5. Research unlocks are bought in listed order as science accumulates.
6. Stages advance in listed order once requirements are met and the stage roll succeeds.
7. The first shipped player goal checks for a crewed orbit around the configured target body.
8. If the rival reaches its final stage first, the contract fails and the science penalty is applied manually in addition to stock reputation failure.

## Fallback Rules

Built-in defaults are used when:

- `agencies.cfg` is missing
- the file cannot be parsed
- no valid agencies load
- no valid space races load
