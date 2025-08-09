using System;
using Dungonator.Shared.Dungeon.Layers.Maze;
using FishNet.CodeGenerating;
using UnityEngine;

namespace Dungonator.Shared.Dungeon
{
    public class FloorGenData : ScriptableObject
    {
        public int layerIndex;           // e.g. 0 = Maze layer
        public int floorNumber;          // 1 through 10
    }
    
    public interface IFloorData
    {
        public int LayerIndex { get; set; }
        public int FloorNumber { get; set; }
        public int SpawnPointIndex { get; set; }
        public Vector2[] SpawnPoints { get; set; }
    }

    public struct MazeFloorData : IFloorData
    {
        public int LayerIndex { get; set; }
        public int FloorNumber { get; set; }
        public int SpawnPointIndex { get; set; }
        public int CellSize { get; set; } //size of a cell in unity tilemap tiles
        public Vector2[] SpawnPoints { get; set; }
        
        public CellData[,] Cells;

        public struct CellData : IEquatable<CellData>
        {
            public bool IsPlayerSpawn;
            public bool IsBossSpawn;
            public bool IsEliteSpawn;
            public bool IsChallengeSpawn;

            public CellShapes CellShape;

            public bool Equals(CellData other)
            {
                return IsPlayerSpawn == other.IsPlayerSpawn && IsBossSpawn == other.IsBossSpawn && IsEliteSpawn == other.IsEliteSpawn && IsChallengeSpawn == other.IsChallengeSpawn && CellShape == other.CellShape;
            }

            public override bool Equals(object obj)
            {
                return obj is CellData other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(IsPlayerSpawn, IsBossSpawn, IsEliteSpawn, IsChallengeSpawn, (int)CellShape);
            }
        }
    }

    public static class FloorDataHelper
    {
        public static bool TryGetFloorData<T>(IFloorData src, out T data)
            where T : struct, IFloorData
        {
            if (src is T t) { data = t; return true; }
            data = default;
            return false;
        }
    }
}