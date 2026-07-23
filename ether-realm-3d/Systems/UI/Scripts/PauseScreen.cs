using Godot;

namespace EtherRealm3D.Systems.UI.Scripts;

public partial class PauseScreen : Control
{
	public const string MainMenuScenePath = "res://Systems/UI/Scenes/main_menu.tscn";
	
	public override void _Ready()
	{
		Visible = false;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event.IsActionPressed("pause"))
		{
			Visible = !Visible;
			if (!Visible)
				Input.SetMouseMode(Input.MouseModeEnum.Captured);
			else
				Input.SetMouseMode(Input.MouseModeEnum.Visible);
			//GetTree().Paused = Visible;
		}
	}

	public override void _Process(double delta)
	{
	}
	
	public void BtnResumePressed()
	{
		Input.SetMouseMode(Input.MouseModeEnum.Captured);
		Visible = false;
	}
	
	public void BtnExitPressed()
	{
		GetTree().ChangeSceneToFile(MainMenuScenePath);
	}
}
