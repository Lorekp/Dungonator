using System;
using System.Collections.Generic;
using Dungonator.Shared.Dungeon;
using Dungonator.Shared.Dungeon.Layers.Maze;
using Dungonator.Shared.Helpers;
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

        public static void BuildServerColliders(MazeFloorData data, Transform parent)
        {
            if (data.Cells == null)
                return;

            var rb = parent.gameObject.GetOrAddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            rb.simulated = true;

            var cc = parent.gameObject.GetOrAddComponent<CompositeCollider2D>();
            cc.geometryType   = CompositeCollider2D.GeometryType.Polygons;
            cc.generationType = CompositeCollider2D.GenerationType.Synchronous;

            var cells     = data.Cells;
            int rows      = cells.GetLength(0);
            int cols      = cells.GetLength(1);
            float size    = data.CellSize;
            float thickness = data.WallThickness;

            // vertical boundaries
            for (int col = 0; col <= cols; col++)
            {
                int row = 0;
                while (row < rows)
                {
                    int code = GetVerticalWallCode(cells, rows, cols, col, row);
                    if (code == 0)
                    {
                        row++;
                        continue;
                    }

                    int startRow = row;
                    while (row < rows && GetVerticalWallCode(cells, rows, cols, col, row) == code)
                        row++;

                    float boundaryX = col * size;
                    float yStart = startRow * size;
                    float yEnd   = row * size;

                    // VERTICAL stripe at x = boundaryX, spanning yStart..yEnd
                    float leftExtent  = (code & 1) != 0 ? thickness : 0f; // into left cell (−X)
                    float rightExtent = (code & 2) != 0 ? thickness : 0f; // into right cell (+X)
                    float width   = leftExtent + rightExtent;             // t or 2t
                    float centerX = boundaryX + (rightExtent - leftExtent) * 0.5f;
                    CreateCollider(parent,
                        new Vector2(centerX, (yStart + yEnd) * 0.5f),
                        new Vector2(width, Mathf.Abs(yStart - yEnd)));
                }
            }

            // horizontal boundaries
            for (int row = 0; row <= rows; row++)
            {
                int col = 0;
                while (col < cols)
                {
                    int code = GetHorizontalWallCode(cells, rows, cols, col, row);
                    if (code == 0)
                    {
                        col++;
                        continue;
                    }

                    int startCol = col;
                    while (col < cols && GetHorizontalWallCode(cells, rows, cols, col, row) == code)
                        col++;

                    float boundaryY = row * size;
                    float xStart = startCol * size;
                    float xEnd   = col * size;

                    
                    // HORIZONTAL stripe at y = boundaryY, spanning xStart..xEnd
                    float bottomExtent = (code & 1) != 0 ? thickness : 0f; // into bottom cell (−Y)
                    float topExtent    = (code & 2) != 0 ? thickness : 0f; // into top cell (+Y)
                    float height  = bottomExtent + topExtent;
                    float centerY = boundaryY + (topExtent - bottomExtent) * 0.5f;
                    CreateCollider(parent,
                        new Vector2((xStart + xEnd) * 0.5f, centerY),
                        new Vector2(Mathf.Abs(xEnd - xStart), height));
                }
            }
        }

        private static void CreateCollider(Transform parent, Vector2 center, Vector2 size)
        {
            var bc = parent.gameObject.AddComponent<BoxCollider2D>();
            bc.compositeOperation = Collider2D.CompositeOperation.Merge;
            bc.offset = center;
            bc.size = size;
        }

        private static int GetVerticalWallCode(MazeFloorData.CellData[,] cells, int rows, int cols, int col, int row)
        {
            bool fromLeft  = col > 0    && HasWall(cells[row, col - 1].CellShape, CellWalls.East);
            bool fromRight = col < cols && HasWall(cells[row, col].CellShape,     CellWalls.West);
            // bit0 = left, bit1 = right
            return (fromLeft ? 1 : 0) | (fromRight ? 2 : 0);
        }

        private static int GetHorizontalWallCode(MazeFloorData.CellData[,] cells, int rows, int cols, int col, int row)
        {
            bool fromBottom = row > 0    && HasWall(cells[row - 1, col].CellShape, CellWalls.North);
            bool fromTop    = row < rows && HasWall(cells[row, col].CellShape,     CellWalls.South);
            // bit0 = bottom, bit1 = top
            return (fromBottom ? 1 : 0) | (fromTop ? 2 : 0);
        }

        private static bool HasWall(CellShapes shape, CellWalls wall)
        {
            return (ShapeToWalls[shape] & wall) != 0;
        }
    }
}