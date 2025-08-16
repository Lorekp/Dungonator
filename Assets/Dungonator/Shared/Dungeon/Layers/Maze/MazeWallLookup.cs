using System;

namespace Dungonator.Shared.Dungeon.Layers.Maze
{
    [Flags]
    public enum CellWalls { None=0, North=1<<0, East=1<<1, South=1<<2, West=1<<3 }

    public static class MazeWallLookup
    {
        // Index by (int)CellShapes (order must match your enum)
        public static readonly CellWalls[] ShapeToWalls =
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
            /* Box         */ CellWalls.North | CellWalls.East  | CellWalls.South | CellWalls.West,
        };

        public static CellWalls Get(CellShapes shape) => ShapeToWalls[(int)shape];
    }
}