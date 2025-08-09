using System;
using System.Collections.Generic;
using Dungonator.Server.Layers.Maze.Scripts.Enums;
using Dungonator.Shared.Dungeon.Layers.Maze;

namespace Dungonator.Server.Layers.Maze.Scripts.MazeGeneration
{
    public class Cell
    {
        public bool IsPlayerSpawn = false;
        public bool IsBossSpawn = false;
        public bool IsEnemySpawn = false;
        public bool IsChallengeSpawn = false;
        public int Row { get; }
        public int Column { get; }
        public int Type { get; set; }
        public CellWalls Walls { get; set; }
        public List<Cell> Neighbors { get; } = new();
        public Dictionary<Cell, bool> Links { get; } = new();
        private static readonly Dictionary<CellWalls, CellShapes> ShapeMap = new() //map shapes to wall combinations
        {
            // 4 walls
            [CellWalls.North | CellWalls.East | CellWalls.South | CellWalls.West] = CellShapes.Box,

            // 3 walls (1 open side) => DEAD ENDS
            [CellWalls.East | CellWalls.South | CellWalls.West]  = CellShapes.DeadEnd,     // opens North
            [CellWalls.North | CellWalls.South | CellWalls.West] = CellShapes.DeadEnd90,  // opens East
            [CellWalls.North | CellWalls.East | CellWalls.West]  = CellShapes.DeadEnd180, // opens South
            [CellWalls.North | CellWalls.East | CellWalls.South] = CellShapes.DeadEnd270, // opens West

            // 1 wall (3 open sides) => T SHAPES
            [CellWalls.North] = CellShapes.T,     // wall at North, opens South
            [CellWalls.East]  = CellShapes.T90,   // wall at East, opens West
            [CellWalls.South] = CellShapes.T180,  // wall at South, opens North
            [CellWalls.West]  = CellShapes.T270,  // wall at West, opens East

            // 2 walls opposite => I SHAPES
            [CellWalls.East | CellWalls.West] = CellShapes.VerticalI,
            [CellWalls.North  | CellWalls.South]  = CellShapes.HorizontalI,

            // 2 walls adjacent => L SHAPES
            [CellWalls.South | CellWalls.West]  = CellShapes.L,
            [CellWalls.North | CellWalls.West]  = CellShapes.L90,
            [CellWalls.North | CellWalls.East]  = CellShapes.L180,
            [CellWalls.South | CellWalls.East]  = CellShapes.L270,

            // 0 walls
            [CellWalls.None] = CellShapes.X
        };

        public Cell(int row, int column)
        {
            Row = row;
            Column = column;
            Type = (int)CellTypes.Empty;
            
            Walls = CellWalls.North | CellWalls.East | CellWalls.South | CellWalls.West; //No Links, walls everywhere
        }

        public void Link(Cell cell, bool bidirectional = true)
        {
            if (!Links.TryAdd(cell, true)) return; //try to link
                
            //knock down wall
            Walls &= ~GetCardinalDirection(cell);
            
            if (bidirectional) //link neighbor if bidirectional
                cell.Link(this, false);
        }

        private CellWalls GetCardinalDirection(Cell cell)
        {
            //find neighbors cardinal direction in relation to this cell
            int dRow = cell.Row - this.Row;
            int dCol = cell.Column - this.Column;

            CellWalls cardinalDirection;

            if (dRow == -1)      cardinalDirection = CellWalls.North;
            else if (dRow == 1)  cardinalDirection = CellWalls.South;
            else if (dCol == -1) cardinalDirection = CellWalls.West;
            else if (dCol == 1)  cardinalDirection = CellWalls.East;
            else throw new Exception("Cells are not adjacent!");
            
            return cardinalDirection;
        }
        
        public void Unlink(Cell cell, bool bidirectional = true)
        {
            if (!Links.Remove(cell)) return;
        
            if (bidirectional)
                cell.Unlink(this, false);
        }

        public bool IsLinked(Cell cell)
        {
            return Links.ContainsKey(cell);
        }

        public CellShapes GetCellShape()
        {
            if (ShapeMap.TryGetValue(Walls, out var shape))
                return shape;
            
            throw new InvalidOperationException($"Unable to find cell shape for walls: {Walls}");
        }
        
        private bool HasWalls(CellWalls walls, CellWalls a, CellWalls b)
        {
            return (walls & a) != 0 && (walls & b) != 0;
        }
    }
}
