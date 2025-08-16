using System;
using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using Dungonator.Shared.Dungeon;
using Dungonator.Shared.Dungeon.Layers.Maze;
using Dungonator.Shared.Helpers; // GetOrAddComponent

namespace Dungonator.Server.Layers.Maze.Scripts.MazeGeneration
{
    public static class MazePhysicsSetup
    {
        public struct BuildOptions
        {
            public int perFrame;          // create this many colliders per frame before yielding
            public int generateEvery;     // call GenerateGeometry() after this many new colliders
        }

        public sealed class Handle
        {
            public Transform Root;        // the GameObject that holds RB2D + Composite + Box sources
            public bool IsBuilding;
            public float Progress01;      // 0..1 based on stripes processed
            public event Action OnCompleted;
            internal void FireDone() => OnCompleted?.Invoke();
        }

        /// <summary>
        /// Starts an async full-maze collider build under 'parent'.
        /// Returns a handle with progress + completion event.
        /// </summary>
        public static Handle BuildServerColliders(MazeFloorData data, Transform parent, BuildOptions? options = null)
        {
            var opt = options ?? new BuildOptions { perFrame = 400, generateEvery = 3000 };

            // fresh host you can destroy when rebuilding
            var host = new GameObject("MazeColliders");
            host.transform.SetParent(parent, false);

            var handle = new Handle { Root = host.transform, IsBuilding = true, Progress01 = 0f };

            var runner = host.GetOrAddComponent<Runner>();
            runner.Begin(data, opt, handle);

            return handle;
        }

        // ---------- internal ----------

        private sealed class Runner : MonoBehaviour
        {
            private MazeFloorData _data;
            private BuildOptions   _opt;
            private Handle         _handle;

            private Rigidbody2D         _rb;
            private CompositeCollider2D _cc;

            private int _rows, _cols;
            private float _size, _thick;

            private byte[,] _vcode; // [cols+1, rows]
            private byte[,] _hcode; // [rows+1, cols]

            private int _addedSinceGen;

            public void Begin(MazeFloorData data, BuildOptions opt, Handle handle)
            {
                _data   = data;
                _opt    = opt;
                _handle = handle;

                var go = gameObject;

                _rb = go.GetOrAddComponent<Rigidbody2D>();
                _rb.bodyType  = RigidbodyType2D.Static;
                _rb.simulated = false; // pause physics during build

                _cc = go.GetOrAddComponent<CompositeCollider2D>();
                _cc.geometryType   = CompositeCollider2D.GeometryType.Polygons;
                _cc.generationType = CompositeCollider2D.GenerationType.Manual;
                _cc.enabled        = false;

                var cells = _data.Cells;
                _rows  = cells.GetLength(0);
                _cols  = cells.GetLength(1);
                _size  = _data.CellSize;
                _thick = _data.WallThickness;

                // precompute boundary codes once
                _vcode = new byte[_cols + 1, _rows];
                _hcode = new byte[_rows + 1, _cols];

                for (int c = 0; c <= _cols; c++)
                    for (int r = 0; r < _rows; r++)
                        _vcode[c, r] = (byte)GetVerticalWallCodeFast(cells, _rows, _cols, c, r);

                for (int r = 0; r <= _rows; r++)
                    for (int c = 0; c < _cols; c++)
                        _hcode[r, c] = (byte)GetHorizontalWallCodeFast(cells, _rows, _cols, c, r);

                StopAllCoroutines();
                StartCoroutine(BuildAllAsync());
            }

            private IEnumerator BuildAllAsync()
            {
                // Prepare
                var cells = _data.Cells;
                var wallThickness = _data.WallThickness;
                int rows = cells.GetLength(0); // row 0 = TOP (same as client)
                int cols = cells.GetLength(1);

                float cellSize = _size;
                
                //knobs
                var ewWidth = wallThickness * 0.79f; // shared width for EAST/WEST colliders
                var nHeight = wallThickness * 0.71f; // height for NORTH colliders
                var sHeight = wallThickness * 0.755f; // height for SOUTH colliders

                _cc.enabled = false;
                _cc.geometryType = CompositeCollider2D.GeometryType.Polygons;
                _cc.generationType = CompositeCollider2D.GenerationType.Manual;

                int madeThisFrame = 0;
                int total = rows * cols;
                int done = 0;

                // Build EXACTLY like ClientMazePhysics.RebuildForWindow
                for (int row = 0; row < rows; row++)
                {
                    for (int col = 0; col < cols; col++)
                    {
                        var walls = Dungonator.Shared.Dungeon.Layers.Maze.MazeWallLookup.Get(cells[row, col].CellShape);
                        if (walls == Dungonator.Shared.Dungeon.Layers.Maze.CellWalls.None)
                        {
                            done++;
                            continue;
                        }

                        float x0 = col * cellSize;
                        float y0 = (rows - 1 - row) * cellSize; // flip: row 0 (top) maps to highest Y
                        float x1 = x0 + cellSize;
                        float y1 = y0 + cellSize;

                        var cornerNE = false;
                        var cornerNW = false;
                        var cornerSE = false;
                        var cornerSW = false;

                        if ((walls & (CellWalls.North | CellWalls.East)) == 0) cornerNE = true;
                        if ((walls & (CellWalls.North | CellWalls.West)) == 0) cornerNW = true;
                        if ((walls & (CellWalls.South | CellWalls.East)) == 0) cornerSE = true;
                        if ((walls & (CellWalls.South | CellWalls.West)) == 0) cornerSW = true;
                        
                        var innerW = Mathf.Max(0f, x1 - x0);
                        var innerH = Mathf.Max(0f, y1 - y0);

                        var cyWE = y0 + innerH * 0.5f;
                        var cxNS = x0 + innerW * 0.5f;
                        // EAST/WEST (same width), NORTH/SOUTH (same height)
                        if ((walls & CellWalls.West) != 0)
                        {
                            CreateBox(x0 + ewWidth * 0.5f, cyWE, ewWidth, innerH);
                        }

                        if ((walls & CellWalls.East) != 0)
                        {
                            CreateBox(x1 - ewWidth * 0.5f, cyWE, ewWidth, innerH);
                        }

                        if ((walls & CellWalls.South) != 0)
                        {
                            CreateBox(cxNS, y0 + sHeight * 0.5f, innerW, sHeight);
                        }

                        if ((walls & CellWalls.North) != 0)
                        {
                            CreateBox(cxNS, y1 - nHeight * 0.5f, innerW, nHeight);
                        }

                        // corners use width of E/W and height of N/S
                        if (cornerNE)
                        {
                            CreateBox(x1 - ewWidth * 0.5f, y1 - nHeight * 0.5f, ewWidth, nHeight);
                        }

                        if (cornerNW)
                        {
                            CreateBox(x0 + ewWidth * 0.5f, y1 - nHeight * 0.5f, ewWidth, nHeight);
                        }

                        if (cornerSE)
                        {
                            CreateBox(x1 - ewWidth * 0.5f, y0 + sHeight * 0.5f, ewWidth, sHeight);
                        }

                        if (cornerSW)
                        {
                            CreateBox(x0 + ewWidth * 0.5f, y0 + sHeight * 0.5f, ewWidth, sHeight);
                        }
                        // Yield tuning (keep your _opt if you like)
                        if (++madeThisFrame >= _opt.perFrame)
                        {
                            madeThisFrame = 0;
                            _handle.Progress01 = Mathf.Clamp01(++done / (float)total);
                            yield return null;
                        }
                        else
                        {
                            done++;
                        }
                    }
                }

                // One composite build, then enable sim for authority
                _cc.enabled = true;
                _cc.GenerateGeometry();
                _rb.simulated = true;

                _handle.Progress01 = 1f;
                _handle.IsBuilding = false;
                _handle.FireDone();
            }

            private void CreateBox(float cx, float cy, float w, float h)
            {
                var bc = gameObject.AddComponent<BoxCollider2D>();
                bc.compositeOperation = Collider2D.CompositeOperation.Merge;
                bc.isTrigger          = false;
                bc.offset             = new Vector2(cx, cy);   // local (root at (0,0,0))
                bc.size               = new Vector2(w,  h);
            }
        }

        // --- fast helpers ---

        private static int GetVerticalWallCodeFast(MazeFloorData.CellData[,] cells, int rows, int cols, int col, int row)
        {
            bool left  = col > 0    && ((MazeWallLookup.ShapeToWalls[(int)cells[row, col - 1].CellShape] & CellWalls.East)  != 0);
            bool right = col < cols && ((MazeWallLookup.ShapeToWalls[(int)cells[row, col    ].CellShape] & CellWalls.West)  != 0);
            return (left ? 1 : 0) | (right ? 2 : 0);
        }

        private static int GetHorizontalWallCodeFast(MazeFloorData.CellData[,] cells, int rows, int cols, int col, int row)
        {
            bool bottom = row > 0    && ((MazeWallLookup.ShapeToWalls[(int)cells[row - 1, col].CellShape] & CellWalls.North) != 0);
            bool top    = row < rows && ((MazeWallLookup.ShapeToWalls[(int)cells[row,     col].CellShape] & CellWalls.South) != 0);
            return (bottom ? 1 : 0) | (top ? 2 : 0);
        }
    }
}
