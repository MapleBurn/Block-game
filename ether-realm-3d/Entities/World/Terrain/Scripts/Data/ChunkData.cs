using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts.Data;

public class ChunkData
{
    public Vector3I ChunkCoordinate { get; }
    public float[,,] Densities { get; }

    public ChunkData(Vector3I coord, float[,,] densities)
    {
        ChunkCoordinate = coord;
        Densities = densities;
    }
}