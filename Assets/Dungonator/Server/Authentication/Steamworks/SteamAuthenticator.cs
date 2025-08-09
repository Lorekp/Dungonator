using System;
using FishNet.Authenticating;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Serializing;
using FishNet.Transporting;
using Steamworks;

namespace Dungonator.Server.Authentication.Steamworks
{
    public sealed class SteamAuthenticator : Authenticator
    {
        public static SteamAuthenticator Instance { get; private set; }
        private SteamAuthTransport _transport;
        private NetworkManager _nm;

        public override event Action<NetworkConnection,bool> OnAuthenticationResult;

        public override void InitializeOnce(NetworkManager nm)
        {
            base.InitializeOnce(nm);
            _nm = nm;
            Instance = this;
            
            // client: when transport is “Started”, we grab a ticket & send it
            nm.ClientManager.OnClientConnectionState += OnClientConnectionState;

            // server: clean up Steam sessions when they disconnect
            nm.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;
        }

        //─────────────────────────────────── CLIENT ───────────────────────────────────

        private void OnClientConnectionState(ClientConnectionStateArgs args)
        {
            if (args.ConnectionState != LocalConnectionState.Started)
                return;

            RequestSteamTicket();
        }

        private void RequestSteamTicket()
        {
            // TODO: replace with actual host SteamID (from server)
            CSteamID hostId = SteamUser.GetSteamID();

            Heathen.SteamworksIntegration.API.Authentication.GetAuthSessionTicket(hostId, (resp, ioError) =>
            {
                if (ioError || (resp.Result != EResult.k_EResultOK))
                {
                    //TODO: display error to client
                    _nm.ClientManager.StopConnection();
                    return;
                }

                byte[] ticket = resp.Data;
                // ─── write steamId + ticket into a writer ───
                var w = WriterPool.Retrieve(sizeof(ulong) + ticket.Length);
                w.WriteUInt64(SteamUser.GetSteamID().m_SteamID);
                w.WriteUInt8Array(ticket, 0, ticket.Length);

                // ─── send via a ServerRpc ───
                _transport.SubmitTicketServerRpc(w.GetArraySegment());
                WriterPool.Store(w);
            });
        }

        public void HandleTicket(NetworkConnection conn, ArraySegment<byte> payload)
        {
            if (conn == null)
                return; //TODO: send error?
            
            // read steamId + ticket
            var r = ReaderPool.Retrieve(payload, _nm, Reader.DataSource.Server);
            ulong steamId = r.ReadUInt64();
            
            byte[] ticket = new byte[r.Remaining];
            r.ReadUInt8Array(ref ticket, ticket.Length);
            ReaderPool.Store(r);

            // TODO: capacity/queue logic
            // if you want to queue or reject, just:
            //   OnAuthenticationResult?.Invoke(conn, false);
            //   return;

            // now validate with Steam
            CSteamID cId = new CSteamID(steamId);
            var begin = SteamGameServer.BeginAuthSession(ticket, ticket.Length, cId);

            bool ok = begin == EBeginAuthSessionResult.k_EBeginAuthSessionResultOK;

            if (ok)
            {
                // conn.CustomData = new PlayerData()
                // {
                //     id = steamId,
                //     batch = 0
                // };
            }
            
            OnAuthenticationResult?.Invoke(conn, ok);
        }

        //─────────────────────────────────── SERVER ───────────────────────────────────

        private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            // if (args.ConnectionState == RemoteConnectionState.Stopped
            //     && conn.CustomData is PlayerData customData)
            // {
            //     // tear down the Steam session when they disconnect
            //     Authentication.EndAuthSession(new CSteamID(customData.id));
            // }
        }
    }
}