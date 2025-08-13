using System;
using System.Collections.Generic;
using System.Linq;
using Dungonator.Client.Layers.Maze;
using Dungonator.Shared.Dungeon;
using Dungonator.Shared.Helpers;
using FishNet;
using FishNet.Managing.Scened;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.U2D;
using SceneManager = UnityEngine.SceneManagement.SceneManager;

namespace Dungonator.Client.Scripts
{
    public class ClientSceneManager : MonoBehaviour
    {
        private readonly Dictionary<string, FloorDataBroadcast> _pendingFloorDataBroadcasts = new();
        private readonly Dictionary<string, bool> _pendingFloorDataBroadcastsReady = new();
        private readonly Dictionary<string, IFloorData> _loadedScenes = new();

        /* ---------- Unity life-cycle ---------- */

        private void OnEnable()
        {
            Debug.Log("[ClientSceneManager] ENABLED");

            InstanceFinder.SceneManager.OnLoadEnd += OnLoadEnd;
            InstanceFinder.ClientManager.RegisterBroadcast<FloorDataBroadcast>(OnFloorDataBroadcast);

            _loadedScenes.Add("MainMenuScene", null);
            Debug.Log("[ClientSceneManager] Added initial scene MainMenuScene to list");
        }

        private void OnDisable()
        {
            Debug.Log("[ClientSceneManager] DISABLED");

            InstanceFinder.SceneManager.OnLoadEnd -= OnLoadEnd;
            InstanceFinder.ClientManager.UnregisterBroadcast<FloorDataBroadcast>(OnFloorDataBroadcast);
        }

        /* ---------- Broadcasts ---------- */

        private void OnFloorDataBroadcast(FloorDataBroadcast floorData, Channel channel)
        {
            Debug.Log($"[ClientSceneManager] FloorDataBroadcast received: " +
                      $"scene='{floorData.SceneName}', channel={channel}");

            _pendingFloorDataBroadcasts[floorData.SceneName] = floorData;
        }

        /* ---------- Scene events ---------- */

        private void OnLoadEnd(SceneLoadEndEventArgs args)
        {
            // Which scene that just finished loading matches our pending broadcasts?
            var match = args.LoadedScenes
                .FirstOrDefault(s => _pendingFloorDataBroadcasts.ContainsKey(s.name));

            if (match == default)
            {
                Debug.Log("[ClientSceneManager] OnLoadEnd fired, " +
                          "but no matching pending broadcast");
                return;
            }

            // Make the new dungeon scene active BEFORE unloading others
            SceneManager.SetActiveScene(match);
            _pendingFloorDataBroadcastsReady.Add(match.name, true);
            InstanceFinder.ClientManager.Broadcast(new ClientFinishedLoadingBroadcast{});
        }

        private void ProcessFloorDataBroadcast(Scene scene)
        {
            // Pull out and remove the stored data
            var data = _pendingFloorDataBroadcasts[scene.name].Data;
            _pendingFloorDataBroadcasts.Remove(scene.name);
            _pendingFloorDataBroadcastsReady.Remove(scene.name);

            // Build the floor contents
            LoadFloor(data, scene.name); ;

            // Unload any previous scenes (e.g. MainMenuScene)
            foreach (var sceneName in _loadedScenes.Keys
                         .Where(s => s != scene.name)
                         .ToList())
            {
                SceneManager.UnloadSceneAsync(sceneName);
                _loadedScenes.Remove(sceneName);
            }

            // Track currently-loaded scene
            _loadedScenes.Add(scene.name, data);
        }

        public void OnPlayerSpawn(NetworkBehaviour nb)
        {
            var po = nb.gameObject;

            if (_pendingFloorDataBroadcasts.TryGetValue("Floor-1", out var data)) //if floor 1
            {
                if (!FloorDataHelper.TryGetFloorData(data.Data, out MazeFloorData mazeData))
                    throw new Exception("Invalid FloorData!");
                
                Debug.Log($"[ClientSceneManager] attaching mazedrawer");
                po.GetOrAddComponent<MazeDrawer>().Init(mazeData);
                
                var visuals = po.transform.Find("Visuals");
                visuals.gameObject.GetOrAddComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>("Player/debugplayer");
                po.GetOrAddComponent<PlayerInput>().actions = Resources.Load<InputActionAsset>("Input/Player");
                
                AttachPixelPerfectCamera(po);
                //Camera setup for top-down 2D gameplay
                /*var camPivot = new GameObject("CameraPivot");
                camPivot.transform.SetParent(po.transform, false);

                var cam = camPivot.GetOrAddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.backgroundColor = Color.black;
                cam.orthographic = true;
                cam.orthographicSize = 7f; // Zoom level
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 100f;
                cam.cullingMask = -1; // Show all layers
                cam.transform.localPosition = new Vector3(0, 0, -10f); // Above the player*/
            }
            
            ProcessFloorDataBroadcast(po.scene);
        }

        /* ---------- helpers ---------- */
        
        void AttachPixelPerfectCamera(GameObject po,
            int assetsPPU = 16,              // pixels-per-unit of your sprites/tiles
            int tilesVisibleY = 10,          // how many tiles tall you want to view
            float aspect = 16f / 9f)         // your target aspect ratio
        {
            // --- camera pivot (nice for shake/rotation without skewing pixels) ---
            var camPivot = new GameObject("CameraPivot");
            camPivot.transform.SetParent(po.transform, false);
            camPivot.transform.localPosition = Vector3.zero;

            // --- camera ---
            var cam = camPivot.GetOrAddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane  = 100f;
            cam.cullingMask = ~0;
            cam.transform.localPosition = new Vector3(0f, 0f, -1f);

            // --- pixel-perfect camera (the important part) ---
            var ppc = camPivot.GetComponent<PixelPerfectCamera>();
            if (ppc == null) ppc = camPivot.AddComponent<PixelPerfectCamera>();

            // Your sprite import PPU (e.g., 16 if a 16×16 tile equals 1 world unit)
            ppc.assetsPPU = assetsPPU;

            // Set a reference resolution that matches how many tiles you want to see vertically
            // If 20 tiles tall at 16 PPU -> 20*16 = 320 vertical pixels
            var refY = tilesVisibleY * assetsPPU;
            var refX = Mathf.RoundToInt(refY * aspect);
            ppc.refResolutionX = refX;
            ppc.refResolutionY = refY;

            // Commonly good defaults for crisp visuals:
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.None;

            // NOTE: Do NOT manually change cam.orthographicSize; PixelPerfectCamera controls it.
        }

        private void LoadFloor(IFloorData data, string sceneName)
        {
            switch (data.LayerIndex)
            {
                case 1:
                    LoadMazeFloor(data, sceneName);
                    break;
                default:
                    throw new NotImplementedException(
                        $"Layer index {data.LayerIndex} is not (yet) implemented.");
            }
        }

        private void LoadMazeFloor(IFloorData data, string sceneName)
        {
            
        }
    }
}
