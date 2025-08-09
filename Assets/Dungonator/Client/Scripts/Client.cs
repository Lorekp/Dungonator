using Heathen.SteamworksIntegration.API;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dungonator.Client.Scripts
{
    public class Client : MonoBehaviour
    {
        void Awake()
        {
            SceneManager.LoadSceneAsync("MainMenuScene", LoadSceneMode.Additive);
            
#if !DISABLESTEAMWORKS
            SteamInitialization(); //Connect to steamwork services
#endif
            //TODO: add other initialization depending on platform
        }

        private void SteamInitialization()
        {
            App.isDebugging = true; //TODO: remove
            
            App.Client.Initialize(3905780);
        }
    }
}