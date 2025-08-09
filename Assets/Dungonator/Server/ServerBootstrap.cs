
using FishNet.Object;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dungonator.Server
{
    public class ServerBootstrap : MonoBehaviour
    {
        [Server]
        void Awake()
        {
            SceneManager.LoadSceneAsync("ServerMainScene", LoadSceneMode.Additive);
        }
        
    }
}
