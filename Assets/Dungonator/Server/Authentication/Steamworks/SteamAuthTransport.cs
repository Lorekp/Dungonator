using System;
using FishNet.Connection;
using FishNet.Object;

namespace Dungonator.Server.Authentication.Steamworks
{
     public class SteamAuthTransport : NetworkBehaviour
     {
          // Sends the ticket to the server; invoked on the client
          [ServerRpc(RequireOwnership = false)]
          public void SubmitTicketServerRpc(ArraySegment<byte> ticketData,
               NetworkConnection conn = null)
          {
               // Forward to your singleton authenticator
               SteamAuthenticator.Instance.HandleTicket(conn, ticketData);
          }
     }
}