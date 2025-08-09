using FishNet;
using FishNet.Managing.Client;
using FishNet.Object;
using UnityEngine;

namespace Dungonator.Client.Scripts
{
    public class ClientObjectSpawnListener : MonoBehaviour
    {
        [SerializeField] private NetworkObject playerPrefab;
        
        private void OnEnable()
        {
            // fires any time any NetworkObject is spawned for this client
            InstanceFinder.ClientManager.Connection.OnObjectAdded += OnClientObjectSpawned;
        }

        private void OnDisable()
        {
            InstanceFinder.ClientManager.Connection.OnObjectAdded -= OnClientObjectSpawned;
        }

        private void OnClientObjectSpawned(NetworkObject nob)
        {
            // filter only player type
            if (nob.PrefabId == playerPrefab.PrefabId)
            {
                
            }
        }
    }
}