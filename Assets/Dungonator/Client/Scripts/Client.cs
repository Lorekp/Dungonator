using System;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Dungonator.Client.Scripts
{
    public class Client : MonoBehaviour
    {
        public static bool SteamInitialized { get; private set; }

        private void Awake()
        {
            SceneManager.LoadSceneAsync("MainMenuScene", LoadSceneMode.Additive);
#if !DISABLESTEAMWORKS
            try
            {
                SteamInitialized = SteamAPI.Init();
                if (!SteamInitialized)
                    Debug.LogWarning("Steam initialization failed. Run Steam and configure your development App ID before login.");
            }
            catch (DllNotFoundException)
            {
                Debug.LogWarning("Steam native library is unavailable; Steam login is disabled.");
            }
#endif
        }

        private void Update()
        {
#if !DISABLESTEAMWORKS
            if (SteamInitialized) SteamAPI.RunCallbacks();
#endif
        }

        private void OnDestroy()
        {
#if !DISABLESTEAMWORKS
            if (SteamInitialized) SteamAPI.Shutdown();
#endif
            SteamInitialized = false;
        }
    }
}
