# Other-agencies

A lightweight KSP mod mechanic where ignored offered contracts can expire because a rival agency took them.


## Configuration

- Main config file: `GameData/Other-Agencies/agencies.cfg`
- Full config reference: `CONFIG.md`
- If config is missing/invalid, built-in defaults are used.

## Rival agencies (default)

1. **KerbalX Industries**
   - Focus: launch + orbit contracts
   - Behavior: prefers low-tech/simple missions first
   - Funds/tech flavor: medium funds, early-game

2. **OrbitCorp**
   - Focus: satellite + relay networks
   - Behavior: prefers antennas/comms/CommNet style contracts
   - Funds/tech flavor: high funds, mid-game

3. **Munar Exploration Group**
   - Focus: Mun + Minmus contracts only
   - Behavior: ignores everything else
   - Funds/tech flavor: medium funds, early-mid

4. **Duna Initiative**
   - Focus: Duna + interplanetary contracts
   - Behavior: late-game activation only
   - Funds/tech flavor: very high funds, late-game

5. **Kerbin Science Union**
   - Focus: science experiments
   - Behavior: prefers temperature scans, crew reports, and similar science work
   - Funds/tech flavor: low funds, early-game

6. **Industrial Assembly Co.**
   - Focus: part testing contracts
   - Behavior: favors “test this part/engine at altitude” style tasks
   - Funds/tech flavor: medium funds, early-mid

7. **Deep Space Surveyors**
   - Focus: exploration + flyby contracts
   - Behavior: prefers first-visit/first-time milestones
   - Funds/tech flavor: medium-high funds, mid-game 

8. **Outer Planets Coalition**
   - Focus: Jool/Eeloo/outer system contracts
   - Behavior: rare and only active late-game
   - Funds/tech flavor: extremely high funds, endgame

9. **Kerbin Logistics Network**
   - Focus: rescue 
   - Behavior: prefers stranded Kerbals/passenger logistics
   - Funds/tech flavor: medium funds, early-mid

10. **SpeedRun Aerospace**
    - Focus: urgent contracts with short deadlines
    - Behavior: aggressively targets contracts closest to expiry
    - Funds/tech flavor: medium funds, variable tech, highly competitive


## Design boundaries

This mod intentionally does **not** simulate real agencies, missions, or progression.
It only applies timed pressure to offered contracts. For now.
