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
        // Scenes that are currently loaded on the client.
        private readonly HashSet<string> _loadedSceneNames = new();

        // Broadcasts we’ve received but not yet processed.
        private readonly Dictionary<string, FloorDataBroadcast> _pendingDataByScene = new();

        // Data for scenes we’ve already processed (so PlayerSpawn can use it).
        private readonly Dictionary<string, IFloorData> _readyDataByScene = new();

        /* ---------- Unity life-cycle ---------- */

        private void OnEnable()
        {
            Debug.Log("[ClientSceneManager] ENABLED");

            InstanceFinder.SceneManager.OnLoadEnd += OnLoadEnd;
            InstanceFinder.ClientManager.RegisterBroadcast<FloorDataBroadcast>(OnFloorDataBroadcast);

            _loadedSceneNames.Add("MainMenuScene");
            Debug.Log("[ClientSceneManager] Added initial scene MainMenuScene to list");
        }

        private void OnDisable()
        {
            Debug.Log("[ClientSceneManager] DISABLED");

            InstanceFinder.SceneManager.OnLoadEnd -= OnLoadEnd;
            InstanceFinder.ClientManager.UnregisterBroadcast<FloorDataBroadcast>(OnFloorDataBroadcast);
        }

        /* ---------- Broadcasts ---------- */

        private void OnFloorDataBroadcast(FloorDataBroadcast msg, Channel channel)
        {
            Debug.Log($"[ClientSceneManager] FloorDataBroadcast received: scene='{msg.SceneName}', channel={channel}");
            _pendingDataByScene.Add(msg.SceneName, msg);

            if (_loadedSceneNames.Contains(msg.SceneName))
            {
                var scene = SceneManager.GetSceneByName(msg.SceneName);
                if (scene.IsValid() && scene.isLoaded)
                    ProcessFloorDataBroadcast(scene);
            }
        }

        /* ---------- Scene events ---------- */

        private void OnLoadEnd(SceneLoadEndEventArgs args)
        {
            foreach (var scene in args.LoadedScenes)
            {
                if (scene.name == "ClientMainScene")
                    return;
                
                _loadedSceneNames.Add(scene.name);

                if (_pendingDataByScene.ContainsKey(scene.name))
                    ProcessFloorDataBroadcast(scene);
                else
                    Debug.Log($"[ClientSceneManager] Scene '{scene.name}' loaded before data; will process when broadcast arrives.");

                SceneManager.SetActiveScene(scene);
                InstanceFinder.ClientManager.Broadcast(new ClientFinishedLoadingBroadcast{});
            }
        }

        private void ProcessFloorDataBroadcast(Scene scene)
        {
            if (!_pendingDataByScene.Remove(scene.name, out var msg))
            {
                Debug.Log($"[ClientSceneManager] No pending data for scene '{scene.name}' (likely already processed).");
                return;
            }

            var data = msg.Data;
            _readyDataByScene[scene.name] = data;

            LoadFloor(data, scene.name);

            // Unload everything else
            var toUnload = _loadedSceneNames.Where(s => s != scene.name).ToList();
            foreach (var s in toUnload)
            {
                Debug.Log($"[ClientSceneManager] Unloading scene '{s}'.");
                SceneManager.UnloadSceneAsync(s);
                _readyDataByScene.Remove(s);
                _loadedSceneNames.Remove(s);
            }

            Debug.Log($"[ClientSceneManager] Scene '{scene.name}' active with floor data.");
        }

        public void OnPlayerSpawn(NetworkBehaviour nb)
        {
            var po = nb.gameObject;

            if (_readyDataByScene.TryGetValue("Floor-1", out var data)) //if floor 1
            {
                if (!FloorDataHelper.TryGetFloorData(data, out MazeFloorData mazeData))
                    throw new Exception("Invalid FloorData!");
                
                Debug.Log($"[ClientSceneManager] attaching mazedrawer");
                po.GetOrAddComponent<MazeDrawer>().Init(mazeData);
                
                var visuals = po.transform.Find("Visuals");
                visuals.gameObject.GetOrAddComponent<SpriteRenderer>().sprite = Resources.Load<Sprite>("Player/debugplayer");
                po.GetOrAddComponent<PlayerInput>().actions = Resources.Load<InputActionAsset>("Input/Player");
                
                AttachPixelPerfectCamera(po);
            }

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
