using System.Text;
using PlayFab;
using PlayFab.ClientModels;
using Steamworks;
using UnityEngine;

// Toolkit wrapper

namespace Dungonator.Client.Steamworks
{
    public class PlayFabSteamLogin
    {
        private Callback<GetTicketForWebApiResponse_t> _ticketCb;
        private HAuthTicket _hTicket = HAuthTicket.Invalid;

        private LoginResult AuthResult;

        public void Init() => _ticketCb = Callback<GetTicketForWebApiResponse_t>.Create(OnTicket);

        public LoginResult GetSteamAuthTicket()
        {
            GenerateSteamAuthTicket();
            return AuthResult;
        }

        private void GenerateSteamAuthTicket() => _hTicket = SteamUser.GetAuthTicketForWebApi("AzurePlayFab"); //call this to Init playfab auth

        private void OnTicket(GetTicketForWebApiResponse_t cb)
        {
            var hex = new StringBuilder();
            for (int i = 0; i < cb.m_cubTicket; i++) hex.AppendFormat("{0:x2}", cb.m_rgubTicket[i]);

            PlayFabClientAPI.LoginWithSteam(new LoginWithSteamRequest
                {
                    TitleId               = PlayFabSettings.TitleId,
                    CreateAccount         = true,
                    SteamTicket           = hex.ToString(),
                    TicketIsServiceSpecific = true   // because we used GetAuthTicketForWebApi
                },
                res =>
                {
                    Debug.Log($"Logged in! PlayFabId={res.PlayFabId}");
                    SteamUser.CancelAuthTicket(_hTicket);

                    AuthResult = res;
                },
                err =>
                {
                    Debug.LogError(err.GenerateErrorReport());
                    SteamUser.CancelAuthTicket(_hTicket);
                });
        }
    }
}