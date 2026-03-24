# Other-agencies

A lightweight KSP mod mechanic where ignored offered contracts can expire because a rival agency took them.

## MVP behavior

- Polls `ContractSystem` every few seconds (not every frame).
- Filters only `Offered` contracts.
- Evaluates contracts when near expiry or older than an offered-time threshold.
- Uses lightweight agency preferences + bounded probability rolls.
- Expires the contract with `contract.SetState(Contract.State.Expired)`.
- Displays a screen message like `OrbitCorp completed: Satellite Deployment`.

## Design boundaries

This mod intentionally does **not** simulate real agencies, missions, or progression.
It only applies timed pressure to offered contracts.
