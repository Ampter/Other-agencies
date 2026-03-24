## OVERVIEW

This mod introduces rival agencies that compete for contracts by removing them from the player if ignored.

This is a **simulation illusion**, not a full system.

The mod must remain:

* Lightweight
* Stable
* Compatible with KSP’s contract system

---

## HISTORICAL CONTEXT (IMPORTANT)

Previous attempt: “Space Race” mod

Planned features included:

* Rival agency taking contracts
* Stealing science and staff
* Simulating progression and missions ([Kerbal Space Program Forums][1])

Result:

* Never completed
* Scope became too large
* System complexity caused failure

Conclusion:

> The failure was caused by attempting to simulate a full rival space program instead of a focused mechanic.

---

## CORE DESIGN PRINCIPLE

Rival agencies do not exist as real entities.

They are:

* Filters
* Probabilities
* Event triggers

They do not:

* Launch rockets
* Progress independently
* Persist in the game world

---

## NON-GOALS (STRICT)

The mod must NOT implement:

* Vessel spawning
* Real AI behavior
* Science stealing
* Kerbal hiring/firing
* Tech tree changes
* Research systems
* Background simulation loops

These systems were attempted previously and caused failure.

---

## CORE SYSTEMS

### 1. Contract Watcher

Responsibilities:

* Poll contracts from ContractSystem
* Filter only OFFERED contracts
* Ignore all other states

Rules:

* Never modify active contracts
* Never modify completed contracts

---

### 2. Agency System

Agencies are simple data containers:

```id="a1"
class Agency
{
    string Name;
    Func<Contract, bool> Preference;
    float Aggression;
}
```

No persistence required beyond runtime.

---

### 3. Contract Evaluation

Trigger conditions:

* Contract is near expiry
  OR
* Contract has passed a defined time threshold

Example:

```id="a2"
bool IsNearExpiry(Contract c)
{
    return (c.DateExpire - CurrentTime()) < Threshold;
}
```

---

### 4. Selection Logic

Steps:

1. Find agencies matching contract
2. Roll probability per agency
3. First successful roll wins

Constraints:

* Max one agency per contract
* No chaining or retries

---

### 5. Execution System

Only allowed action:

```id="a3"
contract.SetState(Contract.State.Expired);
```

Do not:

* Delete contracts directly
* Modify rewards
* Alter parameters

---

### 6. Messaging System

Must display:

* Agency name
* Contract title

Example:

* “OrbitCorp completed: Satellite Deployment”

Use:

```id="a4"
ScreenMessages.PostScreenMessage(...)
```

---

## CONTRACT CLASSIFICATION

Phase 1 (safe):

* String matching (title/description)

Phase 2 (optional):

* Parameter inspection
* Celestial body detection

Never assume contract type is stable.

---

## TIME MODEL

Use:

```id="a5"
Planetarium.GetUniversalTime()
```

Rules:

* No background simulation
* No time accumulation systems
* Evaluate only during checks

---

## RANDOMNESS RULES

* Use bounded probability (0.2–0.5 typical)
* Scale only with simple factors:

  * Agency aggression
  * Contract relevance

No hidden or complex formulas.

---

## PERFORMANCE RULES

* Do not run every frame
* Check every few seconds
* Avoid heavy loops

---

## STABILITY RULES

* Null-check all contracts
* Guard against empty lists
* Do not cache contract references long-term

---

## MVP REQUIREMENTS

Minimum working system:

* Detect offered contracts
* Detect near-expiry
* Select agency
* Expire contract
* Display message

Nothing else is required.

---

## EXPANSION RULE (CRITICAL)

Before adding any feature:

Ask:
“Does this require simulating the rival as a real entity?”

If yes:

* Do not implement

---

## DESIGN PHILOSOPHY

This mod is:

* A pressure system
* A timing mechanic
* A gameplay modifier

It is NOT:

* A strategy simulation
* A faction system
* A world simulation

---

## FINAL CONSTRAINT

If the system cannot be explained in one sentence:

“Contracts expire faster because rivals take them”

Then it is too complex.

---

END OF FILE

[1]: https://forum.kerbalspaceprogram.com/topic/101838-alphaspace-race/?utm_source=chatgpt.com "[ALPHA]Space Race - KSP1 Mod Development - Kerbal Space Program Forums"
