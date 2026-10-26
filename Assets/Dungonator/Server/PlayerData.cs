using System;
using System.Collections.Generic;
using PlayFab.ServerModels;

namespace Dungonator.Server
{
    [Serializable]
    public struct PlayerData
    {
        public string PlayFabId;
        public string SteamID;

        public string DisplayName;

        public int    CurrentFloor;

        public static PlayerData From(Dictionary<string, UserDataRecord> src, string playFabId)
        {
            PlayerData p = new()
            {
                PlayFabId   = playFabId,
                DisplayName = "fallbackDisplayName",
                CurrentFloor = 0
            };

            if (src.TryGetValue("DisplayName", out var dn))
                p.DisplayName = dn.Value;

            if (src.TryGetValue("CurrentFloor", out var cf) &&
                int.TryParse(cf.Value, out int floor))
                p.CurrentFloor = floor;

            return p;
        }

        public static PlayerData SeedPlayerData(string playFabId, string displayName, string steamId = null)
        {
            PlayerData p = new()
            {
                SteamID = steamId,
                PlayFabId   = playFabId,
                DisplayName = displayName,
                CurrentFloor = 0
            };
            return p;
        }
    }
}
