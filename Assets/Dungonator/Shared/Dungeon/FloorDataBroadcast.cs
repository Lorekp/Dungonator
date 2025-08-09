using FishNet.Broadcast;

namespace Dungonator.Shared.Dungeon
{
    public struct FloorDataBroadcast : IBroadcast
    {
        public string SceneName;
        public IFloorData Data;
    }
}