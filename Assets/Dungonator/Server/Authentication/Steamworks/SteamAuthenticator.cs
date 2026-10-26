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

        private HAuthTicket _ticket = HAuthTicket.Invalid;
        private Callback<GetAuthSessionTicketResponse_t> _ticketResponse;

        private void RequestSteamTicket()
        {
            if (_transport == null)
                _transport = GetComponent<SteamAuthTransport>();
            if (_transport == null)
            {
                _nm.ClientManager.StopConnection();
                return;
            }

            var bytes = new byte[1024];
            uint length = 0;
            var identity = new SteamNetworkingIdentity();
            identity.SetSteamID(SteamUser.GetSteamID()); // Legacy self-host prototype; configure the actual host for deployment.
            _ticketResponse?.Dispose();
            _ticketResponse = Callback<GetAuthSessionTicketResponse_t>.Create(response =>
            {
                if (response.m_hAuthTicket != _ticket) return;
                if (response.m_eResult != EResult.k_EResultOK)
                {
                    _nm.ClientManager.StopConnection();
                    return;
                }
                var writer = WriterPool.Retrieve(sizeof(ulong) + (int)length);
                writer.WriteUInt64(SteamUser.GetSteamID().m_SteamID);
                writer.WriteUInt8Array(bytes, 0, (int)length);
                _transport.SubmitTicketServerRpc(writer.GetArraySegment());
                WriterPool.Store(writer);
            });
            _ticket = SteamUser.GetAuthSessionTicket(bytes, bytes.Length, out length, ref identity);
        }

        private void OnDestroy()
        {
            _ticketResponse?.Dispose();
            if (_ticket != HAuthTicket.Invalid) SteamUser.CancelAuthTicket(_ticket);
            if (_nm == null) return;
            _nm.ClientManager.OnClientConnectionState -= OnClientConnectionState;
            _nm.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
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
