using Dungonator.Shared.Authentication;
using FishNet;
using FishNet.Transporting;
using Steamworks;
using UnityEngine;

namespace Dungonator.Client.Scripts
{
    public class ClientConnector : MonoBehaviour
    {
        [Header("Dial settings")]
        [SerializeField] private string serverAddress = "127.0.0.1";
        [SerializeField] private ushort port          = 7777;
        
        public static ClientConnector Instance {get; private set;}
        private Callback<GetTicketForWebApiResponse_t> _ticketCb;
        private AuthCredentialBroadcast _pendingBroadcast;
        
        void Awake()
        {
            if (Instance != null) {Destroy(gameObject);}
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        public void Connect()
        {
            IClientAuthProvider provider = ClientAuthPicker.ChooseProviderForThisBuild();
            
            var tug = InstanceFinder.TransportManager.Transport;
            tug.SetClientAddress(serverAddress);
            tug.SetPort(port);
            
            InstanceFinder.ClientManager.OnClientConnectionState += HandleLocalState;
            
            provider.GetCredential(broadcast =>
            {
                // socket first, then send credential
                InstanceFinder.ClientManager.StartConnection(serverAddress, port);
                _pendingBroadcast = broadcast;
            });
            
            Debug.Log($"<color=purple>[CLIENT]</color> Connecting to {serverAddress}:{port} …");
            
        }
        
        private void HandleLocalState(ClientConnectionStateArgs args)
        {
            switch (args.ConnectionState)
            {
                case LocalConnectionState.Started:
                    Debug.Log("<color=green>[CLIENT]</color> ✅ Connection established!");
                    if (_pendingBroadcast.Provider != AuthProvider.Invalid)
                    {
                        InstanceFinder.ClientManager.Broadcast(_pendingBroadcast);
                        _pendingBroadcast = default;          // clear
                    }
                    break;

                case LocalConnectionState.Stopped:
                    Debug.LogWarning("[CLIENT] ❌ Connection closed or failed.");
                    InstanceFinder.ClientManager.OnClientConnectionState -= HandleLocalState;
                    break;
            }
        }
    }
}