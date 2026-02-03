using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using GC = Godot.Collections;

namespace EtherRealm3D.Scripts;

public partial class Chunk : StaticBody3D
{
	[Export] private Material _material;
	[Export] private CollisionShape3D _collider;
	[Export] private MeshInstance3D _meshInstance;
	
	private readonly List<Vector3> _vertices = new();  
	private readonly List<Vector3> _normals = new();  
	private readonly List<Color> _colors = new();
	
	private readonly Dictionary<Vector3, Color> _blocks = new();

	#region Cube data
	// pozice vertexů krychle o velikosti 1x1x1, centrované v počátku souřadnic
	private Vector3[] _cubeVertices = new Vector3[]
	{
		new Vector3(-0.5f, -0.5f,  0.5f), // 0
		new Vector3( 0.5f, -0.5f,  0.5f), // 1
		new Vector3( 0.5f, -0.5f, -0.5f), // 2
		new Vector3(-0.5f, -0.5f, -0.5f), // 3
		new Vector3(-0.5f,  0.5f,  0.5f), // 4
		new Vector3( 0.5f,  0.5f,  0.5f), // 5
		new Vector3( 0.5f,  0.5f, -0.5f), // 6
		new Vector3(-0.5f,  0.5f, -0.5f)  // 7
	};
	
	private enum Faces
	{
		Front,
		Back,
		Left,
		Right,
		Top,
		Bottom
	}
	
	// Dictionary obsahující pozice trojúhelníků pro každou stranu krychle, souřadnice jsou indexy vertexů v counter-clockwise pořadí
	private Dictionary<Faces, Vector3[]> _faceTriangles = new()
	{
		{ Faces.Front, new[] { new Vector3(0, 4, 5), new Vector3(0, 5, 1) } },
		{ Faces.Back, new[] { new Vector3(2, 6, 7), new Vector3(2, 7, 3) } },
		{ Faces.Left, new[] { new Vector3(3, 7, 4), new Vector3(3, 4, 0) } },
		{ Faces.Right, new[] { new Vector3(1, 5, 6), new Vector3(1, 6, 2) } },
		{ Faces.Top, new[] { new Vector3(4, 7, 6), new Vector3(4, 6, 5) } },
		{ Faces.Bottom, new[] { new Vector3(3, 0, 1), new Vector3(3, 1, 2) } }
	};
	
	// Dictionary obsahující normálové vektory pro každou stranu krychle
	private Dictionary<Faces, Vector3> _faceNormals = new()
	{
		{ Faces.Front, new Vector3(0, 0, 1) },
		{ Faces.Back, new Vector3(0, 0, -1) },
		{ Faces.Left, new Vector3(-1, 0, 0) },
		{ Faces.Right, new Vector3(1, 0, 0) },
		{ Faces.Top, new Vector3(0, 1, 0) },
		{ Faces.Bottom, new Vector3(0, -1, 0) }
	};
	
	private Dictionary<Faces, Color> _faceColors = new()
	{
		{ Faces.Front, Colors.Orange },
		{ Faces.Back, Colors.Purple },
		{ Faces.Left, Colors.Blue },
		{ Faces.Right, Colors.Yellow },
		{ Faces.Top, Colors.Green },
		{ Faces.Bottom, Colors.Red }
	};
	#endregion
	public override void _Ready()
	{
		
	}
	
	public override void _Process(double delta)
	{
	}

	public void GenerateData(int size, int maxHeight, Noise noise, GC.Array<Color> colors)
	{
		for (int x = 0; x < size; x++)
		{
			for (int z = 0; z < size; z++)
			{
				var globalPos = new Vector2(x + Position.X, z + Position.Z);
				
				// procedurální terrain gen
				var random = ((noise.GetNoise2D(globalPos.X, globalPos.Y) + 0.5 * noise.GetNoise2D(globalPos.X * 2, globalPos.Y * 2) + 0.25) * noise.GetNoise2D(globalPos.X * 4, globalPos.Y * 4) / 1.75f + 1) / 2f;
				var height = maxHeight * Mathf.Pow(random, 2);

				if (height < Position.Y)
					continue;
				
				var localHeight = height - Position.Y;
				for (int y = 0; y < Math.Min(localHeight, size); y++)
				{
					_blocks[new Vector3(x, y, z)] = colors[y % colors.Count];
				}
			}
		}
	}
	
	public void GenerateMesh()
	{
		if (_blocks.Count == 0)
			return;
		
		foreach (var d in _blocks)
		{
			if (!HasNeighbor(Faces.Front, _blocks, d.Key))
				AddFace(Faces.Front, d.Key, d.Value);
			if (!HasNeighbor(Faces.Back, _blocks, d.Key))
				AddFace(Faces.Back, d.Key, d.Value);
			if (!HasNeighbor(Faces.Left, _blocks, d.Key))
				AddFace(Faces.Left, d.Key, d.Value);
			if (!HasNeighbor(Faces.Right, _blocks, d.Key))
				AddFace(Faces.Right, d.Key, d.Value);
			if (!HasNeighbor(Faces.Top, _blocks, d.Key))
				AddFace(Faces.Top, d.Key, d.Value);
			if (!HasNeighbor(Faces.Bottom, _blocks, d.Key))
				AddFace(Faces.Bottom, d.Key, d.Value);
		}
		
		CommitMesh();
	}

	private void CommitMesh()
	{
		GC.Array nestedArray = [];
		nestedArray.Resize((int)Mesh.ArrayType.Max);
		nestedArray[(int)Mesh.ArrayType.Vertex] = _vertices.ToArray();  
		nestedArray[(int)Mesh.ArrayType.Normal] = _normals.ToArray();  
		nestedArray[(int)Mesh.ArrayType.Color] = _colors.ToArray();
		
		var arrayMesh = new ArrayMesh();
		arrayMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, nestedArray);
		_meshInstance.Mesh = arrayMesh;
		_meshInstance.Mesh.SurfaceSetMaterial(0, _material);
		_collider.Shape = _meshInstance.Mesh.CreateTrimeshShape();
	}
	
	private void AddFace(Faces face, Vector3 position, Color color)
	{
		var triangles = _faceTriangles[face];
		foreach (var triangle in triangles)
		{
			for (int i = 0; i < 3; i++)
			{
				_vertices.Add(_cubeVertices[(int)triangle[i]] + position);  
				_normals.Add(_faceNormals[face]);  
				_colors.Add(color);
			}
		}
	}
	
	private bool HasNeighbor(Faces face, Dictionary<Vector3, Color> data, Vector3 position)
	{
		var neighborPos = position + _faceNormals[face];
		return data.ContainsKey(neighborPos);
	}
}
