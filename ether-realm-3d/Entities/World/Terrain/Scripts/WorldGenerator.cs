using EtherRealm3D.Entities.Character;
using Godot;

namespace EtherRealm3D.Entities.World.Terrain.Scripts;

public partial class WorldGenerator : Node3D
{
    // Terrain properties
    public int CellsPerAxis = 32;  
    public float VoxelSize = 1.0f;  
    public float IsoLevel = 0.0f;  
    public int NoiseSeed = 0;  
    public float NoiseFrequency = 0.025f;
    public float TerrainAmplitude = 15.0f;

    private FastNoiseLite _noise;  
    private ChunkManager _chunkManager;
    [Export] public Player Player;
    
    public int LoadRadius = 4;
  
    public override void _Ready()  
    {  
        _noise = new FastNoiseLite  
        {  
            Seed = NoiseSeed,  
            NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin,  
            Frequency = NoiseFrequency  
        };
        
        _chunkManager = new ChunkManager();
        AddChild(_chunkManager);
        _chunkManager.Initialize(CellsPerAxis, VoxelSize, IsoLevel, _noise, TerrainAmplitude);
    }  
  
    public override void _Process(double delta)  
    {  
        _chunkManager.UpdateTerrain(Player.GlobalPosition, LoadRadius); 
    }
}