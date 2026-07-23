# Handoff — Emergency Repair Team

## Project Objective

Multiplayer co-op game built on Unity Netcode for GameObjects. Players join a lobby, pick roles, gear up with role-specific items, and launch into missions. The full flow: MainMenu → PreStartLobby → StartLobby → Mission (with reconnect, role persistence, inventory sync, and localization).

## Current State

### Completed

- **Currency System**: `CurrencyManager` on player prefab with `NetworkVariable<int>`. Syncs currency across network, server-authoritative. Starts at 1000 per mission. `CurrencyUI` displays current amount in HUD with +/- 100 test buttons.

- **Lobby Flow**: PreStartLobbyController → StartLobbyController → Mission launch. Conditions: all players in zone, at least one PrimaryRole, all players have a role.
- **Role System**: `NetworkPlayerRole` with bitmask (`networkRoleMask`), add/remove roles via RPC, role-based item slots.
- **Inventory**: Server-tracked role items (`allTrackedRoleItems`). Role items are NOT dropped on disconnect — they're restored on reconnect via `RestoreRoleItemsClientRpc`.
- **Reconnect**: Persistent `localSessionId` via `PlayerPrefs`. `SavePlayerRoleItems()` / `RestorePlayerRoleItems()`.
- **Icons**: `PickableItem.RegisteredIcons` (static dict, populated in `Awake`).
- **Localization**: `com.unity.localization` with tables `UI_Table`, `PlayerUI_Table`, `Items_Table`. Static text via `LocalizeStringEvent`, dynamic text via `LocalizedString` in code.
- **MainMenuUI**: Language dropdown, locale save/restore, `LocalizedString` for dynamic text (status, color label, microphone, code, etc.).
- **SettingsPanelUI**: Volume, mouse sensitivity, microphone selection, language dropdown, graphics button.
- **GraphicsSettingsUI**: Quality presets (Low/Medium/High/Ultra), individual overrides for texture quality, shadows (quality/distance/resolution), draw distance, VSync, FPS limit. Settings saved to `PlayerPrefs`, restored on startup via `GameSettingsApplier` and on panel open. URP shadow resolution via `UniversalRenderPipelineAsset.mainLightShadowmapResolution`.
- **InventoryUI**: Slot display with localized item names from `Items_Table`.
- **PauseMenu**: Pause/resume flow, settings panel, exit to main menu.

### Known Issues

- Shadow quality and resolution changes may not be visually noticeable depending on scene lighting setup.
- Localization flicker (English → selected language on panel open) fixed by setting `canvasGroup.alpha = 0` in `Awake` and fading in.
- Texture quality (mipmap limit) confirmed working: 0 = full resolution (Ultra), 3 = max compression (Low).

## File Structure

```
Assets/Project/
  Scripts/
    Interaction/
      PreStartLobbyController.cs    — Lobby setup, reconnect, player count
      StartLobbyController.cs       — Mission start, zone check, countdown, role blocking
      MissionLaunchZone.cs          — Zone trigger, role mask events
    Items/
      Inventory.cs                  — Slot management, role item logic, lock/unlock
      PickableItem.cs               — Interactable pickup, localization key, icon registry
      NetworkInventorySync.cs       — Server sync, role item tracking, restore on reconnect
      RoleItem.cs                   — Role item data (Role, Category)
    Network/
      NetworkConnectionManager.cs   — Persistent session ID, save/restore role items
      GameSessionData.cs            — Static session data (colors, version)
      NetworkPlayerSpawnTeleporter.cs — Spawn point, StartLobbyController priority
    Roles/
      NetworkPlayerRole.cs          — Role bitmask, add/remove, icon rebuild
    Player/
      PlayerController.cs           — Player movement, interaction, pause
      CurrencyManager.cs            — Networked currency per player (start 1000, add/spend)
    UI/
      MainMenuUI.cs                 — Main menu, language dropdown, localized strings
      SettingsPanelUI.cs            — Settings (volume, sensitivity, mic, language, graphics)
      GraphicsSettingsUI.cs         — Graphics presets & individual overrides
      PauseMenu.cs                  — Pause/Resume/Settings/Exit flow
      PausePanelUI.cs               — Pause panel buttons, player volume entries
      InventoryUI.cs                — Inventory slot display, localized item names
      GameSettingsApplier.cs        — Startup settings restore (FPS, shadows, etc.)
      CurrencyUI.cs                 — Currency HUD display, +100/-100 test buttons
  Prefabs/
    UI/
      Windows/
        SettingsPanel.prefab
        GraphicsSettingsPanel.prefab
        PausePanel.prefab
        MainMenuCanvas.prefab
        ...
```

## Next Steps

1. Verify `LocalizeStringEvent` on remaining static UI elements (assign `Table Collection` + `Table Entry`).
2. Add missing localization keys for shadow dropdown (`Disable`, `Hard Only`, `All`).
3. Test full loop: MainMenu → PreStartLobby → StartLobby → Mission (with reconnects, disconnects, language switching).
4. Tune graphics preset values based on player feedback.
5. Investigate shadow visual feedback — may need scene-specific lighting adjustments.
6. Add `LocalizeStringEvent` components to PausePanel prefab buttons (Resume, Settings, Exit).

## Currency System

### Architecture (Shared — one pool for all players)

- **`CurrencyManager`** (`NetworkBehaviour`, singleton, `DontDestroyOnLoad`):
  - Place on a `NetworkObject` in the Mission scene (e.g. on the same GameObject as `MissionManager`).
  - `NetworkVariable<int>` — one shared value for the whole team, server-authoritative.
  - `RequestAddCurrency(int)` / `RequestSpendCurrency(int)` — any client → `ServerRpc`.
  - `OnCurrencyChanged` event for UI updates.
  - Late-joining / reconnecting players auto-sync via `NetworkVariable`.
- **`CurrencyUI`** (`MonoBehaviour`, on HUD canvas):
  - Displays current shared currency as `$ {amount}`.
  - Two test buttons: **+100** and **-100**.
  - Finds `CurrencyManager` via singleton `CurrencyManager.Instance`.

### Setup Required in Editor

1. Add `CurrencyManager` to a `NetworkObject` in the Mission scene (e.g. same GameObject as `MissionManager`).
2. Create a `CurrencyUI` canvas panel (e.g., anchored top-right) with:
   - `TMP_Text` for currency display.
   - Two `Button`s wired to `addButton` / `subtractButton` references.
   - Set `testAmount` to 100.
3. Place the `CurrencyUI` panel in the Mission scene HUD (or instantiate via code).

### Next Steps for Currency

- Implement mission quota system (target earn amount per mission).
- Add currency rewards for mission objectives (loot, enemy kills, etc.).
- Add currency purchase UI for shop items during missions.
- Persist currency across missions (save to `PlayerPrefs` or server-side).
