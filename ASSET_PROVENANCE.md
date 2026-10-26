# Asset and dependency provenance

## Original replacement artwork

`tools/generate_placeholder_art.py` creates the floor, brick wall and diamond player marker from geometric rules, without reading source artwork. Its generated PNGs, Tile assets and all 16 maze-cell layouts replace the imported dungeon atlas. These are deliberately simple prototype graphics. Existing maze generation, floor lifecycle, movement and fog code are retained.

## Retained dependencies

- **FishNet free 4.6.12:** copied unchanged from [FirstGearGames/FishNet](https://github.com/FirstGearGames/FishNet/tree/c73c80316b4f9d9b3d7829cf634ebca682750aca), `Assets/FishNet/`, replacing the old Pro import. Upstream license and third-party notices accompany it in that directory. Free-edition stubs are upstream code, not missing files from this cleanup.
- **Steamworks.NET:** resolved by Unity Package Manager at commit `19b6ff745d59eb5b11dc9d8a12285533b2c70431`; its package contains the relevant notices. The Steam service still requires the developer's own application setup.
- **PlayFab SDK/editor extensions:** Microsoft/PlayFab code; the official Unity SDK Apache-2.0 license is restored at `Assets/PlayFabSDK/LICENSE` ([source](https://github.com/PlayFab/UnitySDK/blob/04e73b3a78ce1afbba9f3e003f3fe51ba77bd8bd/LICENSE)).
- **Unity packages and TextMesh Pro essentials:** their existing package terms and bundled notices apply. Liberation Sans retains its SIL Open Font License; the bundled EmojiOne sample retains its attribution.

## Removed imports

Heathen Steamworks/SystemCore, Bitgem, SPUM and vHierarchy are not included. The dungeon chipset atlas and its generated tiles were replaced because their redistribution provenance was not established. None of these assets is required by the replacement artwork.

This document records provenance; it does not grant a new license to the author's game code or to third-party services.
