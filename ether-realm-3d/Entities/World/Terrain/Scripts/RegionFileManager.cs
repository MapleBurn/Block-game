using System.IO;
using EtherRealm3D.Entities.World.Terrain.Scripts.Data;
using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts;

public static class RegionFileManager
{
    public const int RegionSize = 8;

    private const string SaveDir  = "game_data/worlds/world1/";
    private const uint   Magic    = 0x45544852; // "ETHR"
    private const ushort Version  = 1;

    public static void SaveChunk(ChunkData data)
    {
        Directory.CreateDirectory(SaveDir);

        using var stream = new FileStream(SaveDir + data.ChunkCoordinate.ToString(), FileMode.Create);
        using var writer = new BinaryWriter(stream);
        
        writer.Write(Magic);
        writer.Write(Version);
        
        writer.Write(data.ChunkCoordinate.X);  
        writer.Write(data.ChunkCoordinate.Y);  
        writer.Write(data.ChunkCoordinate.Z);
        
        writer.Write(data.Densities.GetLength(0));  
        writer.Write(data.Densities.GetLength(1));  
        writer.Write(data.Densities.GetLength(2));  
        for (int x = 0; x < data.Densities.GetLength(0); x++)  
            for (int y = 0; y < data.Densities.GetLength(1); y++)  
                for (int z = 0; z < data.Densities.GetLength(2); z++)  
                    writer.Write(data.Densities[x, y, z]);
    }  
  
    public static ChunkData LoadChunk(Vector3I chunkCoordinate)
    {
        var path = SaveDir + chunkCoordinate.ToString();
        if (!File.Exists(path))
            return null;

        using var stream = new FileStream(path, FileMode.Open);
        using var reader = new BinaryReader(stream);

        var magic = reader.ReadInt32();
        var version = reader.ReadInt16();
        if (magic != Magic || version != Version)
            throw new InvalidDataException($"Invalid chunk file for {chunkCoordinate}.");

        var coord = new Vector3I(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());

        int sizeX = reader.ReadInt32();
        int sizeY = reader.ReadInt32();
        int sizeZ = reader.ReadInt32();

        var densities = new float[sizeX, sizeY, sizeZ];
        for (int x = 0; x < sizeX; x++)
        for (int y = 0; y < sizeY; y++)
        for (int z = 0; z < sizeZ; z++)
            densities[x, y, z] = reader.ReadSingle();

        return new ChunkData(coord, densities);
    }
}