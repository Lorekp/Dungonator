using Dungonator.Client.Steamworks;
using PlayFab.ClientModels;

namespace Dungonator.Client.PlayFab
{
    public class PlayFabAuthenticator
    {
        #if !DISABLESTEAMWORKS
        PlayFabSteamLogin steamLogin;
        #endif
        
        public void InitSteamAuth()
        {
            steamLogin = new PlayFabSteamLogin();
            steamLogin.Init();
        }
        public bool SteamAuthenticate()
        {
            LoginResult authResult = steamLogin.GetSteamAuthTicket();
            
            return authResult != null;
        }
    }
}