using FishNet.Broadcast;

namespace Dungonator.Shared.Authentication
{
    public enum AuthProvider : byte
    {
        Invalid = 0,
        Steam = 1,
        Xbox = 2,
        PSN = 3
    }
    
    public struct AuthCredentialBroadcast : IBroadcast
    {
        public AuthProvider Provider;
        public string Token;
    }
}