namespace Dungonator.Shared.Dungeon.Layers.Maze
{
    public enum CellShapes
    {
        //L90, L180 ... are rotated by x degrees clockwise
        VerticalI,
        HorizontalI,
        L, 
        L90,
        L180,
        L270,
        X,
        T,
        T90,
        T180,
        T270,
        DeadEnd,
        DeadEnd90,
        DeadEnd180,
        DeadEnd270,
        Box
    }

    public enum CellTypes
    {
        Empty,
        Boss,
        Challenge,
        PlayerSpawn
    }
}