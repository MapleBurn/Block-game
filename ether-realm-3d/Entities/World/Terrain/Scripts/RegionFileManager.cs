using System;
using System.Collections.Generic;
using System.IO;
using EtherRealm3D.Entities.World.Terrain.Scripts.Data;
using Godot;
using FileAccess = System.IO.FileAccess;

namespace EtherRealm3D.Entities.World.Terrain.Scripts;

public class RegionFileManager
{
    public const int RegionSize = 8;

    private const string SaveDir  = "user://Shooter/saves/";
    private const uint   Magic    = 0x45544852; // "ETHR"
    private const ushort Version  = 1;

    /// <summary>
    /// Call this whenever chunks are unloaded. Chunks are automatically grouped
    /// into their 8×8×8 region files. Existing region files are merged so that
    /// chunks already on disk are preserved unless overwritten by a newer entry.
    /// </summary>
    public static void SaveChunks(List<ChunkData> scheduledChunks)
    {
        DirAccess.MakeDirRecursiveAbsolute(SaveDir);

        // Group incoming chunks by region coordinate
        var byRegion = new Dictionary<Vector3I, List<ChunkData>>();
        foreach (var chunk in scheduledChunks)
        {
            Vector3I regionCoord = ChunkToRegion(chunk.ChunkCoordinate);
            if (!byRegion.TryGetValue(regionCoord, out var list))
            {
                list = new List<ChunkData>();
                byRegion[regionCoord] = list;
            }
            list.Add(chunk);
        }
        
        foreach (var (regionCoord, newChunks) in byRegion)
        {
            string filePath = RegionFilePath(regionCoord);

            // Load whatever is already on disk for this region
            var existing = LoadRegionFile(filePath);

            // Overwrite/add entries from the incoming list (new data wins)
            foreach (var chunk in newChunks)
                existing[chunk.ChunkCoordinate] = chunk;

            // Write the merged result back to disk
            WriteRegionFile(filePath, existing);
        }
    }

    /// <summary>
    /// Loads all chunks stored in the region file that contains <paramref name="chunkCoord"/>.
    /// Returns an empty list if the file does not exist yet.
    /// </summary>
    public static List<ChunkData> LoadRegion(Vector3I chunkCoord)
    {
        string filePath = RegionFilePath(ChunkToRegion(chunkCoord));
        return new List<ChunkData>(LoadRegionFile(filePath).Values);
    }

    /// <summary>
    /// Loads a single chunk from its region file. Returns null if not found.
    /// </summary>
    public static ChunkData LoadChunk(Vector3I chunkCoord)
    {
        string filePath = RegionFilePath(ChunkToRegion(chunkCoord));
        var region = LoadRegionFile(filePath);
        return region.TryGetValue(chunkCoord, out var data) ? data : null;
    }

    // -------------------------------------------------------------------------
    // Region helpers
    // -------------------------------------------------------------------------

    /// <summary>Maps any chunk coordinate to its region coordinate using floor division.</summary>
    public static Vector3I ChunkToRegion(Vector3I chunkCoord)
    {
        return new Vector3I(
            FloorDiv(chunkCoord.X, RegionSize),
            FloorDiv(chunkCoord.Y, RegionSize),
            FloorDiv(chunkCoord.Z, RegionSize)
        );
    }

    private static string RegionFilePath(Vector3I regionCoord)
    {
        string fileName = $"region_{regionCoord.X}_{regionCoord.Y}_{regionCoord.Z}.ethr";
        return ProjectSettings.GlobalizePath(SaveDir + fileName);
    }

    /// <summary>Floor division that handles negative coordinates correctly.</summary>
    private static int FloorDiv(int a, int b)
    {
        return (int)Math.Floor((double)a / b);
    }

    // -------------------------------------------------------------------------
    // Low-level file I/O
    // -------------------------------------------------------------------------

    /// <summary>
    /// Reads a region file and returns its chunks keyed by chunk coordinate.
    /// Returns an empty dictionary if the file does not exist.
    /// </summary>
    private static Dictionary<Vector3I, ChunkData> LoadRegionFile(string filePath)
    {
        var result = new Dictionary<Vector3I, ChunkData>();

        if (!File.Exists(filePath))
            return result;

        using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);

        uint   magic   = br.ReadUInt32();
        ushort version = br.ReadUInt16();

        if (magic != Magic)
            throw new InvalidDataException($"Not a valid ETHR region file: {filePath}");
        if (version != Version)
            throw new InvalidDataException($"Unsupported save version {version} in: {filePath}");

        int chunkCount = br.ReadInt32();
        for (int i = 0; i < chunkCount; i++)
        {
            ChunkData chunk = ReadChunk(br);
            result[chunk.ChunkCoordinate] = chunk;
        }

        return result;
    }

    /// <summary>Writes a full set of chunks to a region file, replacing it entirely.</summary>
    private static void WriteRegionFile(string filePath, Dictionary<Vector3I, ChunkData> chunks)
    {
        using var fs = File.Open(filePath, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        // Header
        bw.Write(Magic);
        bw.Write(Version);

        // Chunk count so the reader knows how many entries to expect
        bw.Write(chunks.Count);

        foreach (var chunk in chunks.Values)
            WriteChunk(bw, chunk);
    }

    // -------------------------------------------------------------------------
    // Chunk serialization
    // -------------------------------------------------------------------------

    private static void WriteChunk(BinaryWriter bw, ChunkData data)
    {
        // Chunk coordinate
        bw.Write(data.ChunkCoordinate.X);
        bw.Write(data.ChunkCoordinate.Y);
        bw.Write(data.ChunkCoordinate.Z);

        // Density array dimensions
        int dimX = data.Densities.GetLength(0);
        int dimY = data.Densities.GetLength(1);
        int dimZ = data.Densities.GetLength(2);
        bw.Write(dimX);
        bw.Write(dimY);
        bw.Write(dimZ);

        // Density values (float = 4 bytes each)
        for (int x = 0; x < dimX; x++)
        for (int y = 0; y < dimY; y++)
        for (int z = 0; z < dimZ; z++)
            bw.Write(data.Densities[x, y, z]);
    }

    private static ChunkData ReadChunk(BinaryReader br)
    {
        // Chunk coordinate
        var pos = new Vector3I(br.ReadInt32(), br.ReadInt32(), br.ReadInt32());

        // Density array dimensions
        int dimX = br.ReadInt32();
        int dimY = br.ReadInt32();
        int dimZ = br.ReadInt32();

        // Density values
        var densities = new float[dimX, dimY, dimZ];
        for (int x = 0; x < dimX; x++)
        for (int y = 0; y < dimY; y++)
        for (int z = 0; z < dimZ; z++)
            densities[x, y, z] = br.ReadSingle();

        return new ChunkData(pos, densities);
    }
}