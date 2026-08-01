using System.IO;
using System.Text;
using EtherRealm3D.Entities.World.Terrain.Scripts.Data;
using Godot;
using FileAccess = System.IO.FileAccess;

namespace EtherRealm3D.Entities.World.Terrain.Scripts;

public static class RegionFileManager
{
    public const int RegionSize = 8;

    private const string SaveDir = "game_data/worlds/world1/regions/";
    private const uint   Magic   = 0x45544852; // "ETHR"
    private const ushort Version = 1;
    
    private const int TableOffset = 6;
    private const int EntrySize   = 12;
    
    private static int FloorDiv(int a, int b) =>
        a / b - (a % b != 0 && (a ^ b) < 0 ? 1 : 0);
    
    private static int PosMod(int a, int b) => ((a % b) + b) % b;

    private static Vector3I ChunkToRegion(Vector3I c) => new(
        FloorDiv(c.X, RegionSize),
        FloorDiv(c.Y, RegionSize),
        FloorDiv(c.Z, RegionSize));

    private static Vector3I ChunkToLocal(Vector3I c) => new(
        PosMod(c.X, RegionSize),
        PosMod(c.Y, RegionSize),
        PosMod(c.Z, RegionSize));

    private static int TableIndex(Vector3I local) =>
        local.X * RegionSize * RegionSize + local.Y * RegionSize + local.Z;

    private static long TableEntryPos(int index) => TableOffset + index * EntrySize;

    private static string RegionPath(Vector3I region) =>
        $"{SaveDir}r.{region.X}.{region.Y}.{region.Z}.region";

    private static void InitRegionFile(FileStream fs)
    {
        using var w = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: true);
        w.Write(Magic);
        w.Write(Version);
        // All 512 slots: offset = -1 (absent), length = 0
        for (int i = 0; i < RegionSize * RegionSize * RegionSize; i++)
        {
            w.Write(-1L);
            w.Write(0);
        }
    }

    public static void SaveChunk(ChunkData data)
    {
        Directory.CreateDirectory(SaveDir);

        var region = ChunkToRegion(data.ChunkCoordinate);
        var local  = ChunkToLocal(data.ChunkCoordinate);
        int index = TableIndex(local);
        var path = RegionPath(region);

        // Serialize chunk payload into a byte array first so we know its size
        byte[] payload;
        using (var ms = new MemoryStream())
        using (var mw = new BinaryWriter(ms))
        {
            mw.Write(data.ChunkCoordinate.X);
            mw.Write(data.ChunkCoordinate.Y);
            mw.Write(data.ChunkCoordinate.Z);

            mw.Write(data.Densities.GetLength(0));
            mw.Write(data.Densities.GetLength(1));
            mw.Write(data.Densities.GetLength(2));
            for (int x = 0; x < data.Densities.GetLength(0); x++)
            for (int y = 0; y < data.Densities.GetLength(1); y++)
            for (int z = 0; z < data.Densities.GetLength(2); z++)
                mw.Write(data.Densities[x, y, z]);

            payload = ms.ToArray();
        }

        bool isNew = !File.Exists(path);
        using var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        using var w  = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: true);
        using var r  = new BinaryReader(fs, Encoding.UTF8, leaveOpen: true);

        if (isNew)
            InitRegionFile(fs);

        // Read existing table entry for this slot
        long entryPos = TableEntryPos(index);
        fs.Seek(entryPos, SeekOrigin.Begin);
        long existingOffset = r.ReadInt64();
        int  existingLength = r.ReadInt32();

        // Reuse the existing slot if the new payload fits; otherwise append
        long writeOffset = (existingOffset != -1 && payload.Length <= existingLength)
            ? existingOffset
            : fs.Seek(0, SeekOrigin.End);

        // Write payload
        fs.Seek(writeOffset, SeekOrigin.Begin);
        fs.Write(payload, 0, payload.Length);

        // Update table entry
        fs.Seek(entryPos, SeekOrigin.Begin);
        w.Write(writeOffset);
        w.Write(payload.Length);
    }

    public static ChunkData LoadChunk(Vector3I chunkCoordinate)
    {
        var region = ChunkToRegion(chunkCoordinate);
        var local  = ChunkToLocal(chunkCoordinate);
        int index  = TableIndex(local);
        var path   = RegionPath(region);

        if (!File.Exists(path))
            return null;

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var r  = new BinaryReader(fs);

        // Validate region file header
        uint   magic   = r.ReadUInt32();
        ushort version = r.ReadUInt16();
        if (magic != Magic || version != Version)
            throw new InvalidDataException($"Invalid region file: {path}");

        // Read table entry
        fs.Seek(TableEntryPos(index), SeekOrigin.Begin);
        long offset = r.ReadInt64();

        if (offset == -1)
            return null; // chunk absent in this region

        // Read chunk payload
        fs.Seek(offset, SeekOrigin.Begin);

        var coord = new Vector3I(r.ReadInt32(), r.ReadInt32(), r.ReadInt32());

        int sizeX = r.ReadInt32();
        int sizeY = r.ReadInt32();
        int sizeZ = r.ReadInt32();

        var densities = new float[sizeX, sizeY, sizeZ];
        for (int x = 0; x < sizeX; x++)
        for (int y = 0; y < sizeY; y++)
        for (int z = 0; z < sizeZ; z++)
            densities[x, y, z] = r.ReadSingle();

        return new ChunkData(coord, densities);
    }
}
