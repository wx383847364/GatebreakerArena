---
name: gatebreaker-ui-boundary
description: Use for Gatebreaker Arena when implementing or reviewing UI, UGUI, prefab, HUD, View, Presenter, Controller, binding, scene UI, buttons, images, text, layout, animation, safe area, or UI smoke tests. Enforces explicit UI bindings, thin presentation logic, prefab visual preservation, and no gameplay rules in UI.
---

# Gatebreaker UI Boundary

Use this skill for any task that touches:
- UGUI, prefab, HUD, View, Presenter, Controller, or UI binding
- buttons, images, text, layout, animation, safe area, or scene UI
- `Assets/HotUpdateContent/Script/App.HotUpdate/GatebreakerArena/UI`
- `Assets/Res` or `Assets/Scenes` when the change affects UI objects or bindings

## Core Rules

- UI is presentation glue.
- UI may bind state, forward input, play approved feedback, and refresh visuals.
- UI must not own match rules, scoring, serve ammo, paddle physics formulas, AI decisions, persistence, or settlement.

- Runtime UI node access must use explicit bindings.
- Prefer serialized fields, generated bindings, binding manifests, or project binding components when available.
- Do not implement runtime UI features by falling back to `Transform.Find`, `GameObject.Find`, or recursive `GetComponentsInChildren`.
- One-time Editor migration or authoring scripts may use explicit path lookup only if the result is saved back into prefab or binding data before delivery.

- Preserve original prefab visuals by default.
- Do not change original prefab colors, alpha, tint defaults, material colors, `Graphic.color`, or `CanvasGroup.alpha` during UI logic work unless the user explicitly requests that visual change.
- Do not treat disabled, selected, locked, hover, mask, or cleanup work as permission to alter existing visual parameters.

## Required Workflow

1. Classify whether the task is UI-related before editing.
2. Read the relevant prefab/binding/view code before changing behavior.
3. Add or fix explicit bindings first when runtime code needs a UI node.
4. Keep Presenter/Controller logic focused on state binding and input forwarding.
5. Preserve prefab visual parameters unless the task explicitly asks for style changes.
6. Add focused smoke or regression checks when UI behavior changes.

## Do Not

- Do not put gameplay formulas or settlement logic in UI scripts.
- Do not use runtime hierarchy lookup as a convenience fallback.
- Do not change prefab color or transparency while implementing UI business logic unless explicitly requested.
- Do not let multiple agents edit the same prefab or scene binding area casually.

## Validation

Before finishing:
- Check that UI runtime code uses explicit bindings for touched nodes.
- Check that no gameplay rules moved into UI.
- Check that prefab colors, transparency, tint defaults, material colors, and `CanvasGroup.alpha` were preserved unless explicitly requested.
- Check that UI-facing behavior has an appropriate smoke or regression check.
