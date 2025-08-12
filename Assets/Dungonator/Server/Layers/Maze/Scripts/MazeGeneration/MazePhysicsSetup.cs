using System;
using System.Collections.Generic;
using Dungonator.Shared.Dungeon;
using Dungonator.Shared.Dungeon.Layers.Maze;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Dungonator.Server.Layers.Maze.Scripts.MazeGeneration
{
    public static class MazePhysicsSetup
    {
        [Flags]
        private enum CellWalls
        {
            None  = 0,
            North = 1 << 0,
            East  = 1 << 1,
            South = 1 << 2,
            West  = 1 << 3
        }

        private static readonly Dictionary<CellShapes, CellWalls> ShapeToWalls = new()
        {
            {CellShapes.Box, CellWalls.North | CellWalls.East | CellWalls.South | CellWalls.West},
            {CellShapes.DeadEnd, CellWalls.East | CellWalls.South | CellWalls.West},
            {CellShapes.DeadEnd90, CellWalls.North | CellWalls.South | CellWalls.West},
            {CellShapes.DeadEnd180, CellWalls.North | CellWalls.East | CellWalls.West},
            {CellShapes.DeadEnd270, CellWalls.North | CellWalls.East | CellWalls.South},
            {CellShapes.T, CellWalls.North},
            {CellShapes.T90, CellWalls.East},
            {CellShapes.T180, CellWalls.South},
            {CellShapes.T270, CellWalls.West},
            {CellShapes.VerticalI, CellWalls.East | CellWalls.West},
            {CellShapes.HorizontalI, CellWalls.North | CellWalls.South},
            {CellShapes.L, CellWalls.South | CellWalls.West},
            {CellShapes.L90, CellWalls.North | CellWalls.West},
            {CellShapes.L180, CellWalls.North | CellWalls.East},
            {CellShapes.L270, CellWalls.South | CellWalls.East},
            {CellShapes.X, CellWalls.None}
        };

        private const float WallThickness = 0.1f;

        public static void BuildServerColliders(MazeFloorData data, Transform parent)
        {
            if (data.Cells == null) return;
            var cells = data.Cells;
            int rows = cells.GetLength(0);
            int cols = cells.GetLength(1);
            float size = data.CellSize;

            // vertical boundaries
            for (int col = 0; col <= cols; col++)
            {
                int row = 0;
                while (row < rows)
                {
                    if (!HasVerticalWall(cells, rows, cols, col, row))
                    {
                        row++;
                        continue;
                    }

                    int startRow = row;
                    while (row < rows && HasVerticalWall(cells, rows, cols, col, row))
                        row++;

                    float x = col * size;
                    float yStart = (rows - startRow) * size;
                    float yEnd = (rows - row) * size;
                    CreateCollider(parent, new Vector2(x, (yStart + yEnd) * 0.5f), new Vector2(WallThickness, Mathf.Abs(yStart - yEnd)));
                }
            }

            // horizontal boundaries
            for (int row = 0; row <= rows; row++)
            {
                int col = 0;
                while (col < cols)
                {
                    if (!HasHorizontalWall(cells, rows, cols, col, row))
                    {
                        col++;
                        continue;
                    }

                    int startCol = col;
                    while (col < cols && HasHorizontalWall(cells, rows, cols, col, row))
                        col++;

                    float y = (rows - row) * size;
                    float xStart = startCol * size;
                    float xEnd = col * size;
                    CreateCollider(parent, new Vector2((xStart + xEnd) * 0.5f, y), new Vector2(Mathf.Abs(xEnd - xStart), WallThickness));
                }
            }
        }

        private static void CreateCollider(Transform parent, Vector2 center, Vector2 size)
        {
            var go = new GameObject("MazeWallCollider");
            go.transform.SetParent(parent, false);
            var bc = go.AddComponent<BoxCollider2D>();
            bc.offset = center;
            bc.size = size;
        }

        private static bool HasVerticalWall(MazeFloorData.CellData[,] cells, int rows, int cols, int col, int row)
        {
            if (col == 0)
                return HasWall(cells[row, 0].CellShape, CellWalls.West);
            if (col == cols)
                return HasWall(cells[row, cols - 1].CellShape, CellWalls.East);
            return HasWall(cells[row, col - 1].CellShape, CellWalls.East) ||
                   HasWall(cells[row, col].CellShape, CellWalls.West);
        }

        private static bool HasHorizontalWall(MazeFloorData.CellData[,] cells, int rows, int cols, int col, int row)
        {
            if (row == 0)
                return HasWall(cells[0, col].CellShape, CellWalls.North);
            if (row == rows)
                return HasWall(cells[rows - 1, col].CellShape, CellWalls.South);
            return HasWall(cells[row - 1, col].CellShape, CellWalls.South) ||
                   HasWall(cells[row, col].CellShape, CellWalls.North);
        }

        private static bool HasWall(CellShapes shape, CellWalls wall)
        {
            return (ShapeToWalls[shape] & wall) != 0;
        }
    }
}