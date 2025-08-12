using System;
using System.Collections.Generic;
using System.Linq;
using Dungonator.Client.Layers.Maze.MazeCellTemplates;
using Dungonator.Shared.Dungeon;
using Dungonator.Shared.Dungeon.Layers.Maze;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace Dungonator.Client.Layers.Maze
{
    /// <summary>
    /// Draws the maze around the player.
    /// Assumptions:
    ///   • _cells is indexed [row, col] == [y, x], with row 0 at the TOP (north).
    ///   • Templates store row 0 = bottom (no per-cell Y flip).
    ///   • 1 world unit == 1 tile.
    /// </summary>
    public class MazeDrawer : MonoBehaviour
    {
        private MazeFloorData.CellData[,] _cells;      // [row, col] (row 0 = top)
        private readonly HashSet<Vector2Int> _loadedCellCoords = new();
        private int _cellSize;
        private readonly Dictionary<CellShapes, MazeCellTemplate> _templateMap = new();

        private Transform _playerTransform;
        // stores (col, row)
        private Vector3Int _playerCellPosition;

        [SerializeField] private int _renderDistance = 4;

        private Grid _grid;
        private Tilemap _tilemap;

        private bool _isInitialized;

        // Reused buffers for batched SetTiles
        private Vector3Int[] _posBuf;
        private TileBase[]   _tileBuf;

        public void Init(MazeFloorData.CellData[,] cells)
        {
            _playerTransform = transform;
            _cells = cells;

            EnsureTilemapAndGrid();
            AssignTemplatesToMap();

            _cellSize = _templateMap.Values.First().GetCellSize();

            var tilesPerCell = _cellSize * _cellSize;
            _posBuf  = new Vector3Int[tilesPerCell];
            _tileBuf = new TileBase[tilesPerCell];

            UpdateCurrentCellPos();
            UpdateMaze(_renderDistance);
            ClientMazePhysicsSetup.ConfigureClientColliders(_tilemap);
            _isInitialized = true;
        }

        private void Update()
        {
            if (!_isInitialized) return;

            if (!UpdateCurrentCellPos())
                return;

            UpdateMaze(_renderDistance);
        }

        private void UpdateMaze(int distance)
        {
            var start = new Vector2Int(_playerCellPosition.x, _playerCellPosition.y); // (col,row)
            var cellsToLoad   = GetReachableCoords(start, distance);
            var cellsToUnload = _loadedCellCoords.Except(cellsToLoad);

            LoadCells(cellsToLoad.Except(_loadedCellCoords).ToHashSet());
            UnloadCells(cellsToUnload.ToHashSet());
        }

        private void LoadCells(HashSet<Vector2Int> cellsToLoad)
        {
            var tilesPerCell = _cellSize * _cellSize;
            if (_posBuf == null || _posBuf.Length != tilesPerCell)
            {
                _posBuf  = new Vector3Int[tilesPerCell];
                _tileBuf = new TileBase[tilesPerCell];
            }

            int rows = _cells.GetLength(0);
            int cols = _cells.GetLength(1);

            foreach (var cellCoord in cellsToLoad)
            {
                int col = cellCoord.x; // x
                int row = cellCoord.y; // y

                if (row < 0 || row >= rows || col < 0 || col >= cols)
                    continue;

                var cell     = _cells[row, col]; // [row, col]
                var template = _templateMap[cell.CellShape].flatLayout;

                if (template == null || template.Length != tilesPerCell)
                {
                    Debug.LogError($"Template for {cell.CellShape} has wrong size.");
                    continue;
                }

                // Bottom-left origin of this cell in world-tiles.
                // row 0 (top) should appear at highest Y -> flip rows here.
                var originX = col * _cellSize;
                var originY = (rows - 1 - row) * _cellSize;

                var idx = 0;
                for (int yLocal = 0; yLocal < _cellSize; yLocal++)
                for (int xLocal = 0; xLocal < _cellSize; xLocal++)
                {
                    _posBuf[idx]  = new Vector3Int(originX + xLocal,
                                                   originY + yLocal,
                                                   0);

                    // flatLayout row 0 = bottom (matches yLocal=0 bottom row)
                    _tileBuf[idx] = template[yLocal * _cellSize + xLocal];
                    idx++;
                }

                _tilemap.SetTiles(_posBuf, _tileBuf);
            }

            _loadedCellCoords.UnionWith(cellsToLoad);
        }

        private void UnloadCells(HashSet<Vector2Int> cellsToUnload)
        {
            var tilesPerCell = _cellSize * _cellSize;
            if (_posBuf == null || _posBuf.Length != tilesPerCell)
                _posBuf = new Vector3Int[tilesPerCell];
            if (_tileBuf == null || _tileBuf.Length != tilesPerCell)
                _tileBuf = new TileBase[tilesPerCell];

            Array.Clear(_tileBuf, 0, _tileBuf.Length);

            int rows = _cells.GetLength(0);

            foreach (var cellCoord in cellsToUnload)
            {
                int col = cellCoord.x;
                int row = cellCoord.y;

                var originX = col * _cellSize;
                var originY = (rows - 1 - row) * _cellSize;

                var idx = 0;
                for (int yLocal = 0; yLocal < _cellSize; yLocal++)
                for (int xLocal = 0; xLocal < _cellSize; xLocal++)
                    _posBuf[idx++] = new Vector3Int(originX + xLocal, originY + yLocal, 0);

                _tilemap.SetTiles(_posBuf, _tileBuf);
            }

            _loadedCellCoords.ExceptWith(cellsToUnload);
        }

        /// <summary>
        /// Returns all grid coordinates reachable from start within range steps.
        /// start = (col,row). Row 0 is TOP. North = row-1, South = row+1.
        /// </summary>
        private HashSet<Vector2Int> GetReachableCoords(Vector2Int start, int range)
        {
            var reachable = new HashSet<Vector2Int>();
            var queue     = new Queue<(Vector2Int pos, int stepsLeft)>();

            int rows = _cells.GetLength(0); // y
            int cols = _cells.GetLength(1); // x

            if (start.x < 0 || start.y < 0 || start.x >= cols || start.y >= rows)
                return reachable;

            reachable.Add(start);
            queue.Enqueue((start, range));

            while (queue.Count > 0)
            {
                var (pos, steps) = queue.Dequeue();
                int col = pos.x, row = pos.y;

                if (col < 0 || row < 0 || col >= cols || row >= rows)
                    continue;
                if (steps == 0)
                    continue;

                var neighbors = new List<Vector2Int>();
                AddConnectedNeighbors(col, row, _cells[row, col].CellShape, neighbors);

                foreach (var nbr in neighbors)
                {
                    if (nbr.x < 0 || nbr.y < 0 || nbr.x >= cols || nbr.y >= rows)
                        continue;
                    if (!reachable.Add(nbr))
                        continue;

                    queue.Enqueue((nbr, steps - 1));
                }
            }

            return reachable;
        }

        private void AssignTemplatesToMap()
        {
            var templates = Resources.LoadAll<MazeCellTemplate>("CellTemplates/Layer1");

            _templateMap.Clear();

            foreach (var tpl in templates)
            {
                if (tpl == null) { Debug.LogWarning("Null template in CellTemplates/Layer1"); continue; }
                if (!_templateMap.TryAdd(tpl.shape, tpl))
                    Debug.LogError($"Duplicate template for shape {tpl.shape}");
            }

            foreach (CellShapes shape in Enum.GetValues(typeof(CellShapes)))
                if (!_templateMap.ContainsKey(shape))
                    Debug.LogError($"Missing MazeCellTemplate for shape {shape}");
        }

        private void EnsureTilemapAndGrid()
        {
            if (_tilemap != null) return;

            var myScene = gameObject.scene;

            _grid = FindObjectsByType<Grid>(FindObjectsSortMode.None)
                    .FirstOrDefault(g => g.gameObject.scene == myScene);

            if (_grid == null)
            {
                var go = new GameObject("Grid", typeof(Grid));
                SceneManager.MoveGameObjectToScene(go, myScene);
                _grid = go.GetComponent<Grid>();
            }

            _tilemap = _grid.GetComponentInChildren<Tilemap>();
            if (_tilemap == null)
            {
                var tgo = new GameObject("Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
                tgo.transform.SetParent(_grid.transform, false);
                _tilemap = tgo.GetComponent<Tilemap>();
            }
        }

        /// <summary>
        /// Updates cached (col,row). Returns true if changed.
        /// Row 0 is TOP: row = rows-1 - floor(y / cellSize)
        /// </summary>
        private bool UpdateCurrentCellPos()
        {
            int rows = _cells.GetLength(0);
            int cols = _cells.GetLength(1);

            var p  = _playerTransform.position;
            var col = Mathf.FloorToInt(p.x / _cellSize);
            var row = rows - 1 - Mathf.FloorToInt(p.y / _cellSize); // flip from world +Y to top-based row

            var current = new Vector3Int(col, row, 0);
            if (current == _playerCellPosition)
                return false;

            _playerCellPosition = current;

            var sx = Mathf.Clamp(col, 0, cols - 1);
            var sy = Mathf.Clamp(row, 0, rows - 1);

            Debug.Log($"[MazeDrawer] PlayerCell (col,row)=({col},{row}) shape={_cells[sy, sx].CellShape}");
            return true;
        }

        /// <summary>
        /// Adds neighbors using (col,row) with row 0 = TOP:
        /// North = (col, row-1), East = (col+1, row), South = (col, row+1), West = (col-1, row)
        /// </summary>
        private static void AddConnectedNeighbors(int col, int row, CellShapes shape, List<Vector2Int> buffer)
        {
            switch (shape)
            {
                // Straights
                case CellShapes.VerticalI:
                    buffer.Add(new Vector2Int(col,     row - 1)); // north
                    buffer.Add(new Vector2Int(col,     row + 1)); // south
                    break;

                case CellShapes.HorizontalI:
                    buffer.Add(new Vector2Int(col - 1, row)); // west
                    buffer.Add(new Vector2Int(col + 1, row)); // east
                    break;

                // Elbows (L): base is Up + Right
                case CellShapes.L:
                    buffer.Add(new Vector2Int(col,     row - 1)); // up
                    buffer.Add(new Vector2Int(col + 1, row));     // right
                    break;

                case CellShapes.L90: // Right + Down
                    buffer.Add(new Vector2Int(col + 1, row));
                    buffer.Add(new Vector2Int(col,     row + 1));
                    break;

                case CellShapes.L180: // Down + Left
                    buffer.Add(new Vector2Int(col,     row + 1));
                    buffer.Add(new Vector2Int(col - 1, row));
                    break;

                case CellShapes.L270: // Left + Up
                    buffer.Add(new Vector2Int(col - 1, row));
                    buffer.Add(new Vector2Int(col,     row - 1));
                    break;

                // Cross
                case CellShapes.X:
                    buffer.Add(new Vector2Int(col,     row - 1));
                    buffer.Add(new Vector2Int(col + 1, row));
                    buffer.Add(new Vector2Int(col,     row + 1));
                    buffer.Add(new Vector2Int(col - 1, row));
                    break;

                // T junctions (base has wall North → opens Left+Right+Down)
                case CellShapes.T:
                    buffer.Add(new Vector2Int(col - 1, row)); // left
                    buffer.Add(new Vector2Int(col + 1, row)); // right
                    buffer.Add(new Vector2Int(col,     row + 1)); // down
                    break;

                case CellShapes.T90: // wall East → Up + Left + Down
                    buffer.Add(new Vector2Int(col,     row - 1));
                    buffer.Add(new Vector2Int(col - 1, row));
                    buffer.Add(new Vector2Int(col,     row + 1));
                    break;

                case CellShapes.T180: // wall South → Left + Right + Up
                    buffer.Add(new Vector2Int(col - 1, row));
                    buffer.Add(new Vector2Int(col + 1, row));
                    buffer.Add(new Vector2Int(col,     row - 1));
                    break;

                case CellShapes.T270: // wall West → Up + Right + Down
                    buffer.Add(new Vector2Int(col,     row - 1));
                    buffer.Add(new Vector2Int(col + 1, row));
                    buffer.Add(new Vector2Int(col,     row + 1));
                    break;

                // Dead-ends
                case CellShapes.DeadEnd:      // open up
                    buffer.Add(new Vector2Int(col, row - 1));
                    break;
                case CellShapes.DeadEnd90:    // open right
                    buffer.Add(new Vector2Int(col + 1, row));
                    break;
                case CellShapes.DeadEnd180:   // open down
                    buffer.Add(new Vector2Int(col, row + 1));
                    break;
                case CellShapes.DeadEnd270:   // open left
                    buffer.Add(new Vector2Int(col - 1, row));
                    break;

                // Sealed block
                case CellShapes.Box:
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(shape), shape, null);
            }
        }
    }
}
