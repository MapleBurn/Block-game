using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts.Data;

/// <summary>  
/// Plain data produced on a background thread.  
/// Applied to the scene on the main thread.  
/// </summary>  
public class ChunkMeshData  
{  
    public Vector3I ChunkCoordinate;  
    public Vector3[] Vertices;  
    public Vector3[] Normals;  
    public int[] Indices;  
  
    public bool IsEmpty => Vertices == null || Vertices.Length == 0;  
}