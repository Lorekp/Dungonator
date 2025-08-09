using System;
using Dungonator.Shared.Dungeon.Layers.Maze;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Tilemaps;

namespace Dungonator.Client.Layers.Maze.MazeCellTemplates
{
    [CreateAssetMenu(menuName = "Maze/MazeCellTemplate")]
    public class MazeCellTemplate : ScriptableObject
    {
        public int cellSizeXY = 10;
        
        public CellShapes shape;

        // 2D array to hold tile references
        public TileBase[] flatLayout;

        private MazeCellTemplate()
        {
            flatLayout = new TileBase[cellSizeXY * cellSizeXY];;
        }
        
        public TileBase[,] Get2DLayout()
        {
            TileBase[,] result = new TileBase[cellSizeXY, cellSizeXY];
            for (int x = 0; x < cellSizeXY; x++)
                for (int y = 0; y < cellSizeXY; y++)
                    result[x, y] = flatLayout[y * cellSizeXY + x]; // row-major order
            return result;
        }
        
        public void SetTile(int x, int y, TileBase tile)
        {
            flatLayout[y * cellSizeXY + x] = tile;
        }

        public TileBase GetTile(int x, int y)
        {
            return flatLayout[y * cellSizeXY + x];
        }

        public int GetCellSize()
        {
            return cellSizeXY;
        }
    }
}