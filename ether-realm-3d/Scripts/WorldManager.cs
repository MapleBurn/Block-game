using Godot;
using System;
using SCG = System.Collections.Generic;
using System.Linq;
using Godot.Collections;
using Array = System.Array;

namespace EtherRealm3D.Scripts;

public partial class WorldManager : Node3D
{
	[Export] private MultiMeshInstance3D _multiMesh;
	[Export] private MeshManager _mesh;
	
	private Vector3 _worldSize = new Vector3(256, 128, 256);
	private readonly SCG.Dictionary<Vector3, Color> _data = new();

	[Export] private Array<Color> _colors = new();
	
	public override void _Ready()
	{
		GenerateWorld();
	}
	
	public override void _Process(double delta)
	{
	}
	
	private void GenerateWorld()
	{
		var noise = new FastNoiseLite();
		noise.NoiseType = FastNoiseLite.NoiseTypeEnum.Simplex;
		
		for (int x = 0; x < _worldSize.X; x++)
		{
			for (int y = 0; y < _worldSize.Y; y++)
			{
				for (int z = 0; z < _worldSize.Z; z++)
				{
					var random = noise.GetNoise3D(x, y, z);
					var cutoff = y / _worldSize.Y * 0.25f; // s rostoucí výškou je menší pravděpodobnost výskytu bloku
					if (random > cutoff)
					{
						/*var newBlock = _blockScene.Instantiate<CsgBox3D>();
						newBlock.Position = new Vector3(x, y, z);
						AddChild(newBlock);*/

						_data.Add(new Vector3(x, y, z), _colors.PickRandom());
					}
				}
			}
		}
		
		//staré použití MultiMesh - nevýhoda je, že nelze použít kolize
		/*_multiMesh.Multimesh.InstanceCount = _data.Count;
		for (int i = 0; i < _multiMesh.Multimesh.InstanceCount; i++)
		{
			_multiMesh.Multimesh.SetInstanceTransform(i, new Transform3D(Basis.Identity, _data[i]));
			_multiMesh.Multimesh.SetInstanceColor(i, _colors.PickRandom());
		}*/
		
		//vytvoří mesh pro všechny bloky najednou
		_mesh.GenerateMesh(_data);
	}
}
