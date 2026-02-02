using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using GC = Godot.Collections;

namespace EtherRealm3D.Scripts;

public partial class MeshManager : MeshInstance3D
{
	[Export] private Material _material;
	
	private List<Vector3> _vertices = new();  
	private List<Vector3> _normals = new();  
	private List<Color> _colors = new();
	
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

	public override void _Ready()
	{
		//GenerateMesh();
	}
	
	public override void _Process(double delta)
	{
	}
	
	public void GenerateMesh(Dictionary<Vector3, Color> data)
	{
		foreach (var d in data)
		{
			if (!HasNeighbor(Faces.Front, data, d.Key))
				AddFace(Faces.Front, d.Key, d.Value);
			if (!HasNeighbor(Faces.Back, data, d.Key))
				AddFace(Faces.Back, d.Key, d.Value);
			if (!HasNeighbor(Faces.Left, data, d.Key))
				AddFace(Faces.Left, d.Key, d.Value);
			if (!HasNeighbor(Faces.Right, data, d.Key))
				AddFace(Faces.Right, d.Key, d.Value);
			if (!HasNeighbor(Faces.Top, data, d.Key))
				AddFace(Faces.Top, d.Key, d.Value);
			if (!HasNeighbor(Faces.Bottom, data, d.Key))
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
		Mesh = arrayMesh;
		Mesh.SurfaceSetMaterial(0, _material);
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
				_colors.Add(_faceColors[face]);
			}
		}
	}
	
	private bool HasNeighbor(Faces face, Dictionary<Vector3, Color> data, Vector3 position)
	{
		var neighborPos = position + _faceNormals[face];
		return data.ContainsKey(neighborPos);
	}
}
