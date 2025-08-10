using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace Dungonator.Server.Layers.Maze.Scripts.MazeGeneration
{
    public class Grid
    {
        public int Rows { get; }
        public int Columns { get; }
        private Cell[,] _cells;
        private readonly Random _rng;

        public Grid(int rows, int columns, Random rng)
        {
            Rows = rows;
            Columns = columns;
            _rng = rng;
            _cells = new Cell[Rows, Columns];
            PrepareGrid();
            InitializeCellNeighbors();
        }

        private void PrepareGrid()
        {
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    _cells[row, col] = new Cell(row, col);
                }
            }
        }

        private void InitializeCellNeighbors()
        {
            for (var row = 0; row < Rows; row++)
            {
                for (var col = 0; col < Columns; col++)
                {
                    var cell = _cells[row, col];
                    if (GetCell(row - 1, col) != null) cell.Neighbors.Add(GetCell(row - 1, col)); // North
                    if (GetCell(row + 1, col) != null) cell.Neighbors.Add(GetCell(row + 1, col)); // South
                    if (GetCell(row, col - 1) != null) cell.Neighbors.Add(GetCell(row, col - 1)); // West
                    if (GetCell(row, col + 1) != null) cell.Neighbors.Add(GetCell(row, col + 1)); // East
                }
            }
        }
        
        public Cell GetCell(int row, int col)
        {
            if (row < 0 || row >= Rows || col < 0 || col >= Columns)
                return null;
            return _cells[row, col];
        }

        public Cell RandomCell()
        {
            var row = _rng.Next(Rows);
            var col = _rng.Next(Columns);
            return _cells[row, col];
        }

        public IEnumerable<Cell> EachCell()
        {
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    yield return _cells[row, col];
                }
            }
        }
    }
}