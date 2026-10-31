using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts.Chunks;

public enum ChunkContent
{
    Mixed, // Surface passes through the chunk; densities are stored
    Air,   // Entirely above the surface; no densities stored
    Solid  // Entirely below the surface; no densities stored
}

public class ChunkData
{
    public Vector3I ChunkCoordinate { get; }
    public ChunkContent Content { get; }

    /// <summary>Density samples per axis (cells per axis + 1).</summary>
    public int Size { get; }

    /// <summary>
    /// Flat density grid indexed by <see cref="Index"/>. Null unless Content is Mixed.
    /// Negative values are inside the terrain, positive values are air.
    /// </summary>
    public float[] Densities { get; }

    public ChunkData(Vector3I coord, int size, float[] densities)
    {
        ChunkCoordinate = coord;
        Size = size;
        Densities = densities;
        Content = ChunkContent.Mixed;
    }

    private ChunkData(Vector3I coord, int size, ChunkContent content)
    {
        ChunkCoordinate = coord;
        Size = size;
        Content = content;
    }

    public static ChunkData Uniform(Vector3I coord, int size, ChunkContent content) => new(coord, size, content);

    public int Index(int x, int y, int z) => (x * Size + y) * Size + z;
}
