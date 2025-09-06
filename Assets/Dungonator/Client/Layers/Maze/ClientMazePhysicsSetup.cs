using System;
using System.Collections.Generic;
using Dungonator.Shared.Dungeon.Layers.Maze;
using UnityEngine;

namespace Dungonator.Client.Layers.Maze
{
    /// <summary>
    /// Pooled client-side wall colliders that match MazeDrawer's tile placement exactly.
    /// Builds stripes per-cell, fully inside the cell, then merges via a CompositeCollider2D (Polygons).
    /// Parent this under a world-fixed transform (e.g., your Grid), NOT the player.
    /// </summary>
    public static class ClientMazePhysics
    {
        /// <summary>
        /// Rebuild pooled colliders for the streamed window (loadedCells).
        /// 'parent' must be a static/world transform (e.g., Grid.transform).
        /// </summary>
        public static void RebuildForWindow(
            Shared.Dungeon.MazeFloorData.CellData[,] cells,
            float cellSize,
            float wallThickness,
            Transform parent,
            IReadOnlyCollection<Vector2Int> loadedCells)
        {
            if (cells == null || loadedCells == null || loadedCells.Count == 0) return;
            if (cellSize <= 0f || wallThickness <= 0f) return;

            var rows = cells.GetLength(0); // [row, col] with row 0 = TOP
            var cols = cells.GetLength(1);

            // Static root to host Composite + pooled BoxCollider2D stripes
            var root = GetOrCreateChild(parent, "MazePhysics_Client");
            root.position   = Vector3.zero;
            root.rotation   = Quaternion.identity;
            root.localScale = Vector3.one;
            
            // assign physics layer for raycasts
            var wallLayer = LayerMask.NameToLayer("MazeWalls");
            if (wallLayer == -1)
                Debug.LogWarning("Create a Layer named 'MazeWalls' in Project Settings > Tags and Layers.");
            else
                root.gameObject.layer = wallLayer;

            var host = root.GetComponent<PoolHost>() ?? root.gameObject.AddComponent<PoolHost>();
            host.InitOnce();

            // Pause physics while we edit colliders in place
            host.RB.bodyType  = RigidbodyType2D.Static;
            host.RB.simulated = false;
            host.Composite.enabled        = false;
            host.Composite.geometryType   = CompositeCollider2D.GeometryType.Polygons;
            host.Composite.generationType = CompositeCollider2D.GenerationType.Manual;

            // Tight AABB around loaded cells (not required, but keeps counts bounded)
            int minC=int.MaxValue, maxC=int.MinValue, minR=int.MaxValue, maxR=int.MinValue;
            foreach (var p in loadedCells)
            {
                if (p.x < minC) minC = p.x;
                if (p.x > maxC) maxC = p.x;
                if (p.y < minR) minR = p.y;
                if (p.y > maxR) maxR = p.y;
            }
            minC = Mathf.Clamp(minC, 0, cols-1);
            maxC = Mathf.Clamp(maxC, 0, cols-1);
            minR = Mathf.Clamp(minR, 0, rows-1);
            maxR = Mathf.Clamp(maxR, 0, rows-1);

            var used = 0;

            // Build per-cell stripes fully inside the cell, matching your LoadCells mapping.
            foreach (var cellRC in loadedCells)
            {
                var col = cellRC.x; // x
                var row = cellRC.y; // y
                if (col < minC || col > maxC || row < minR || row > maxR) continue;
                if (row < 0 || row >= rows || col < 0 || col >= cols) continue;

                var walls = MazeWallLookup.Get(cells[row, col].CellShape);
                
                var x0 = col * cellSize;
                var y0 = (rows - 1 - row) * cellSize; // flip: row 0 (top) maps to highest Y
                var x1 = x0 + cellSize;
                var y1 = y0 + cellSize;

                var cornerNE = false;
                var cornerNW = false;
                var cornerSE = false;
                var cornerSW = false;

                if ((walls & (CellWalls.North | CellWalls.East)) == 0)  cornerNE = true;
                if ((walls & (CellWalls.North | CellWalls.West)) == 0)  cornerNW = true;
                if ((walls & (CellWalls.South | CellWalls.East)) == 0)  cornerSE = true;
                if ((walls & (CellWalls.South | CellWalls.West)) == 0)  cornerSW = true;
                
                // --- knobs ---
                var ewWidth  = wallThickness * 0.79f;  // shared width for EAST/WEST colliders
                var nHeight = wallThickness * 0.71f;  // height for NORTH colliders
                var sHeight = wallThickness * 0.755f; // height for SOUTH colliders

                var innerW = Mathf.Max(0f, x1 - x0);
                var innerH = Mathf.Max(0f, y1 - y0);

                var cyWE = y0 + innerH * 0.5f;
                var cxNS = x0 + innerW * 0.5f;
                // EAST/WEST (same width), NORTH/SOUTH (same height)
                if ((walls & CellWalls.West) != 0)
                {
                    var bc = host.GetStripe(used++);
                    SetStripe(bc, x0 + ewWidth * 0.5f, cyWE, ewWidth, innerH);
                }

                if ((walls & CellWalls.East) != 0)
                {
                    var bc = host.GetStripe(used++);
                    SetStripe(bc, x1 - ewWidth * 0.5f, cyWE, ewWidth, innerH);
                }

                if ((walls & CellWalls.South) != 0)
                {
                    var bc = host.GetStripe(used++);
                    SetStripe(bc, cxNS, y0 + sHeight * 0.5f, innerW, sHeight);
                }

                if ((walls & CellWalls.North) != 0)
                {
                    var bc = host.GetStripe(used++);
                    SetStripe(bc, cxNS, y1 - nHeight * 0.5f, innerW, nHeight);
                }
                
                // corners use width of E/W and height of N/S
                if (cornerNE)
                {
                    var bc = host.GetStripe(used++);
                    SetStripe(bc, x1 - ewWidth * 0.5f, y1 - nHeight * 0.5f, ewWidth, nHeight);
                }

                if (cornerNW)
                {
                    var bc = host.GetStripe(used++);
                    SetStripe(bc, x0 + ewWidth * 0.5f, y1 - nHeight * 0.5f, ewWidth, nHeight);
                }

                if (cornerSE)
                {
                    var bc = host.GetStripe(used++);
                    SetStripe(bc, x1 - ewWidth * 0.5f, y0 + sHeight * 0.5f, ewWidth, sHeight);
                }

                if (cornerSW)
                {
                    var bc = host.GetStripe(used++);
                    SetStripe(bc, x0 + ewWidth * 0.5f, y0 + sHeight * 0.5f, ewWidth, sHeight);
                }
            }

            // Disable any extra pooled stripes
            host.DisableFrom(used);

            // One composite remesh, then resume physics
            host.Composite.enabled = true;
            host.Composite.GenerateGeometry();
            host.RB.simulated = true;
        }

        // -------- helpers --------

        private static void SetStripe(BoxCollider2D bc, float cx, float cy, float w, float h)
        {
            bc.isTrigger          = false;
            bc.compositeOperation = Collider2D.CompositeOperation.Merge;
            bc.offset             = new Vector2(cx, cy);
            bc.size               = new Vector2(w,  h);
            if (!bc.enabled) bc.enabled = true;
        }

        private static Transform GetOrCreateChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t) return t;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        // -------- pooled host (colliders live on same GO as Composite for best merging) --------

        private sealed class PoolHost : MonoBehaviour
        {
            public Rigidbody2D         RB        { get; private set; }
            public CompositeCollider2D Composite { get; private set; }

            private readonly List<BoxCollider2D> _pool = new(1024);

            public void InitOnce()
            {
                RB        = GetComponent<Rigidbody2D>()         ?? gameObject.AddComponent<Rigidbody2D>();
                Composite = GetComponent<CompositeCollider2D>() ?? gameObject.AddComponent<CompositeCollider2D>();

                RB.bodyType  = RigidbodyType2D.Static;
                RB.simulated = true;

                Composite.geometryType   = CompositeCollider2D.GeometryType.Polygons;
                Composite.generationType = CompositeCollider2D.GenerationType.Manual;
            }

            public BoxCollider2D GetStripe(int index)
            {
                while (_pool.Count <= index)
                {
                    var bc = gameObject.AddComponent<BoxCollider2D>();
                    bc.enabled = false;
                    _pool.Add(bc);
                }
                var stripe = _pool[index];
                if (!stripe.enabled) stripe.enabled = true;
                return stripe;
            }

            public void DisableFrom(int start)
            {
                for (var i = start; i < _pool.Count; i++)
                {
                    var bc = _pool[i];
                    if (bc.enabled) bc.enabled = false;
                }
            }
        }
    }
}
