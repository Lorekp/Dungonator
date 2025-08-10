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
    /// Attach this class to the player to draw the maze around him.
    /// Assumes MazeCellTemplate flatLayout is stored with row 0 = bottom.
    /// </summary>
    public class MazeDrawer : MonoBehaviour
    {
        private MazeFloorData.CellData[,] _cells;
        private readonly HashSet<Vector2Int> _loadedCellCoords = new();
        private int _cellSize;
        private readonly Dictionary<CellShapes, MazeCellTemplate> _templateMap = new();

        private Transform _playerTransform;
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

            // Pick any available template to derive cell size
            _cellSize = _templateMap.Values.First().GetCellSize();

            // Pre-size buffers
            var tilesPerCell = _cellSize * _cellSize;
            _posBuf  = new Vector3Int[tilesPerCell];
            _tileBuf = new TileBase[tilesPerCell];

            UpdateCurrentCellPos();
            UpdateMaze(_renderDistance);
            _isInitialized = true;
        }

        private void Update()
        {
            if (!_isInitialized) return;

            if (!UpdateCurrentCellPos())
                return;

            UpdateMaze(_renderDistance);
        }

        private void UpdateMaze(int distance) // distance in cells
        {
            var cellsToLoad = GetReachableCoords(new Vector2Int(_playerCellPosition.x, _playerCellPosition.y), distance);

            var cellsToUnload = _loadedCellCoords.Except(cellsToLoad);

            // Load only new cells; unload cells that dropped out
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

            foreach (var cellCoord in cellsToLoad)
            {
                var cell     = _cells[cellCoord.x, cellCoord.y];
                var template = _templateMap[cell.CellShape].flatLayout;

                if (template == null || template.Length != tilesPerCell)
                {
                    Debug.LogError($"Template for {cell.CellShape} has wrong size.");
                    continue;
                }

                // Bottom-left origin for this cell block (world tile coords)
                var originX = cellCoord.x * _cellSize;
                var originY = cellCoord.y * _cellSize;

                var idx = 0;
                for (int yLocal = 0; yLocal < _cellSize; yLocal++)
                for (int xLocal = 0; xLocal < _cellSize; xLocal++)
                {
                    // IMPORTANT: no Y inversion; yLocal=0 maps to bottom row
                    _posBuf[idx]  = new Vector3Int(originX + xLocal,
                                                   originY + yLocal,
                                                   0);

                    // flatLayout is row-major with row 0 = bottom
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

            foreach (var cellCoord in cellsToUnload)
            {
                Array.Clear(_tileBuf, 0, tilesPerCell);

                var originX = cellCoord.x * _cellSize;
                var originY = cellCoord.y * _cellSize;

                var idx = 0;
                for (int yLocal = 0; yLocal < _cellSize; yLocal++)
                for (int xLocal = 0; xLocal < _cellSize; xLocal++)
                {
                    _posBuf[idx++] = new Vector3Int(originX + xLocal,
                                                    originY + yLocal,
                                                    0);
                }

                _tilemap.SetTiles(_posBuf, _tileBuf);
            }

            _loadedCellCoords.ExceptWith(cellsToUnload);
        }

        /// <summary>
        /// Returns all grid coordinates reachable from <paramref name="start"/>
        /// within <paramref name="range"/> steps, following each cell's openings.
        /// </summary>
        private HashSet<Vector2Int> GetReachableCoords(Vector2Int start, int range)
        {
            var reachable = new HashSet<Vector2Int>();
            var queue     = new Queue<(Vector2Int pos, int stepsLeft)>();

            var maxX = _cells.GetLength(0);
            var maxY = _cells.GetLength(1);

            if (start.x < 0 || start.y < 0 || start.x >= maxX || start.y >= maxY)
                return reachable;

            reachable.Add(start);
            queue.Enqueue((start, range));

            while (queue.Count > 0)
            {
                var (pos, steps) = queue.Dequeue();

                if (pos.x < 0 || pos.y < 0 || pos.x >= maxX || pos.y >= maxY)
                    continue;
                if (steps == 0)
                    continue;

                var neighbors = new List<Vector2Int>();
                AddConnectedNeighbors(pos, _cells[pos.x, pos.y].CellShape, neighbors);

                foreach (var nbr in neighbors)
                {
                    if (nbr.x < 0 || nbr.y < 0 || nbr.x >= maxX || nbr.y >= maxY)
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
            {
                if (!_templateMap.ContainsKey(shape))
                    Debug.LogError($"Missing MazeCellTemplate for shape {shape}");
            }
        }

        private void EnsureTilemapAndGrid()
        {
            if (_tilemap != null) return;

            var myScene = gameObject.scene;

            // Find Grid in this scene
            _grid = FindObjectsByType<Grid>(FindObjectsSortMode.None)
                    .FirstOrDefault(g => g.gameObject.scene == myScene);

            if (_grid == null)
            {
                var go = new GameObject("Grid", typeof(Grid));
                SceneManager.MoveGameObjectToScene(go, myScene);
                _grid = go.GetComponent<Grid>();
            }

            // Find or create Tilemap under that Grid
            _tilemap = _grid.GetComponentInChildren<Tilemap>();
            if (_tilemap == null)
            {
                var tgo = new GameObject("Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
                tgo.transform.SetParent(_grid.transform, false);
                _tilemap = tgo.GetComponent<Tilemap>();
            }
        }

        /// <summary>
        /// Updates the cached player cell index. Returns true if it changed.
        /// </summary>
        private bool UpdateCurrentCellPos()
        {
            // Robust math: 1 world unit == 1 tile
            var p = _playerTransform.position;
            var cx = Mathf.FloorToInt(p.x / _cellSize);
            var cy = Mathf.FloorToInt(p.y / _cellSize);

            var current = new Vector3Int(cx, cy, 0);
            if (current == _playerCellPosition)
                return false;

            _playerCellPosition = current;

            // Optional: safe preview without OOB
            var maxX = _cells.GetLength(0);
            var maxY = _cells.GetLength(1);
            var sx = Mathf.Clamp(cx, 0, maxX - 1);
            var sy = Mathf.Clamp(cy, 0, maxY - 1);

            Debug.LogError($"[MazeDrawer] PlayerCell=({cx},{cy}) shape={_cells[sx, sy].CellShape}");
            return true;
        }

        private static void AddConnectedNeighbors(Vector2Int cellPos, CellShapes shape, List<Vector2Int> buffer)
        {
            switch (shape)
            {
                // Straights
                case CellShapes.VerticalI:
                    buffer.Add(cellPos + Vector2Int.up);
                    buffer.Add(cellPos + Vector2Int.down);
                    break;

                case CellShapes.HorizontalI:
                    buffer.Add(cellPos + Vector2Int.left);
                    buffer.Add(cellPos + Vector2Int.right);
                    break;

                // Elbows (L)
                case CellShapes.L:
                    buffer.Add(cellPos + Vector2Int.up);
                    buffer.Add(cellPos + Vector2Int.right);
                    break;

                case CellShapes.L90:
                    buffer.Add(cellPos + Vector2Int.right);
                    buffer.Add(cellPos + Vector2Int.down);
                    break;

                case CellShapes.L180:
                    buffer.Add(cellPos + Vector2Int.down);
                    buffer.Add(cellPos + Vector2Int.left);
                    break;

                case CellShapes.L270:
                    buffer.Add(cellPos + Vector2Int.left);
                    buffer.Add(cellPos + Vector2Int.up);
                    break;

                // Cross
                case CellShapes.X:
                    buffer.Add(cellPos + Vector2Int.up);
                    buffer.Add(cellPos + Vector2Int.right);
                    buffer.Add(cellPos + Vector2Int.down);
                    buffer.Add(cellPos + Vector2Int.left);
                    break;

                // T junctions
                case CellShapes.T:
                    buffer.Add(cellPos + Vector2Int.left);
                    buffer.Add(cellPos + Vector2Int.right);
                    buffer.Add(cellPos + Vector2Int.down);
                    break;

                case CellShapes.T90:
                    buffer.Add(cellPos + Vector2Int.up);
                    buffer.Add(cellPos + Vector2Int.left);
                    buffer.Add(cellPos + Vector2Int.down);
                    break;

                case CellShapes.T180:
                    buffer.Add(cellPos + Vector2Int.left);
                    buffer.Add(cellPos + Vector2Int.right);
                    buffer.Add(cellPos + Vector2Int.up);
                    break;

                case CellShapes.T270:
                    buffer.Add(cellPos + Vector2Int.up);
                    buffer.Add(cellPos + Vector2Int.right);
                    buffer.Add(cellPos + Vector2Int.down);
                    break;

                // Dead-ends
                case CellShapes.DeadEnd:      // open up
                    buffer.Add(cellPos + Vector2Int.up);
                    break;
                case CellShapes.DeadEnd90:    // open right
                    buffer.Add(cellPos + Vector2Int.right);
                    break;
                case CellShapes.DeadEnd180:   // open down
                    buffer.Add(cellPos + Vector2Int.down);
                    break;
                case CellShapes.DeadEnd270:   // open left
                    buffer.Add(cellPos + Vector2Int.left);
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
