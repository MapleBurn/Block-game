using System.Diagnostics.Tracing;
using Godot;

namespace EtherRealm3D.Scripts.UI;

public partial class WorldSaveFile : Control
{
	// children
	[Export] private Label _nameLabel;
	
	public void Configure(string worldName)
	{
		_nameLabel.Text = worldName;
	}
	
	[Signal] public delegate void FilePressedEventHandler();
	private void OnFilePressed()
	{
		GD.Print("[Save File] I was pressed!");
		EmitSignal(SignalName.FilePressed);
	}
}