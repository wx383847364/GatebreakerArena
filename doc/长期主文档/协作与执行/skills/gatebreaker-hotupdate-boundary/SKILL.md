---
name: gatebreaker-hotupdate-boundary
description: Use for the Gatebreaker Arena Unity + HybridCLR + YooAssets project when implementing or reviewing gameplay code, architecture boundaries, cross-layer DTOs, runtime asset loading, tests, validation, or subagent task splits. Enforces App.AOT/App.Shared/App.HotUpdate.GatebreakerArena ownership and keeps gameplay rules out of UI and AOT.
---

# Gatebreaker HotUpdate Boundary

Use this skill for any Gatebreaker Arena task that touches:
- `App.AOT`
- `App.Shared`
- `App.HotUpdate.GatebreakerArena`
- HybridCLR loading
- YooAssets loading
- gameplay architecture
- tests, validation, or subagent task assignment

## Core Rules

- `App.AOT` is host infrastructure only.
- `App.AOT` may initialize services, persistence, platform bridges, YooAssets, HybridCLR, and shared infrastructure.
- `App.AOT` must not contain match rules, scoring, serve ammo, paddle bounce formulas, AI decisions, settlement, or UI business flow.

- `App.Shared` is for stable cross-layer contracts only.
- Put only DTOs, interfaces, and cross-layer events in `App.Shared`.
- Do not put gameplay service implementations, Unity scene logic, or `ScriptableObject` logic in `App.Shared`.

- `App.HotUpdate.GatebreakerArena` is the gameplay layer.
- Put match state, Ball, Paddle, Zone, Serve, Mode, AI, configuration catalogs, orchestration, and presenters here.
- Prefer pure C# domain logic that can be tested without Unity scene objects.

- `MonoBehaviour` is presentation glue, not gameplay authority.
- UI classes may forward input, bind state, and refresh visuals.
- UI classes must not own scoring, serve ammo rules, paddle physics formulas, AI decisions, persistence, or settlement.

- Runtime assets must load through YooAssets or the project asset runtime abstraction.
- Do not introduce new `Resources.Load` calls for formal runtime features.
- Do not use editor-only APIs in runtime gameplay code.

## Required Workflow

1. Identify which layer owns the change.
2. Keep shared DTO/interface changes minimal and stable.
3. Keep domain logic in pure C# services and models.
4. Keep Unity object code thin.
5. Verify resource access path and persistence ownership.
6. Review for boundary violations before finishing.

## Subagent Rules

- Every implementation or review subagent should inherit this skill.
- Only one agent may lead changes in `App.Shared`.
- Only one agent may lead changes in HotUpdate entry or composition root.
- Only one UI-focused agent should change prefabs or scene bindings.

## Validation

Before finishing:
- Check layer ownership.
- Check runtime/editor API separation.
- Check that Unity views stay thin.
- Check that gameplay formulas are not in UI scripts.
- Check that runtime asset loading does not bypass YooAssets.
