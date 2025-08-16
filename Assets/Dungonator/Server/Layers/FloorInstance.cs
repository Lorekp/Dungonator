using System;
using System.Collections.Generic;
using Dungonator.Shared.Dungeon;
using FishNet.Connection;

namespace Dungonator.Server.Layers
{
    public struct FloorInstance : IEquatable<FloorInstance>
    {
        public int Handle;
        public string SceneName;
        public HashSet<NetworkConnection> Connections; //bool specifies if player has been spawned
        public HashSet<NetworkConnection> PendingConnections;
        public bool HasScene;
        public IFloorData Data;

        public bool PhysicsReady;

        public bool Equals(FloorInstance other)
        {
            return Handle == other.Handle;
        }

        public override bool Equals(object obj)
        {
            return obj is FloorInstance other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Handle);
        }
    }
}