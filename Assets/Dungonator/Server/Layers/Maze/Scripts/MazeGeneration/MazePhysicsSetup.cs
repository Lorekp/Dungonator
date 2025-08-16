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
        [Flags]
        private enum CellWalls { None=0, North=1<<0, East=1<<1, South=1<<2, West=1<<3 }

        // index by (int)CellShapes for speed
        private static readonly CellWalls[] ShapeToWalls =
        {
            /* VerticalI   */ CellWalls.East | CellWalls.West,
            /* HorizontalI */ CellWalls.North | CellWalls.South,
            /* L           */ CellWalls.South | CellWalls.West,
            /* L90         */ CellWalls.North | CellWalls.West,
            /* L180        */ CellWalls.North | CellWalls.East,
            /* L270        */ CellWalls.South | CellWalls.East,
            /* X           */ CellWalls.None,
            /* T           */ CellWalls.North,
            /* T90         */ CellWalls.East,
            /* T180        */ CellWalls.South,
            /* T270        */ CellWalls.West,
            /* DeadEnd     */ CellWalls.East | CellWalls.South | CellWalls.West,
            /* DeadEnd90   */ CellWalls.North | CellWalls.South | CellWalls.West,
            /* DeadEnd180  */ CellWalls.North | CellWalls.East  | CellWalls.West,
            /* DeadEnd270  */ CellWalls.North | CellWalls.East  | CellWalls.South,
            /* Box         */ CellWalls.North | CellWalls.East  | CellWalls.South | CellWalls.West
        };

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
                int madeThisFrame = 0;

                // count stripes for progress
                int stripesTotal = (_cols + 1) * _rows + (_rows + 1) * _cols;
                int stripesDone  = 0;

                // vertical boundaries
                for (int c = 0; c <= _cols; c++)
                {
                    int r = 0;
                    while (r < _rows)
                    {
                        int code = _vcode[c, r];
                        if (code == 0) { r++; stripesDone++; continue; }

                        int start = r;
                        while (r < _rows && _vcode[c, r] == code) { r++; stripesDone++; }

                        float bx = c * _size;
                        float y0 = start * _size;
                        float y1 = r     * _size;

                        float left  = (code & 1) != 0 ? _thick : 0f;
                        float right = (code & 2) != 0 ? _thick : 0f;
                        float width = left + right;
                        if (width > 0f)
                        {
                            float cx = bx + 0.5f * (right - left);
                            float cy = 0.5f * (y0 + y1);
                            float h  = Mathf.Max(0f, y1 - y0);
                            if (h > 0f)
                            {
                                CreateBox(cx, cy, width, h);
                                if (++madeThisFrame >= _opt.perFrame)
                                {
                                    madeThisFrame = 0;
                                    _handle.Progress01 = Mathf.Clamp01(stripesDone / (float)stripesTotal);
                                    yield return null;
                                }
                            }
                        }
                    }
                }

                // horizontal boundaries
                for (int r = 0; r <= _rows; r++)
                {
                    int c = 0;
                    while (c < _cols)
                    {
                        int code = _hcode[r, c];
                        if (code == 0) { c++; stripesDone++; continue; }

                        int start = c;
                        while (c < _cols && _hcode[r, c] == code) { c++; stripesDone++; }

                        float by = r * _size;
                        float x0 = start * _size;
                        float x1 = c     * _size;

                        float bot = (code & 1) != 0 ? _thick : 0f;
                        float top = (code & 2) != 0 ? _thick : 0f;
                        float height = bot + top;
                        if (height > 0f)
                        {
                            float cy = by + 0.5f * (top - bot);
                            float cx = 0.5f * (x0 + x1);
                            float w  = Mathf.Max(0f, x1 - x0);
                            if (w > 0f)
                            {
                                CreateBox(cx, cy, w, height);
                                if (++madeThisFrame >= _opt.perFrame)
                                {
                                    madeThisFrame = 0;
                                    _handle.Progress01 = Mathf.Clamp01(stripesDone / (float)stripesTotal);
                                    yield return null;
                                }
                            }
                        }
                    }
                }

                // final composite build
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
                bc.offset             = new Vector2(cx, cy);
                bc.size               = new Vector2(w, h);

                // small periodic composite generate to avoid a big end spike
                _addedSinceGen++;
                if (_addedSinceGen >= _opt.generateEvery)
                {
                    _cc.enabled = true;
                    _cc.GenerateGeometry();
                    _addedSinceGen = 0;
                }
            }
        }

        // --- fast helpers ---

        private static int GetVerticalWallCodeFast(MazeFloorData.CellData[,] cells, int rows, int cols, int col, int row)
        {
            bool left  = col > 0    && ((ShapeToWalls[(int)cells[row, col - 1].CellShape] & CellWalls.East)  != 0);
            bool right = col < cols && ((ShapeToWalls[(int)cells[row, col    ].CellShape] & CellWalls.West)  != 0);
            return (left ? 1 : 0) | (right ? 2 : 0);
        }

        private static int GetHorizontalWallCodeFast(MazeFloorData.CellData[,] cells, int rows, int cols, int col, int row)
        {
            bool bottom = row > 0    && ((ShapeToWalls[(int)cells[row - 1, col].CellShape] & CellWalls.North) != 0);
            bool top    = row < rows && ((ShapeToWalls[(int)cells[row,     col].CellShape] & CellWalls.South) != 0);
            return (bottom ? 1 : 0) | (top ? 2 : 0);
        }
    }
}
