using Godot;

namespace EtherRealm3D.Scripts.Terrain;

public partial class TerrainWorld : Node3D
{
    // Properties
    public Vector3I WorldSizeInChunks = new Vector3I(5, 5, 5);  
    public int CellsPerAxis = 32;  
    public float VoxelSize = 1.0f;  
    public float IsoLevel = 0.0f;  
    public int NoiseSeed = 0;  
    public float NoiseFrequency = 0.025f;  
    
    private FastNoiseLite _noise;  
  
    public override void _Ready()  
    {  
        CreateNoise();  
        GenerateWorld();  
    }  
  
    private void CreateNoise()  
    {  
        _noise = new FastNoiseLite  
        {  
            Seed = NoiseSeed,  
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,  
            Frequency = NoiseFrequency  
        };  
    }  
  
    private void GenerateWorld()  
    {
        for (int x = 0; x < WorldSizeInChunks.X; x++)  
        {  
            for (int y = 0; y < WorldSizeInChunks.Y; y++)  
            {  
                for (int z = 0; z < WorldSizeInChunks.Z; z++)  
                {  
                    CreateChunk(new Vector3I(x, y, z));  
                }  
            }  
        }  
    }  
  
    private void CreateChunk(Vector3I chunkCoordinate)  
    {  
        TerrainChunk chunk = new TerrainChunk();  
  
        chunk.Configure(chunkCoordinate, CellsPerAxis, VoxelSize, IsoLevel, _noise);  
  
        AddChild(chunk);  
        chunk.Generate();  
    }
}