# Other-agencies

A lightweight KSP mod mechanic where ignored offered contracts can expire because a rival agency took them.

## MVP behavior

- Polls `ContractSystem` every few seconds (not every frame).
- Filters only `Offered` contracts.
- Evaluates contracts when near expiry or older than an offered-time threshold.
- Uses lightweight agency preferences + bounded probability rolls.
- Expires the contract with `contract.SetState(Contract.State.Expired)`.
- Displays a screen message with agency + contract title and a short agency flavor line.

## Rival agencies

1. **KerbalX Industries**
   - Focus: launch + orbit contracts
   - Behavior: prefers low-tech/simple missions first
   - Funds/tech flavor: medium funds, early-game, “fast and reliable”
   - Flavor line: `KerbalX has successfully placed a satellite before you.`
2. **OrbitCorp**
   - Focus: satellite + relay networks
   - Behavior: prefers antennas/comms/CommNet style contracts
   - Funds/tech flavor: high funds, mid-game, efficiency-driven
3. **Munar Exploration Group**
   - Focus: Mun + Minmus contracts only
   - Behavior: ignores everything else
   - Funds/tech flavor: medium funds, early-mid, Mun-obsessed
   - Flavor line: `Munar Exploration Group planted their flag first.`
4. **Duna Initiative**
   - Focus: Duna + interplanetary contracts
   - Behavior: late-game activation only
   - Funds/tech flavor: very high funds, late-game, ambitious/elite
5. **Kerbin Science Union**
   - Focus: science experiments
   - Behavior: prefers temperature scans, crew reports, and similar science work
   - Funds/tech flavor: low funds, early-game, academic
6. **Industrial Assembly Co.**
   - Focus: part testing contracts
   - Behavior: favors “test this part/engine at altitude” style tasks
   - Funds/tech flavor: medium funds, early-mid, engineering-driven
7. **Deep Space Surveyors**
   - Focus: exploration + flyby contracts
   - Behavior: prefers first-visit/first-time milestones
   - Funds/tech flavor: medium-high funds, mid-game explorers
8. **Outer Planets Coalition**
   - Focus: Jool/Eeloo/outer system contracts
   - Behavior: rare and only active late-game
   - Funds/tech flavor: extremely high funds, endgame, mysterious/powerful
9. **Kerbin Logistics Network**
   - Focus: rescue + crew transport contracts
   - Behavior: prefers stranded Kerbals/passenger logistics
   - Funds/tech flavor: medium funds, early-mid, practical service agency
10. **SpeedRun Aerospace**
    - Focus: urgent contracts with short deadlines
    - Behavior: aggressively targets contracts closest to expiry
    - Funds/tech flavor: medium funds, variable tech, highly competitive

## Design boundaries

This mod intentionally does **not** simulate real agencies, missions, or progression.
It only applies timed pressure to offered contracts.
