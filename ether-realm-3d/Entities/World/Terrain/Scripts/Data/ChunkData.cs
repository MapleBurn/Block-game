using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts.Data;

/// <summary>
/// Immutable data transfer object holding a chunk's density field and configuration metadata.
/// </summary>
public class ChunkData
{
    public Vector3I ChunkCoordinate { get; }
    public float[,,] Densities { get; }
    public float IsoLevel { get; }
    public float VoxelSize { get; }
    public int CellsPerAxis { get; }

    public ChunkData(Vector3I coord, float[,,] densities, float isoLevel, float voxelSize, int cellsPerAxis)
    {
        ChunkCoordinate = coord;
        Densities = densities;
        IsoLevel = isoLevel;
        VoxelSize = voxelSize;
        CellsPerAxis = cellsPerAxis;
    }
}