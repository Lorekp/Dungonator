using Dungonator.Shared.Helpers;
using FishNet.Object;
using UnityEngine;

namespace Dungonator.Client.Scripts
{
    [UnityEngine.Scripting.Preserve]
    public class ClientEventListener : MonoBehaviour
    {
        private ClientSceneManager _sceneManager;

        private void OnEnable()
        {
            EventBroadcaster.OnStartClientEvent += OnStartClient;
            _sceneManager = gameObject.GetComponent<ClientSceneManager>();
        }
        private void OnDisable() => EventBroadcaster.OnStartClientEvent -= OnStartClient;

        private void OnStartClient(NetworkBehaviour nb)
        {
            
            if (!nb.IsOwner) return;
            if (!nb.CompareTag("Player")) return;
            
            _sceneManager.OnPlayerSpawn(nb);
        }
    }
}