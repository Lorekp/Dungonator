# Dungonator

Dungonator is a Unity prototype for a networked, top-down dungeon game. It explores procedural maze floors, player movement with FishNet prediction, Steam authentication through PlayFab, and a client-side fog-of-war effect. The repository contains substantial gameplay infrastructure, but the current project is unfinished and does not provide a verified, ready-to-play build.

## Implemented systems

- **Maze generation:** an iterative recursive-backtracker algorithm creates connected cell layouts, with player spawn positions and boss/challenge markers. Maze topology can use a configured seed; marker placement uses Unity's separate random generator, so the entire floor is not reproducible from that seed alone.
- **Networked floor lifecycle:** the server generates floor data, builds collision geometry, loads connection-specific scenes, and broadcasts floor data. The client coordinates scene loading and received data before attaching maze rendering and player components.
- **Movement:** a 2D rigidbody controller uses FishNet replication and reconciliation with Unity Input System input.
- **Authentication and player data:** Steam Web API tickets are exchanged for PlayFab session tickets. Server-side code validates credentials, loads player metadata, and attempts to save the current floor on disconnect.
- **Presentation:** pixel-perfect camera setup, maze rendering, an exploration texture for fog of war, and a UI Toolkit server-browser prototype.

Boss and challenge markers are generation metadata; they are not evidence of completed combat or encounters. Console authentication branches are placeholders rather than supported platform integrations.

## Project layout

| Directory | Purpose |
| --- | --- |
| `Assets/Dungonator/Client/` | Connection setup, Steam/PlayFab client authentication, menus, rendering, camera and fog |
| `Assets/Dungonator/Server/` | Authentication, player records, floor lifecycle, maze generation and server collision |
| `Assets/Dungonator/Shared/` | Network broadcasts, floor data and serialization, movement prediction, shared scenes |
| `Assets/Editor/` | Maze-cell template editor |
| `Packages/` | Unity dependency manifest, lockfile and embedded Steamworks toolkit |
| `ProjectSettings/` | Unity version, input, rendering and build configuration |

FishNet, PlayFab, Heathen tools and other imported asset packages are dependencies, not original Dungonator implementations.

## Opening the project

The current project needs the configuration and repairs below before multiplayer testing.

1. Install **Unity 6000.2.10f1** through Unity Hub. Install the target platform's build support if you intend to produce a player or dedicated server.
2. Obtain an authorized checkout and add its root directory as a project in Unity Hub.
3. Allow Unity to restore packages. Git must be available for the Git-based SystemCore and Steamworks.NET dependencies. The manifest includes URP 17.2.0, Input System 1.14.2, Dedicated Server 1.6.1 and Unity Test Framework 1.6.0.
4. Resolve import and compile errors before entering Play mode. In particular, check the PlayFab data-model mismatch between `Server/PlayerData.cs` and `Server/Authentication/PlayFabAuthenticator.cs`: the former accepts client-model user-data records while the latter retrieves server-model records.
5. Configure your own Steam application and PlayFab title, including their Steam login integration. The desktop authentication picker requires initialized Steam; it has no guest/offline fallback. Configure the server's PlayFab credential through a private server-only configuration. The source default is empty and server API calls fail without a valid credential.
6. Inspect `ClientMainScene.unity`, `UI/MainMenu/MainMenuScene.unity`, `ServerMainScene.unity` and the shared `Floor-1.unity` scene. Check Inspector references, transport configuration, generation assets and scene inclusion before attempting separate client/server sessions.

The committed build settings enable only the main-menu and server scenes. They do not constitute a complete client/server build configuration: the client bootstrap and generated-floor scene also need deliberate setup. There is no repository build script or documented command-line launcher.

## Current limitations and checking changes

- Only the maze layer is implemented; the client player setup explicitly targets `Floor-1`.
- Floor caching throws `NotImplementedException`. The tutorial queue waits for its configured capacity and its instance setup remains incomplete.
- The Steam server browser requests LAN servers; its selected-server connection code is commented out.
- The client sends its loading notification from the scene-load callback. The scene/data ordering and collision readiness require multiplayer testing.
- Fog updates scan and upload the full texture on movement. This is a prototype effect without a demonstrated performance budget.
- Dynamic enemy spawning is a TODO. There is no demonstrated complete dungeon progression or release workflow.

No project-specific automated test suite or GitHub Actions workflow is provided in the repository. Unity Test Framework is installed, but that alone does not establish test coverage. After repairing the setup, useful manual checks are a two-client login/spawn session, movement reconciliation, floor reconnects, persistence after disconnect, and maze generation with repeated seeds. These checks are recommendations, not recorded passing results.

## Dependencies and licensing

No project-level license is supplied. Included SDKs, artwork and editor assets retain their respective authorship and license terms. Preserve their notices, including `Assets/FishNet/LICENSE.txt` and `Packages/com.heathen.steamworks/LICENSE.md`.

The Heathen toolkit includes restrictions on source distribution. Other imported assets include Bitgem, SPUM and vHierarchy content. Access to this repository does not grant blanket permission to redistribute those packages or the project; obtain the necessary licenses and permissions before sharing source or shipping a build.
