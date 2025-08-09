using System;
using Dungonator.Shared.Authentication;
using Heathen.SteamworksIntegration.API;
using PlayFab;
using PlayFab.ClientModels;
using Steamworks;
using UnityEngine;

namespace Dungonator.Client.Scripts
{
    public interface IClientAuthProvider
    {
        AuthProvider Kind { get; }
        void GetCredential(Action<AuthCredentialBroadcast> ready);   
    }
    
    /// <summary>
    /// Returns the first credential-provider that makes sense for *this* build
    /// and *this* runtime environment.
    /// </summary>
    public static class ClientAuthPicker
    {
        /// <exception cref="NotSupportedException">
        /// Thrown if no provider is applicable (should never happen in a
        /// shipping build – fall back to GuestAuth if you need that).
        /// </exception>
        public static IClientAuthProvider ChooseProviderForThisBuild()
        {
#if !DISABLESTEAMWORKS
            /* ───── Desktop / Steam ─────────────────────────────── */
            try
            {
                if (App.Initialized) 
                    return new SteamAuth(); 
            }
            catch (DllNotFoundException)
            {
                // TODO: Steamworks.NET not present – fall through to next provider
            }
#endif

#if UNITY_GAMECORE    
            /* Xbox Series S|X / One (GDK) */
            return new XboxAuth();               // TODO: implement when needed
#endif

#if UNITY_PS5 || UNITY_PS4
            return new PsnAuth();                // TODO: implement when needed
#endif

#if UNITY_SWITCH
            return new NintendoAuth();           // TODO: implement when needed
#endif

            /* -------- Default / fallback (optional) ---------- */
            // return new GuestAuth();           // CustomID or DeviceID login
            throw new NotSupportedException(
                "No authentication provider configured for this build.");
        }
    }

#if !DISABLESTEAMWORKS //Steam
    class SteamAuth : IClientAuthProvider
    {
        private Callback<GetTicketForWebApiResponse_t> _cb;
        public AuthProvider Kind => AuthProvider.Steam;

        public void GetCredential(Action<AuthCredentialBroadcast> ready)
        {
            _cb = Callback<GetTicketForWebApiResponse_t>.Create(cb =>
            {
                string hex = BitConverter.ToString(cb.m_rgubTicket, 0, cb.m_cubTicket)
                    .Replace("-", "").ToLowerInvariant();
                
                PlayFabClientAPI.LoginWithSteam(
                    new LoginWithSteamRequest
                    {
                        TitleId = PlayFabSettings.TitleId,
                        SteamTicket             = hex,
                        TicketIsServiceSpecific = true,
                        CreateAccount           = true
                    },
                    res =>
                    {
                        ready(new AuthCredentialBroadcast
                        {
                            Provider     = Kind,
                            Token        = res.SessionTicket
                        });

                        SteamUser.CancelAuthTicket(cb.m_hAuthTicket); // tidy up
                    },
                    err => Debug.LogError(err.GenerateErrorReport()));
            });
            
            SteamUser.GetAuthTicketForWebApi("AzurePlayFab");
        }
    }
#endif
#if UNITY_GAMECORE  // Xbox GDK
class XboxAuth : IClientAuthProvider
{
    public AuthProvider Kind => AuthProvider.Xbox;
    public void GetCredential(Action<AuthCredentialBroadcast> ready)
    {
        // 1) Sign the user into Xbox Live (XalSigninAsync / GDK)
        // 2) Get an Xbox token (XUserGetTokenAndSignature)
        string token = GetXboxLiveToken();
        ready(new AuthCredentialBroadcast{ Provider = Kind, Token = token });
    }
}
#endif
}