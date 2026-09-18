# KittyAdmin

This is a single DLL combining the three previously working custom plugins.

## Commands

- `/fly` or `/flight`
  - Permission: `kittyfly.fly`
  - WASD horizontal movement
  - Mouse direction
  - Space up
  - Shift down
  - No god mode

- `/buildmode` or `/bm`
  - Admin only
  - Returns placed structures and barricades to the admin inventory

- `/buildbase`
  - Permission: `kittybase.spawn`
  - Spawns the existing 2x2 Maple/wooden KittyBase

- `/removebase`
  - Permission: `kittybase.spawn`
  - Removes only the KittyBase created by `/buildbase`

## Important

This project keeps the actual working source logic from the three supplied plugins and combines it into one Rocket plugin class and one DLL.

Before installing the combined DLL, remove the old:
- KittyFly.dll
- KittyBuildMode.dll
- KittyBase.dll

Do not run the old and combined versions at the same time, or commands/events can be registered twice.
