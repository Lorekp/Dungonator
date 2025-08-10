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
    /// Attach this class to the player to draw the maze around him
    /// </summary>
    public class MazeDrawer : MonoBehaviour
    {
        private MazeFloorData.CellData[,] _cells;
        private HashSet<Vector2Int> _loadedCellCoords =  new();
        private int _cellSize;
        private readonly Dictionary<CellShapes, MazeCellTemplate> _templateMap = new();
        
        private Transform _playerTransform;
        private Vector3Int _playerCellPosition;
        
        private int _renderDistance = 4;
        
        private Grid _grid;
        private Tilemap _tilemap;
        
        private bool _isInitialized;

        public void Init(MazeFloorData.CellData[,] cells)
        {
            _playerTransform = gameObject.transform;
            _cells = cells;
            EnsureTilemapAndGrid();
            AssignTemplatesToMap();
            _cellSize = _templateMap[0].GetCellSize();
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

        private void UpdateMaze(int distance) //distance in cells
        {
            var cellsToLoad = GetReachableCoords(new Vector2Int(_playerCellPosition.x, _playerCellPosition.y), distance);

            var cellsToUnload = _loadedCellCoords.Except(cellsToLoad);
            
            LoadCells(cellsToLoad.Except(_loadedCellCoords).ToHashSet()); //only load not-yet-loaded cells
            UnloadCells(cellsToUnload.ToHashSet());
        }
        
        private Vector3Int[] _posBuf;   // reused tile-position buffer
        private TileBase[]   _tileBuf;  // reused tile buffer
        
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
                var cell       = _cells[cellCoord.x, cellCoord.y];
                var template   = _templateMap[cell.CellShape].flatLayout;

                // safety check: wrong-sized template → skip
                if (template == null || template.Length != tilesPerCell)
                {
                    Debug.LogError($"Template for {cell.CellShape} is wrong size");
                    continue;
                }
                
                var originX = cellCoord.x * _cellSize;
                var originY = cellCoord.y * _cellSize;

                var idx = 0;
                for (var y = 0; y < _cellSize; y++)
                for (var x = 0; x < _cellSize; x++)
                {
                    // invert Y so template row 0 goes to top
                    _posBuf[idx]  = new Vector3Int(originX + x,
                        originY + (_cellSize - 1 - y),
                        0);

                    _tileBuf[idx] = template[idx];          // idx = y * cellSize + x
                    idx++;
                }

                _tilemap.SetTiles(_posBuf, _tileBuf);
            }
            
            _loadedCellCoords.UnionWith(cellsToLoad);
        }

        private void UnloadCells(HashSet<Vector2Int> cellsToUnload)
        {
            var tilesPerCell = _cellSize * _cellSize;

            // ensure scratch buffers exist
            if (_posBuf == null || _posBuf.Length != tilesPerCell)
                _posBuf = new Vector3Int[tilesPerCell];
            if (_tileBuf == null || _tileBuf.Length != tilesPerCell)
                _tileBuf = new TileBase[tilesPerCell];

            foreach (var cellCoord in cellsToUnload)
            {
                // wipe tile buffer to all-nulls (one fast mem-clear)
                Array.Clear(_tileBuf, 0, tilesPerCell);

                var originX = cellCoord.x * _cellSize;
                var originY = cellCoord.y * _cellSize;

                var idx = 0;
                for (var y = 0; y < _cellSize; y++)
                for (var x = 0; x < _cellSize; x++)
                {
                    // same inverted-Y positioning used in LoadCells
                    _posBuf[idx++] = new Vector3Int(originX + x,
                        originY + (_cellSize - 1 - y),
                        0);
                }

                // one batch call clears the whole cell
                _tilemap.SetTiles(_posBuf, _tileBuf);
            }
            
            _loadedCellCoords.ExceptWith(cellsToUnload);
        }

        /// <summary>
        /// Returns all grid coordinates reachable from <paramref name="start"/>
        /// within <paramref name="range"/> steps, following each cell's openings.
        /// </summary>
        private HashSet<Vector2Int> GetReachableCoords(
            Vector2Int start,
            int range
        )
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
                    continue; // outside maze bounds
                if (steps == 0)
                    continue; // range exhausted

                var neighbors = new List<Vector2Int>();
                AddConnectedNeighbors(pos, _cells[pos.x, pos.y].CellShape, neighbors);

                foreach (var nbr in neighbors)
                {
                    if (nbr.x < 0 || nbr.y < 0 || nbr.x >= maxX || nbr.y >= maxY)
                        continue; // skip out-of-bounds neighbors
                    if (!reachable.Add(nbr))  // already visited?
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
                if (tpl == null)
                {
                    Debug.LogWarning($"Found a null entry in CellTemplates/Layer1");
                    continue;
                }

                if (_templateMap.TryAdd(tpl.shape, tpl)) continue;
                Debug.LogError($"Duplicate template for shape {tpl.shape}");
            }
            
            foreach (CellShapes shape in System.Enum.GetValues(typeof(CellShapes)))
            {
                if (!_templateMap.ContainsKey(shape))
                    Debug.LogError($"Missing MazeCellTemplate for shape {shape}");
            }
        }

        private void EnsureTilemapAndGrid()
        {
            if (FindFirstObjectByType<Grid>() != null)
                return;

            var grid = new GameObject("Grid", typeof(Grid));
            
            var tilemap = new GameObject("Tilemap", typeof(Tilemap), typeof(TilemapRenderer));
            tilemap.transform.SetParent(grid.transform, worldPositionStays: false);
            
            SceneManager.MoveGameObjectToScene(grid, gameObject.scene);
            
            _tilemap = tilemap.GetComponent<Tilemap>();
        }

        /// <summary>
        /// Returns true if Player is not in the same cell he was before
        /// </summary>
        /// <returns></returns>
        private bool UpdateCurrentCellPos()
        {
            var currentCellPos = _tilemap.WorldToCell(_playerTransform.position) / _cellSize;
            if (currentCellPos == _playerCellPosition)
                return false;
            
            
            _playerCellPosition = currentCellPos;
            Debug.LogError($"UpdateCurrentCellPos: {currentCellPos}, CellShape: {_cells[_playerCellPosition.x, _playerCellPosition.y].CellShape}");
            return true;
        }

        private static void AddConnectedNeighbors(
            Vector2Int cellPos,
            CellShapes shape,
            List<Vector2Int> buffer)
        {
            switch (shape)
            {
                // ───────────── Straight ─────────────
                case CellShapes.VerticalI:
                    buffer.Add(cellPos + Vector2Int.up);
                    buffer.Add(cellPos + Vector2Int.down);
                    break;

                case CellShapes.HorizontalI:
                    buffer.Add(cellPos + Vector2Int.left);
                    buffer.Add(cellPos + Vector2Int.right);
                    break;

                // ───────────── Elbows (L) ─────────────
                // base L = opens to Up + Right
                case CellShapes.L:
                    buffer.Add(cellPos + Vector2Int.up);
                    buffer.Add(cellPos + Vector2Int.right);
                    break;

                case CellShapes.L90: // rotated 90° CW → Right + Down
                    buffer.Add(cellPos + Vector2Int.right);
                    buffer.Add(cellPos + Vector2Int.down);
                    break;

                case CellShapes.L180: // rotated 180° CW → Down + Left
                    buffer.Add(cellPos + Vector2Int.down);
                    buffer.Add(cellPos + Vector2Int.left);
                    break;

                case CellShapes.L270: // rotated 270° CW → Left + Up
                    buffer.Add(cellPos + Vector2Int.left);
                    buffer.Add(cellPos + Vector2Int.up);
                    break;

                // ───────────── Cross ─────────────
                case CellShapes.X:
                    buffer.Add(cellPos + Vector2Int.up);
                    buffer.Add(cellPos + Vector2Int.right);
                    buffer.Add(cellPos + Vector2Int.down);
                    buffer.Add(cellPos + Vector2Int.left);
                    break;

                // ───────────── T-junctions ─────────────
                // base T = opens to Left + Right + Down
                case CellShapes.T:
                    buffer.Add(cellPos + Vector2Int.left);
                    buffer.Add(cellPos + Vector2Int.right);
                    buffer.Add(cellPos + Vector2Int.down);
                    break;

                case CellShapes.T90: // rotated 90° CW → Up + Left + Down
                    buffer.Add(cellPos + Vector2Int.up);
                    buffer.Add(cellPos + Vector2Int.left);
                    buffer.Add(cellPos + Vector2Int.down);
                    break;

                case CellShapes.T180: // rotated 180° CW → Left + Right + Up
                    buffer.Add(cellPos + Vector2Int.left);
                    buffer.Add(cellPos + Vector2Int.right);
                    buffer.Add(cellPos + Vector2Int.up);
                    break;

                case CellShapes.T270: // rotated 270° CW → Up + Right + Down
                    buffer.Add(cellPos + Vector2Int.up);
                    buffer.Add(cellPos + Vector2Int.right);
                    buffer.Add(cellPos + Vector2Int.down);
                    break;

                // ───────────── Dead-ends ─────────────
                // base DeadEnd = Down
                case CellShapes.DeadEnd:
                    buffer.Add(cellPos + Vector2Int.up);
                    break;

                case CellShapes.DeadEnd90: // rotated 90° CW → Left
                    buffer.Add(cellPos + Vector2Int.right);
                    break;

                case CellShapes.DeadEnd180: // rotated 180° CW → Up
                    buffer.Add(cellPos + Vector2Int.down);
                    break;

                case CellShapes.DeadEnd270: // rotated 270° CW → Right
                    buffer.Add(cellPos + Vector2Int.left);
                    break;

                // ───────────── Sealed block ─────────────
                case CellShapes.Box:
                    // no openings
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(shape), shape, null);
            }
        }
    }
}