using Godot;

namespace EtherRealm3D.Scripts;

public partial class WorldManager : Node3D
{
	[Export] private ChunkManager _chunkManager;
	
	public override void _Ready()
	{
		//_chunkManager.GenerateChunks(new Vector3(0,0,0));
	}
	
	public override void _Process(double delta)
	{
	}
}
