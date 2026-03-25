# Other-agencies

`Other-agencies` started as a rival-contract sniping mod and now also supports configurable, contract-driven space races.

## What It Does

- Rival agencies can still steal ignored offered contracts based on preference buckets and aggression.
- Space races are now offered in Mission Control as challenge contracts.
- Accepting a race starts a persistent rival program that gains:
  - passive funds
  - passive science
  - extra funds/science/progress from contracts the rival steals
- The first shipped race is `First Crewed Orbit`:
  - sounding rocket
  - sub-orbital flight
  - contract-funded expansion
  - crewed orbit
- If you decline the race contract, nothing happens.
- If you accept and lose, the contract failure hits both reputation and science.

## Default Race Flow

After your first launch and at least one completed contract, Mission Control can offer:

- `World First Challenge: Beat KerbalX to Orbit`

Accepting it gives support funds up front and starts KerbalX Industries on a probabilistic early-career orbital program. Winning completes the contract. Losing fails it and applies a heavy setback.

## Craft Templates

The default orbital race config references these VAB craft files:

- `OA_KerbalX_Sounding.craft`
- `OA_KerbalX_Suborbital.craft`
- `OA_KerbalX_Orbiter.craft`

Place them under `crafts/VAB/` in the repo before running `create.sh`. The packager copies them into `Ships/VAB/` in the zip.

The current race simulation is still abstract and probabilistic. The craft files are packaged now so the content pipeline is ready for later MechJeb-backed or scripted launch automation.

## Packaging

Run:

```bash
./create.sh
```

The zip now includes:

- `GameData/Other-Agencies/`
- `README.md`
- `CONFIG.md`
- any `.craft` files found under `crafts/VAB/` or `crafts/SPH/`

## Configuration

- Main config file: `GameData/Other-Agencies/agencies.cfg`
- Full config reference: `CONFIG.md`
- Missing or invalid config falls back to built-in defaults

## Rival Agencies

- `KerbalX Industries`: launch/orbit, science, urgent
- `OrbitCorp`: satellite/comms
- `Munar Exploration Group`: Mun/Minmus
- `Duna Initiative`: late-game Duna
- `Kerbin Science Union`: science
- `Industrial Assembly Co.`: part tests
- `Deep Space Surveyors`: exploration
- `Outer Planets Coalition`: outer planets
- `Kerbin Logistics Network`: rescue/transport
- `SpeedRun Aerospace`: urgent deadlines
