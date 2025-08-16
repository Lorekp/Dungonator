using System;
using System.Collections.Generic;
using System.Linq;
using Dungonator.Server.Layers.Maze.Floors.FloorGenData;
using Dungonator.Server.Layers.Maze.Scripts.MazeGeneration;
using Dungonator.Shared.Dungeon;
using Dungonator.Shared.Helpers;
using FishNet;
using FishNet.Component.Spawning;
using FishNet.Connection;
using FishNet.Managing.Scened;
using FishNet.Managing.Server;
using FishNet.Object;
using FishNet.Transporting;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Grid = UnityEngine.Grid;
using SceneManager = FishNet.Managing.Scened.SceneManager;

/*
 * Handles which scene each player should load.
 *  • Floor 0  → multiple “Tutorial-0” instances (capacity-limited)
 *  • Floor >0 → exactly one scene per floor (“Floor-1”, “Floor-2”, …)
 */
namespace Dungonator.Server.Layers
{
    public class FloorManager : MonoBehaviour
    {
        [Header("Scene Data")]
        [SerializeField] private Scene tutorialScene;
        [SerializeField] private MazeGenFloorGenData[] mazeGenFloorData;

        [Header("Caps")]
        [SerializeField] private int tutorialCap = 20;              // players per tutorial instance
        
        [Header("Scripts & Prefabs")]
        [SerializeField] private Server server;
        [SerializeField] private NetworkObject playerPrefab;

        /* floor-index → list of instances */
        private readonly Dictionary<int, FloorInstance> _floors = new();
        private readonly List<FloorInstance> _tutorialInstances = new();
        
        private readonly List<QueuedPlayer> _tutorialQueue = new();
        
        private MazeFloorGenerator _mazeFloorGenerator;

        private struct QueuedPlayer
        {
            public NetworkConnection Connection;
            public PlayerData PlayerData;
        }
        
        private void Awake()
        {
            _mazeFloorGenerator = new MazeFloorGenerator();
        }

        private void OnClientFinishedLoading(NetworkConnection conn, ClientFinishedLoadingBroadcast _, Channel channel)
        {
            if (!server._players.TryGetValue(conn, out var pd))
                return;
            
            if (_floors.TryGetValue(pd.CurrentFloor, out var fi))
            {
                // Physics ready → spawn now
                SpawnPlayer(conn, fi);
                return;
            }

            foreach (var ti in _tutorialInstances)
            {
                if (ti.Connections.Contains(conn)) continue;
                
                SpawnPlayer(conn, ti);
                return;
            }
        }

        private void SpawnPlayer(NetworkConnection connection, FloorInstance fi)
        {
            var floorData = fi.Data;
            
            if (floorData.SpawnPointIndex > floorData.SpawnPoints.Length - 1)
                floorData.SpawnPointIndex = 0;
            
            var idx = floorData.SpawnPointIndex;
            
            var pob = Instantiate(playerPrefab, new Vector3 (floorData.SpawnPoints[idx].x, floorData.SpawnPoints[idx].y, 0f), Quaternion.identity);
            
            var scene = SceneManager.GetScene(fi.Handle);
            
            
            fi.PendingConnections.Remove(connection);
            fi.Connections.Add(connection);
            InstanceFinder.ServerManager.Spawn(pob, connection, scene);
        }

        /// <summary>Call when a client moves to another floor.</summary>
        public void JoinNewFloor(NetworkConnection conn, int newFloor, PlayerData pd)
        {
            Debug.Log($"{pd.DisplayName} is joining floor {newFloor}");
            if (newFloor == 0) //if joining tutorialFloor
            {
                QueueTutorial(conn, pd);
                return;
            }
            
            if (_floors.TryGetValue(newFloor, out var instance)) //already existing instance
            {
                instance.PendingConnections.Add(conn);
                InstanceFinder.SceneManager.LoadConnectionScenes(conn, new SceneLoadData(instance.Handle));
                return;
            }

            if (!FloorLookup.TryGetSceneName(newFloor, out var sceneName))
                throw new NotImplementedException("Floor name doesn't exist");
            
            if (server._players.TryGetValue(conn, out var spd))
            {
                spd.CurrentFloor = newFloor;
                server._players[conn] = spd;
            } //update current floor
            CreateSingleFloor(newFloor, conn);
        }

        public void RemovePlayer(NetworkConnection conn, PlayerData pd) //removes playerconn from floorinstance
        {
            if (pd.CurrentFloor == 0)
            {
                foreach (var fi in _tutorialInstances)
                    fi.Connections.Remove(conn);
                return;
            }
            _floors[pd.CurrentFloor].Connections.Remove(conn);
        }

        private void QueueTutorial(NetworkConnection conn, PlayerData pd)
        {
            _tutorialQueue.Add(new QueuedPlayer { Connection = conn, PlayerData = pd });

            /* When queue reaches cap, spawn a new instance, when scene finishes loading in OnLoadEnd, batch gets drained*/
            if (_tutorialQueue.Count >= tutorialCap)
                CreateTutorialInstance();
        }
        
        private void LoadForConnection(NetworkConnection conn,
            PlayerData pd, FloorInstance fi)
        {
            var sld = new SceneLoadData(fi.Handle);
            InstanceFinder.SceneManager.LoadConnectionScenes(conn, sld);

            // don’t send data/spawn until physics is ready
            if (!fi.PhysicsReady)
            {
                fi.PendingConnections.Add(conn);
                return;
            }

            Debug.Log($"{pd.DisplayName} is loading floor {pd.CurrentFloor}");
            // (your existing broadcast/spawn order)
            InstanceFinder.ServerManager.Broadcast(conn,
                new FloorDataBroadcast { Data = fi.Data, SceneName = fi.SceneName });
        }

        private void CreateTutorialInstance()
        {
            var newFloorInstance = new FloorInstance { Connections = new HashSet<NetworkConnection>(), HasScene = false, SceneName = tutorialScene.name}; 
            
            _tutorialInstances.Add(newFloorInstance);
            
            InstanceFinder.SceneManager.LoadConnectionScenes(new SceneLoadData(tutorialScene.name));
        }

        private void CreateSingleFloor(int floor, NetworkConnection conn = null)
        {
            if (!FloorLookup.TryGetSceneName(floor, out var sceneName))
                throw new ArgumentOutOfRangeException(nameof(floor), $"No scene registered for floor {floor} in FloorName class");

            var floorData = GenerateFloorData(floor); //generates the floor and saves the layout

            var newInstance = new FloorInstance
            {
                Data = floorData,
                HasScene    = false,
                Connections = new HashSet<NetworkConnection>(),
                PendingConnections = new HashSet<NetworkConnection>(),
                SceneName = sceneName
            };

            _floors[floor] = newInstance;
            
            if (conn != null)
                newInstance.PendingConnections.Add(conn);

            var sld = new SceneLoadData(sceneName)
            {
                // keep this floor alive on the server even when empty
                Options = { AutomaticallyUnload = false }
            };
            // this only loads on the server; clients get scenes via OnLoadEnd
            InstanceFinder.SceneManager.LoadConnectionScenes(sld);
        }

        private IFloorData GenerateFloorData(int floor)
        {
            switch (FloorLookup.GetLayerNumber(floor))
            {
                case 1:
                    return GenerateMazeFloorData(floor);
            }
            
            throw new NotSupportedException("Not (yet) supported layer");
        }

        private MazeFloorData GenerateMazeFloorData(int floor)
        {
            var genData = mazeGenFloorData[floor - 1]; //get correct data in array, floor always one higher
            return _mazeFloorGenerator.GenerateFloorData(genData);
        }

        private void OnFloorSetupEnd(FloorInstance fi)
        {
            if (fi.Data is MazeFloorData mazeData)
            {
                var scene = SceneManager.GetScene(fi.Handle);
                var parent = new GameObject("MazePhysics");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(parent, scene);

                var handle = MazePhysicsSetup.BuildServerColliders(
                    mazeData, parent.transform,
                    new MazePhysicsSetup.BuildOptions { perFrame = 400, generateEvery = 3000 });

                handle.OnCompleted += () =>
                {
                    Debug.Log("Finished building maze colliders!");
                    fi.PhysicsReady = true;

                    // snapshot to avoid modify-while-iterating
                    var toSpawn = fi.PendingConnections.ToArray();
                    fi.PendingConnections.Clear();

                    foreach (var conn in toSpawn)
                    {
                        if (!server._players.TryGetValue(conn, out var pd))
                            return;
                            
                        LoadForConnection(conn, pd, fi);
                    }
                };
            }
        }

        private void OnLoadEnd(SceneLoadEndEventArgs args)
        {
            if (!args.QueueData.AsServer) return;
            foreach (var scene in args.LoadedScenes) //assigns loaded scenes handle to either floor or tutorial instance list and connects players that are waiting to join
            {
                Debug.Log("A Scene finished loading!" + scene.name);
                if (scene.name == tutorialScene.name)
                    HandleLoadedTutorialScene(scene);
                else
                    HandleLoadedFloorScene(scene);
            }
        }

        private void HandleLoadedFloorScene(Scene scene)
        {
            //load loaded scene for all connections in FloorInstance
            FloorLookup.TryGetFloorNumber(scene.name, out var floorNumber);
            var instance = _floors[floorNumber];
            instance.Handle = scene.handle;
            instance.HasScene = true;

            if (instance.PhysicsReady)
            {
                // take snapshot, clear, then process
                var toLoad = instance.PendingConnections.ToArray();
                instance.PendingConnections.Clear();

                foreach (var connection in toLoad)
                {
                    if (!server._players.TryGetValue(connection, out var playerData))
                        continue;
                    LoadForConnection(connection, playerData, instance);
                }
            }
            
            _floors[floorNumber] = instance;
            OnFloorSetupEnd(instance);
        }

        private void HandleLoadedTutorialScene(Scene scene)
        {
            for (var i = 0; i < _tutorialInstances.Count; i++)
            {
                var inst = _tutorialInstances[i];
                        
                if (inst.HasScene) continue;
                        
                inst.Handle = scene.handle;
                inst.HasScene = true;
                            
                /* pop exactly tutorialCap players */
                for (var j = 0; j < tutorialCap; j++)
                {
                    var qp = _tutorialQueue[0];
                    _tutorialQueue.RemoveAt(0);

                    inst.PendingConnections.Add(qp.Connection);
                    LoadForConnection(qp.Connection, qp.PlayerData, inst);
                }
                            
                _tutorialInstances[i] = inst;
                OnFloorSetupEnd(inst);
                return;
            }
        }

        private void OnClientPresenceChangeEnd(ClientPresenceChangeEventArgs args)
        {
            if (!args.Added &&
                InstanceFinder.SceneManager.SceneConnections[args.Scene].Count == 0) //if client left an empty scene
            {
                HandleEmptyScene(args.Scene);
            }
        }

        private void HandleEmptyScene(Scene scene)
        {
            if (scene.name == tutorialScene.name)
            {
                // 1) Unload & destroy immediately
                var sud = new SceneUnloadData(scene)
                {
                    Options = { Mode = UnloadOptions.ServerUnloadMode.UnloadUnused }
                };
                InstanceFinder.SceneManager.UnloadConnectionScenes(sud);

                // 2) Remove the corresponding FloorInstance
                _tutorialInstances.RemoveAll(fi => fi.Handle == scene.handle);
                return;
            }
            
            CacheFloor(scene);
        }

        private void CacheFloor(Scene scene)
        {
            throw new NotImplementedException("CacheFloor");
        }
        
        private Vector3 MazeCellCenterWorld(int row, int col, MazeFloorData data, Grid grid)
        {
            var tilesPerMazeCell = data.CellSize;
            // Translate maze-cell to *tile* coordinates
            var tileX = col * tilesPerMazeCell + tilesPerMazeCell / 2;
            var tileY = row * tilesPerMazeCell + tilesPerMazeCell / 2;

            Vector3Int tilePos = new(tileX, tileY, 0);
            return grid.GetCellCenterWorld(tilePos);   // center of that tile
        }

        private void OnEnable()
        {
            InstanceFinder.SceneManager.OnLoadEnd += OnLoadEnd;
            InstanceFinder.SceneManager.OnClientPresenceChangeEnd += OnClientPresenceChangeEnd;;
            InstanceFinder.ServerManager.RegisterBroadcast<ClientFinishedLoadingBroadcast>(
                OnClientFinishedLoading);
        }

        private void OnDisable()
        {
            InstanceFinder.SceneManager.OnLoadEnd -= OnLoadEnd;
            InstanceFinder.SceneManager.OnClientPresenceChangeEnd -= OnClientPresenceChangeEnd;
            InstanceFinder.ServerManager.UnregisterBroadcast<ClientFinishedLoadingBroadcast>(
                OnClientFinishedLoading);
        }
    }
}