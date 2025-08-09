using System;
using System.Collections.Generic;

namespace Dungonator.Server.Layers
{
    public static class FloorLookup
    {
        public static class Maze
        {
            public const string Floor1  = "Floor-1";
            public const string Floor2  = "Floor-2";
            public const string Floor3  = "Floor-3";
            public const string Floor4  = "Floor-4";
            public const string Floor5  = "Floor-5";
            public const string Floor6  = "Floor-6";
            public const string Floor7  = "Floor-7";
            public const string Floor8  = "Floor-8";
            public const string Floor9  = "Floor-9";
            public const string Floor10 = "Floor-10";
        }
        // Forward map: scene name → floor number
        private static readonly Dictionary<string,int> FloorToNumber =
            new Dictionary<string,int>(capacity:10, comparer:StringComparer.Ordinal)
            {
                [FloorLookup.Maze.Floor1]  = 1,
                [FloorLookup.Maze.Floor2]  = 2,
                [FloorLookup.Maze.Floor3]  = 3,
                [FloorLookup.Maze.Floor4]  = 4,
                [FloorLookup.Maze.Floor5]  = 5,
                [FloorLookup.Maze.Floor6]  = 6,
                [FloorLookup.Maze.Floor7]  = 7,
                [FloorLookup.Maze.Floor8]  = 8,
                [FloorLookup.Maze.Floor9]  = 9,
                [FloorLookup.Maze.Floor10] = 10
            };

        // Reverse map: floor number → scene name
        private static readonly Dictionary<int,string> NumberToFloor =
            new Dictionary<int,string>(capacity:10)
            {
                [1]  = FloorLookup.Maze.Floor1,
                [2]  = FloorLookup.Maze.Floor2,
                [3]  = FloorLookup.Maze.Floor3,
                [4]  = FloorLookup.Maze.Floor4,
                [5]  = FloorLookup.Maze.Floor5,
                [6]  = FloorLookup.Maze.Floor6,
                [7]  = FloorLookup.Maze.Floor7,
                [8]  = FloorLookup.Maze.Floor8,
                [9]  = FloorLookup.Maze.Floor9,
                [10] = FloorLookup.Maze.Floor10
            };
        
        // thresholds: floors 0 < x < 10 -> layer 1...
        private static readonly int[] Breaks = { 0, 10, 20, 30 };
        private static readonly int[] Layers = { 0, 1, 2, 3 };

        public static int GetLayerNumber(int floorNumber)
        {
            var idx = Array.BinarySearch(Breaks, floorNumber);
            if (idx < 0)
                idx = ~idx;
            idx = Math.Min(idx, Layers.Length - 1); //clamp idx to last layer
            return Layers[idx];
        }
        
        /// <summary>
        /// Tries to get the floor number (1–10) for a given scene name.
        /// </summary>
        public static bool TryGetFloorNumber(string sceneName, out int floorNumber)
            => FloorToNumber.TryGetValue(sceneName, out floorNumber);

        /// <summary>
        /// Tries to get the scene name ("Floor-X") for a given floor number.
        /// </summary>
        public static bool TryGetSceneName(int floorNumber, out string sceneName)
            => NumberToFloor.TryGetValue(floorNumber, out sceneName);

    }
}