using System;

namespace Dungonator.Server.Layers.Maze.Scripts.Enums
{
    [Flags]
    public enum CellWalls
    {
        None  = 0,        // 0000
        North = 1 << 0,   // 0001
        East  = 1 << 1,   // 0010
        South = 1 << 2,   // 0100
        West  = 1 << 3    // 1000
    }
    public enum CellTypes
    {
        Empty,
        Treasure,
        Boss
    }
}