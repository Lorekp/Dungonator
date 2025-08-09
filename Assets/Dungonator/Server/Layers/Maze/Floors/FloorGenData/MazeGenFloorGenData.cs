using UnityEditor;
using UnityEngine;

namespace Dungonator.Server.Layers.Maze.Floors.FloorData
{
    [CreateAssetMenu(menuName="Maze/MazeFloorData")]
    public class MazeGenFloorGenData : Shared.Dungeon.FloorGenData
    {
        [Tooltip("How many players is this Maze supposed to accomodate?")]
        public int playerCount;
        [Tooltip("How many Cells(aka Chunks) per player?")]
        public int playerDensity;
        [Header("Spawns x instances of type per PlayerCount")]
        public float challengeDensity;
        public float bossDensity;
        public float enemyDensity;
        [Header("CellSize")]
        [Tooltip("How big is the cell? (measured in unity tilemap tiles)")]
        public int cellSize;
        [Header("Seed")]
        [Tooltip("Generate a new seed?")]
        public bool generateSeed;
        public int seed;
    }
}
