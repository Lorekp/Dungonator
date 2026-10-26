using System.Collections.Generic;
using Dungonator.Server.Layers;
using FishNet;
using FishNet.Connection;
using FishNet.Object;
using FishNet.Transporting;
using PlayFab;
using PlayFab.ServerModels;
using UnityEngine;

namespace Dungonator.Server
{
    public class Server : NetworkBehaviour
    {
        [SerializeField] private string titleId        = "1BBC8F";

        [SerializeField] private FloorManager floorManager;

        public Dictionary<NetworkConnection, PlayerData> _players =  new();

        [SerializeField]
        private ushort port = 7777;            // Change if you modified Tugboat.

        private void OnEnable()
        {
            // fires once per remote client
            InstanceFinder.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
            InstanceFinder.ServerManager.OnAuthenticationResult += OnAuthenticationResult;
        }

        private void OnDisable()
        {
            InstanceFinder.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            InstanceFinder.ServerManager.OnAuthenticationResult -= OnAuthenticationResult;
        }

        private void Awake()
        {
            PlayFabSettings.staticSettings.TitleId            = titleId;
            PlayFabSettings.staticSettings.DeveloperSecretKey = System.Environment.GetEnvironmentVariable("DUNGONATOR_PLAYFAB_SECRET");

            if (string.IsNullOrEmpty(PlayFabSettings.staticSettings.DeveloperSecretKey))
                Debug.LogError("DeveloperSecretKey is missing! PlayFab Server calls will fail.");

            var tug = InstanceFinder.TransportManager.Transport;
            tug.SetPort(port);

            InstanceFinder.ServerManager.StartConnection();

            var address = InstanceFinder.TransportManager.Transport.GetPort();
            Debug.Log($"<color=cyan>[SERVER]</color> Listening on UDP 0.0.0.0:{address.ToString()}");
        }

        private void OnAuthenticationResult(NetworkConnection conn, bool success)
        {
            if (success)
            {
                Debug.Log($"[{conn.ClientId}] joined with {_players[conn].DisplayName}");

                if (_players.TryGetValue(conn, out var pd)) //join floor player left on/ tutorial
                    floorManager.JoinNewFloor(conn, pd.CurrentFloor, pd);

                return;
            }

            conn.Disconnect(false);
        }

        private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            if (args.ConnectionState == RemoteConnectionState.Started)
                Debug.Log($"<color=green>[SERVER]</color> Client {conn.ClientId} connecting...");
            else if (args.ConnectionState == RemoteConnectionState.Stopped && conn.IsAuthenticated)
            {
                if (_players.TryGetValue(conn, out var pd))
                    SavePlayerData(pd);
                _players.Remove(conn);

                floorManager.RemovePlayer(conn, pd);

                Debug.LogWarning($"[SERVER] Client {conn.ClientId} disconnected.");
            }
        }

        private static void SavePlayerData(PlayerData pd)
        {
            var kvp = new Dictionary<string, string>
            {
                { "DisplayName",  pd.DisplayName },
                { "SteamID",    pd.SteamID },
                { "CurrentFloor", pd.CurrentFloor.ToString() },

                // any other fields in PlayerData
            };

            PlayFabServerAPI.UpdateUserData(
                new UpdateUserDataRequest
                {
                    PlayFabId   = pd.PlayFabId,
                    Data        = kvp,
                    Permission  = UserDataPermission.Private
                },
                res  => Debug.Log($"[SERVER] Saved data for {pd.DisplayName} (ver {res.DataVersion})"),
                err =>
                {
                    Debug.LogError($"Save failed for {pd.DisplayName}: {err.GenerateErrorReport()}");
                    /* optional retry / queue for later */
                });
        }

        public void RegisterPlayer(NetworkConnection conn, PlayerData pd) =>
            _players[conn] = pd;

    }
}
