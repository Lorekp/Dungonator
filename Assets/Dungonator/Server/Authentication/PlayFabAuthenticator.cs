using System;
using Dungonator.Shared.Authentication;
using FishNet;
using FishNet.Authenticating;
using FishNet.Connection;
using FishNet.Transporting;
using PlayFab;
using PlayFab.ServerModels;
using UnityEngine;
using GetUserDataRequest = PlayFab.ServerModels.GetUserDataRequest;
using LoginWithPSNRequest = PlayFab.ServerModels.LoginWithPSNRequest;
using LoginWithXboxRequest = PlayFab.ServerModels.LoginWithXboxRequest;

namespace Dungonator.Server.Authentication
{
    public class PlayFabAuthenticator : Authenticator
    {
        private Server _server;
        
        public override event Action<NetworkConnection,bool> OnAuthenticationResult;
        
        private void Awake()
        {
            InstanceFinder.ServerManager.RegisterBroadcast<AuthCredentialBroadcast>(
                OnAuth, requireAuthentication:false);

            _server = GetComponentInParent<Server>();
        }

        private void OnAuth(NetworkConnection conn,
            AuthCredentialBroadcast msg,
            Channel _)
        {
            switch (msg.Provider)
            {
                case AuthProvider.Steam:
                    PlayFabServerAPI.AuthenticateSessionTicket(
                        new AuthenticateSessionTicketRequest{ SessionTicket = msg.Token },
                        ok  =>
                        {
                            var playFabId = ok.UserInfo.PlayFabId;
                            PlayFabServerAPI.GetUserAccountInfo(
                                new GetUserAccountInfoRequest { PlayFabId = playFabId },
                                acc =>
                                {
                                    Success(conn, playFabId, acc.UserInfo.SteamInfo.SteamName, acc.UserInfo.SteamInfo.SteamId);
                                },
                                err => Fail(conn, err));
                        },
                        err => Fail(conn, err));
                    break;

                case AuthProvider.Xbox:
                    PlayFabServerAPI.LoginWithXbox(
                        new LoginWithXboxRequest{ XboxToken = msg.Token, CreateAccount = true },
                        ok  => Success(conn, ok.PlayFabId),
                        err => Fail(conn, err));
                    break;

                case AuthProvider.PSN:
                    PlayFabServerAPI.LoginWithPSN(
                        new LoginWithPSNRequest{ AuthCode = msg.Token, CreateAccount = true },
                        ok  => Success(conn, ok.PlayFabId),
                        err => Fail(conn, err));
                    break;

                case AuthProvider.Invalid:
                default:
                    OnAuthenticationResult?.Invoke(conn,false);
                    break;
            }
        }

        //TODO: get names from services (steam etc.) (update names on every login?)
        private void Success(NetworkConnection conn, string playFabId, string displayName = "fallbackDisplayName", string steamId = null) 
        {
            PlayFabServerAPI.GetUserData(
                new GetUserDataRequest
                {
                    PlayFabId = playFabId
                },
                res =>
                {
                    PlayerData pd;
                    
                    if (res.Data == null || res.Data.Count == 0)
                        pd = PlayerData.SeedPlayerData(playFabId, displayName, steamId); //new player
                    else
                        pd = PlayerData.From(res.Data, playFabId);     
                    
                    _server.RegisterPlayer(conn, pd);
                    OnAuthenticationResult?.Invoke(conn, true); 
                },
                err =>
                {
                    Fail(conn, err);
                } 
                );
        }
        private void Fail(NetworkConnection c, PlayFabError err)
        {
            Debug.LogError($"PlayFab failed: {err.GenerateErrorReport()}");
            OnAuthenticationResult?.Invoke(c,false);
        }
    }
}