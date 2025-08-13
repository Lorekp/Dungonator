using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dungonator.Client.Layers.Maze
{
    public static class ClientMazePhysics
    {
        [Flags]
        private enum CellWalls { None=0, North=1<<0, East=1<<1, South=1<<2, West=1<<3 }

        // Must match your ShapeToWalls map on server
        private static readonly Dictionary<Shared.Dungeon.Layers.Maze.CellShapes, CellWalls> ShapeToWalls = new()
        {
            {Shared.Dungeon.Layers.Maze.CellShapes.Box,        CellWalls.North|CellWalls.East|CellWalls.South|CellWalls.West},
            {Shared.Dungeon.Layers.Maze.CellShapes.DeadEnd,    CellWalls.East|CellWalls.South|CellWalls.West},
            {Shared.Dungeon.Layers.Maze.CellShapes.DeadEnd90,  CellWalls.North|CellWalls.South|CellWalls.West},
            {Shared.Dungeon.Layers.Maze.CellShapes.DeadEnd180, CellWalls.North|CellWalls.East|CellWalls.West},
            {Shared.Dungeon.Layers.Maze.CellShapes.DeadEnd270, CellWalls.North|CellWalls.East|CellWalls.South},
            {Shared.Dungeon.Layers.Maze.CellShapes.T,          CellWalls.North},
            {Shared.Dungeon.Layers.Maze.CellShapes.T90,        CellWalls.East},
            {Shared.Dungeon.Layers.Maze.CellShapes.T180,       CellWalls.South},
            {Shared.Dungeon.Layers.Maze.CellShapes.T270,       CellWalls.West},
            {Shared.Dungeon.Layers.Maze.CellShapes.VerticalI,  CellWalls.East|CellWalls.West},
            {Shared.Dungeon.Layers.Maze.CellShapes.HorizontalI,CellWalls.North|CellWalls.South},
            {Shared.Dungeon.Layers.Maze.CellShapes.L,          CellWalls.South|CellWalls.West},
            {Shared.Dungeon.Layers.Maze.CellShapes.L90,        CellWalls.North|CellWalls.West},
            {Shared.Dungeon.Layers.Maze.CellShapes.L180,       CellWalls.North|CellWalls.East},
            {Shared.Dungeon.Layers.Maze.CellShapes.L270,       CellWalls.South|CellWalls.East},
            {Shared.Dungeon.Layers.Maze.CellShapes.X,          CellWalls.None},
        };

        /// Rebuilds colliders for the current streamed window (min/max of loaded cells, expanded by 1).
        /// Places colliders in a CompositeCollider2D under 'parent'. Clears previous stripes each call.
        public static void RebuildForWindow(
            Shared.Dungeon.MazeFloorData.CellData[,] cells,
            float cellSize,
            float wallThickness,
            Transform parent,
            IReadOnlyCollection<Vector2Int> loadedCells)
        {
            if (cells == null || loadedCells == null || loadedCells.Count == 0) return;

            // Ensure a clean parent
            var root = GetOrCreateChild(parent, "MazePhysics_Client");
            // wipe old stripes
            for (int i = root.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(root.GetChild(i).gameObject);

            // Composite setup on the root
            var rb = root.gameObject.GetComponent<Rigidbody2D>() ?? root.gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static; rb.simulated = true;

            var cc = root.gameObject.GetComponent<CompositeCollider2D>() ?? root.gameObject.AddComponent<CompositeCollider2D>();
            cc.geometryType   = CompositeCollider2D.GeometryType.Polygons;
            cc.generationType = CompositeCollider2D.GenerationType.Synchronous;

            int rows = cells.GetLength(0); // row 0 = TOP
            int cols = cells.GetLength(1);

            // Compute a tight window around loaded cells, then expand by 1 to cover shared borders
            int minCol = int.MaxValue, maxCol = int.MinValue;
            int minRow = int.MaxValue, maxRow = int.MinValue;
            foreach (var rc in loadedCells)
            {
                minCol = Math.Min(minCol, rc.x);
                maxCol = Math.Max(maxCol, rc.x);
                minRow = Math.Min(minRow, rc.y);
                maxRow = Math.Max(maxRow, rc.y);
            }
            minCol = Math.Max(0, minCol - 1);
            maxCol = Math.Min(cols - 1, maxCol + 1);
            minRow = Math.Max(0, minRow - 1);
            maxRow = Math.Min(rows - 1, maxRow + 1);

            // We scan boundaries within [minRow..maxRow]x[minCol..maxCol]
            // IMPORTANT: Y is flipped (row 0 = TOP). Tilemap places bottom-left of [row,col] at y=(rows-1-row)*cellSize.
            // Boundaries:
            //  - Vertical boundary at column 'c' is x = c * cellSize
            //  - Horizontal boundary at row 'r' is y = (rows - r) * cellSize

            // VERTICAL boundaries: c in [minCol..maxCol+1], rows in [minRow..maxRow]
            for (int c = minCol; c <= maxCol + 1; c++)
            {
                int r = minRow;
                while (r <= maxRow)
                {
                    int code = GetVerticalWallCode(cells, rows, cols, c, r);
                    if (code == 0) { r++; continue; }

                    int startR = r;
                    while (r <= maxRow && GetVerticalWallCode(cells, rows, cols, c, r) == code) r++;

                    float boundaryX = c * cellSize;
                    // flip Y span
                    float yStart = (rows - startR) * cellSize;
                    float yEnd   = (rows - r)      * cellSize;

                    float leftExtent  = (code & 1) != 0 ? wallThickness : 0f; // into left cell (−X)
                    float rightExtent = (code & 2) != 0 ? wallThickness : 0f; // into right cell (+X)
                    float width   = leftExtent + rightExtent;
                    float centerX = boundaryX + (rightExtent - leftExtent) * 0.5f;

                    CreateStripe(root,
                        center: new Vector2(centerX, 0.5f * (yStart + yEnd)),
                        size:   new Vector2(width,    Mathf.Abs(yStart - yEnd)));
                }
            }

            // HORIZONTAL boundaries: r in [minRow..maxRow+1], cols in [minCol..maxCol]
            for (int r = minRow; r <= maxRow + 1; r++)
            {
                int c = minCol;
                while (c <= maxCol)
                {
                    int code = GetHorizontalWallCode(cells, rows, cols, c, r);
                    if (code == 0) { c++; continue; }

                    int startC = c;
                    while (c <= maxCol && GetHorizontalWallCode(cells, rows, cols, c, r) == code) c++;

                    float boundaryY = (rows - r) * cellSize;   // flipped
                    float xStart = startC * cellSize;
                    float xEnd   = c      * cellSize;

                    float bottomExtent = (code & 1) != 0 ? wallThickness : 0f; // into bottom cell (−Y)
                    float topExtent    = (code & 2) != 0 ? wallThickness : 0f; // into top cell (+Y)
                    float height  = bottomExtent + topExtent;
                    float centerY = boundaryY + (topExtent - bottomExtent) * 0.5f;

                    CreateStripe(root,
                        center: new Vector2(0.5f * (xStart + xEnd), centerY),
                        size:   new Vector2(Mathf.Abs(xEnd - xStart), height));
                }
            }
        }

        private static void CreateStripe(Transform parent, Vector2 center, Vector2 size)
        {
            var go = new GameObject("Stripe", typeof(BoxCollider2D));
            go.transform.SetParent(parent, false);
            var bc = go.GetComponent<BoxCollider2D>();
            bc.compositeOperation = Collider2D.CompositeOperation.Merge;
            bc.offset = center;
            bc.size   = size;
        }

        private static int GetVerticalWallCode(Shared.Dungeon.MazeFloorData.CellData[,] cells, int rows, int cols, int col, int row)
        {
            bool fromLeft  = col > 0    && row >= 0 && row < rows && HasWall(cells[row, col - 1].CellShape, CellWalls.East);
            bool fromRight = col < cols && row >= 0 && row < rows && HasWall(cells[row, col].CellShape,     CellWalls.West);
            return (fromLeft ? 1 : 0) | (fromRight ? 2 : 0);
        }

        private static int GetHorizontalWallCode(Shared.Dungeon.MazeFloorData.CellData[,] cells, int rows, int cols, int col, int row)
        {
            bool fromBottom = row > 0    && col >= 0 && col < cols && HasWall(cells[row - 1, col].CellShape, CellWalls.North);
            bool fromTop    = row < rows && col >= 0 && col < cols && HasWall(cells[row, col].CellShape,     CellWalls.South);
            return (fromBottom ? 1 : 0) | (fromTop ? 2 : 0);
        }

        private static bool HasWall(Shared.Dungeon.Layers.Maze.CellShapes shape, CellWalls wall)
            => (ShapeToWalls[shape] & wall) != 0;

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t) return t;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }
    }
}
