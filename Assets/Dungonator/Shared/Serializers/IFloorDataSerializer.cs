using Dungonator.Shared.Dungeon;
using Dungonator.Shared.Dungeon.Layers.Maze;
using FishNet.CodeGenerating;
using FishNet.Serializing;
using UnityEngine;

namespace Dungonator.Shared.Serializers
{
    [UseGlobalCustomSerializer]
    public static class IFloorDataSerializer
    {
        public static void WriteIFloorData(this Writer writer, Shared.Dungeon.IFloorData floorData)
        {
            if (floorData is MazeFloorData mazeFloorData)
            {
                writer.WriteMazeFloorData(mazeFloorData);
            }
        }
        
        public static IFloorData ReadIFloorData(this Reader reader)
        {
            var classType = reader.ReadUInt8Unpacked();

            if (classType == 1)
            {
                return reader.ReadMazeFloorData();
            }

            return null;
        }
        
        public static void WriteMazeFloorData(this Writer writer, Shared.Dungeon.MazeFloorData mazeFloorData)
        {
            writer.WriteUInt8Unpacked(1);
            writer.WriteInt32(value: mazeFloorData.LayerIndex);
            writer.WriteInt32(value: mazeFloorData.FloorNumber);
            writer.WriteInt32(value: mazeFloorData.CellSize);
            writer.WriteCellData2DArray(mazeFloorData.Cells);
        }

        public static MazeFloorData ReadMazeFloorData(this Reader reader)
        {
            return new MazeFloorData
            {
                LayerIndex = reader.ReadInt32(),
                FloorNumber = reader.ReadInt32(),
                CellSize = reader.ReadInt32(),
                Cells = reader.ReadCellData2DArray()
            };
        }
    }

    [UseGlobalCustomSerializer]
    public static class CellDataSerializer
    {
        public static void WriteCellData(this Writer writer, MazeFloorData.CellData cell)
        {
            writer.WriteBoolean(cell.IsPlayerSpawn);
            writer.WriteBoolean(cell.IsBossSpawn);
            writer.WriteBoolean(cell.IsEliteSpawn);
            writer.WriteBoolean(cell.IsChallengeSpawn);
            // enums serialize as their underlying int
            writer.WriteInt32((int)cell.CellShape);
        }

        public static MazeFloorData.CellData ReadCellData(this Reader reader)
        {
            var cell = new MazeFloorData.CellData
            {
                IsPlayerSpawn    = reader.ReadBoolean(),
                IsBossSpawn      = reader.ReadBoolean(),
                IsEliteSpawn     = reader.ReadBoolean(),
                IsChallengeSpawn = reader.ReadBoolean(),
                CellShape        = (CellShapes)reader.ReadInt32()
            };
            return cell;
        }
        
        public static void WriteCellData2DArray(this Writer writer, MazeFloorData.CellData[,] array)
        {
            var rows = array.GetLength(0);
            var cols = array.GetLength(1);
            // first the dims
            writer.WriteInt32(rows);
            writer.WriteInt32(cols);
            // then each cell
            for (var i = 0; i < rows; i++)
            for (var j = 0; j < cols; j++)
                writer.WriteCellData(array[i, j]);
        }

        public static MazeFloorData.CellData[,] ReadCellData2DArray(this Reader reader)
        {
            var rows = reader.ReadInt32();
            var cols = reader.ReadInt32();
            var array = new MazeFloorData.CellData[rows, cols];
            for (var i = 0; i < rows; i++)
            for (var j = 0; j < cols; j++)
                array[i, j] = reader.ReadCellData();
            return array;
        }
    }
}