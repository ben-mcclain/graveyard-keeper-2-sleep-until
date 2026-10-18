# Publish Sleep Until to Steam Workshop

**Status: upload kit prepared; no Steam Workshop item has been published yet.**

This guide targets the built-in creator in the inspected Windows GK2 build. Menu names and paths can change with game updates. The DLL is the same v0.1.0 binary reported working in-game; Workshop Loader integration has not been tested in-game.

## Upload from a PC running GK2

1. Download `SleepUntil-0.1.0-workshop.zip` from the [v0.1.0 release](https://github.com/ben-mcclain/graveyard-keeper-2-sleep-until/releases/tag/v0.1.0) and extract it somewhere permanent, for example `Documents\SleepUntilWorkshop`. Keep the extracted folder for future updates.
2. Close the game. Press **Win + R** and open:

   ```text
   %USERPROFILE%\AppData\LocalLow\Lazy Bear Games\Graveyard Keeper 2\Mods
   ```

3. Back up `workshop.json`, then edit its existing `workshopCreatorMode` value to `true`. Preserve other settings. If the file does not exist, launch and close GK2 once; the inspected build creates it automatically. Its setting looks like:

   ```json
   { "workshopCreatorMode": true }
   ```

4. Start GK2 through your signed-in Steam account. Press **Shift + F11** (some keyboards also need Fn).
5. Select **Create new workshop item**. Enter **Sleep Until** as the name. The inspected uploader only offers **Translation** as its type, even though this is a code mod. Do not add fake translation files. State clearly in the description that it is a BepInEx code mod.
6. Use the folder picker to select **the extracted `SleepUntil` folder containing `BepInEx` and `INSTALL.txt`**, not the ZIP or the entire game folder. Click **Create**. It creates and uploads in one operation. Wait for `Upload complete` and the item page.
7. On the item page, edit the description using the text in `_DESCRIPTION.bbcode.txt`. If Steam requests a Workshop agreement, review and accept it yourself before publishing. Set visibility to **Public** when ready. The inspected uploader initially uses **Unlisted**, despite a diagnostic string saying Private.
8. Open the item URL in a signed-out/private browser and confirm that it is visible. Subscribe on a test setup and verify download and installation before calling Workshop installation tested.

### Thumbnail

You may place an original `Thumbnail.png`, `.jpg`, or `.jpeg` in the content folder before upload. The game generates a plain black placeholder if none exists. Thumbnail files are used as the preview, not uploaded as mod content.

### Updating an existing item

Use the existing item's **Upload** button in Shift+F11, rather than creating a duplicate. The inspected uploader resets the description to the title and visibility to Unlisted on every upload. Restore the full description and Public visibility afterward. Keep the creator's local record and content folder; don't delete/recreate the item just to update a DLL.

## Player installation is not subscription-only

Steam delivers the files; the base game does not load this BepInEx DLL itself. Players need BepInEx 5 and either:

- manual copying of `BepInEx\plugins\SleepUntil` into the game's plugins folder, or
- the independently maintained [GK2 Workshop Loader](https://steamcommunity.com/sharedfiles/filedetails/?id=3807346541), installed according to its own instructions, followed by subscribing and approving the mod.

Do not install both manual and Workshop-managed copies. We do not bundle BepInEx, the Workshop Loader, or game files. The optional loader is not required for manual installation.

## Packaging references

- [Workshop Loader author guide](https://github.com/Zoriten/-GK2-WorkshopLoader/blob/main/docs/FOR_MODDERS.txt)
- [Example community code-mod listing](https://steamcommunity.com/sharedfiles/filedetails/?id=3809174815)

The upload kit includes only our plugin, MIT license, installation text, and publishing text. Files beginning with `_` are excluded by GK2's uploader. No private paths, saves, game assemblies, or decompiled source are included.
