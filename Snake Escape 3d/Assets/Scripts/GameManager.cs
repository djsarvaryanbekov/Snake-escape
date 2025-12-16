// --- REPLACE ENTIRE FILE: GameManager.cs ---

using System;
using System.Collections.Generic;
using UnityEngine;

// An enum to clearly identify which end of a snake is being referred to.
public enum SnakeEnd
{
	Head,
	Tail
}

// An enum to define the possible colors, used for game logic (special abilities, matching exits, etc.).
public enum ColorType
{
	Red,
	Green,
	Blue,
	Yellow,
}


public enum PlateColor
{
	Yellow,
	Purple,
	Orange
}

/// <summary>
/// The central singleton for managing game state, rules, and events.
/// </summary>
public class GameManager : MonoBehaviour
{
	// --- SINGLETON PATTERN ---
	public static GameManager Instance { get; private set; }

	// --- EVENTS ---
	public event EventHandler LevelWin;
	public event EventHandler ReloadLevel;
	public event Action<Level_SO> OnLevelLoaded;
	public event Action<FruitData> OnFruitEaten;
	public event Action<FruitData> OnFruitSpawned;
	public event Action<Vector2Int> OnExitRemoved;
	public event Action<Vector2Int, Vector2Int> OnBoxMoved;
	public event Action<Vector2Int, Vector2Int> OnIceCubeMoved;
	public event Action<Vector2Int, Vector2Int> OnHoleFilled;
	public event Action<PressurePlateData, bool> OnPlateStateChanged; // true = active, false = inactive
	public event Action<LaserGateData, bool> OnGateStateChanged; // true = open, false = close

	// --- PUBLIC STATE ---
	[HideInInspector] public Grid grid { get; set; }
	[HideInInspector] public List<Snake> snakesOnLevel { get; private set; } = new List<Snake>();

	private Dictionary<PlateColor, List<PressurePlate>> platesByColor = new Dictionary<PlateColor, List<PressurePlate>>();
	private Dictionary<PlateColor, List<LaserGate>> gatesByColor = new Dictionary<PlateColor, List<LaserGate>>();


	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(this.gameObject);
		}
		else
		{
			Instance = this;
		}
	}

	public void TriggerLevelLoad(Level_SO data)
	{
		OnLevelLoaded?.Invoke(data);
		// Perform an initial check after the level is fully set up.
		UpdateAllPlateStates();
	}

	public Snake GetSnakeAtPosition(Vector2Int gridPosition, out SnakeEnd partClicked)
	{
		foreach (var snake in snakesOnLevel)
		{
			if (gridPosition == snake.GetHeadPosition())
			{
				partClicked = SnakeEnd.Head;
				return snake;
			}
			if (gridPosition == snake.GetTailPosition())
			{
				partClicked = SnakeEnd.Tail;
				return snake;
			}
		}
		partClicked = default;
		return null;
	}

	public void MoveBox(Vector2Int from, Vector2Int to)
	{
		IGridObject boxObject = grid.GetObject(from);
		if (boxObject is Box)
		{
			grid.SetObject(from.x, from.y, new EmptyCell());
			grid.SetObject(to.x, to.y, boxObject);
			OnBoxMoved?.Invoke(from, to);
			UpdateAllPlateStates();
		}
	}

	public void MoveIceCube(Vector2Int from, Vector2Int to)
	{
		IGridObject iceCube = grid.GetObject(from);
		if (iceCube is IceCube)
		{
			grid.SetObject(from.x, from.y, new EmptyCell());
			grid.SetObject(to.x, to.y, iceCube);
			OnIceCubeMoved?.Invoke(from, to);
			UpdateAllPlateStates();
		}
	}

	public void SnakeHasExited(Snake exitedSnake, ExitData exitData)
	{
		if (snakesOnLevel.Contains(exitedSnake))
		{
			Vector2Int exitPosition = exitData.position;
			OnExitRemoved?.Invoke(exitPosition);
			exitedSnake.RemoveFromGame();
			snakesOnLevel.Remove(exitedSnake);
			UpdateAllPlateStates();

			if (snakesOnLevel.Count > 0)
			{
				var remainingColors = new List<ColorType>();
				foreach (var snake in snakesOnLevel) remainingColors.Add(snake.Color);
				var newFruitData = new FruitData { position = exitPosition, colors = remainingColors };
				grid.SetObject(exitPosition.x, exitPosition.y, new Fruit(newFruitData));
				OnFruitSpawned?.Invoke(newFruitData);
			}
			else
			{
				grid.SetObject(exitPosition.x, exitPosition.y, new EmptyCell());
			}
			CheckForWinCondition();
		}
	}

	public void ReportFruitEaten(FruitData eatenFruitData)
	{
		if (eatenFruitData != null)
		{
			grid.SetObject(eatenFruitData.position.x, eatenFruitData.position.y, new EmptyCell());
			OnFruitEaten?.Invoke(eatenFruitData);
		}
	}

	public void FillHole(Vector2Int hole, Vector2Int filler)
	{
		if (filler != Vector2Int.zero && hole != Vector2Int.zero)
		{
			grid.SetObject(filler.x, filler.y, new EmptyCell());
			grid.SetObject(hole.x, hole.y, new EmptyCell());
			OnHoleFilled?.Invoke(hole, filler);
			UpdateAllPlateStates();
		}
	}

	public void RestartLevel() => ReloadLevel?.Invoke(this, EventArgs.Empty);
	private void CheckForWinCondition()
	{
		if (snakesOnLevel.Count == 0)
		{
			Debug.Log("LEVEL COMPLETE!");
			LevelWin?.Invoke(this, EventArgs.Empty);
		}
	}

	public void ClearLevelData()
	{
		snakesOnLevel.Clear();
		platesByColor.Clear();
		gatesByColor.Clear();
	}

	public void ReportSnakeMoved() => UpdateAllPlateStates();

	public void RegisterPlate(PressurePlate plate, PressurePlateData data)
	{
		if (!platesByColor.ContainsKey(data.color))
			platesByColor[data.color] = new List<PressurePlate>();
		platesByColor[data.color].Add(plate);
	}

	public void RegisterGate(LaserGate gate, LaserGateData data)
	{
		if (!gatesByColor.ContainsKey(data.color))
			gatesByColor[data.color] = new List<LaserGate>();
		gatesByColor[data.color].Add(gate);
	}

	public void ReportPlateStateChange(PressurePlateData data, bool isActive)
	{
		OnPlateStateChanged?.Invoke(data, isActive);
		CheckPlateSystem(data.color);
	}

	private void CheckPlateSystem(PlateColor color)
	{
		if (!platesByColor.ContainsKey(color)) return;

		bool allPlatesActive = true;
		foreach (var plate in platesByColor[color])
		{
			if (!plate.IsActive)
			{
				allPlatesActive = false;
				break;
			}
		}

		if (gatesByColor.ContainsKey(color))
		{
			foreach (var gate in gatesByColor[color])
			{
				bool isGateBlockedBySnake = false;
				if (!allPlatesActive)
				{
					foreach (var snake in snakesOnLevel)
					{
						foreach (var segment in snake.Body)
						{
							if (segment == gate.GetData().position)
							{
								isGateBlockedBySnake = true;
								break;
							}
						}
						if (isGateBlockedBySnake) break;
					}
				}

				bool stateChanged = false;
				if (allPlatesActive && !gate.IsOpen)
				{
					gate.Open();
					stateChanged = true;
				}
				else if (!allPlatesActive && gate.IsOpen && !isGateBlockedBySnake)
				{
					gate.Close();
					stateChanged = true;
				}

				if (stateChanged)
				{
					OnGateStateChanged?.Invoke(gate.GetData(), gate.IsOpen);
				}
			}
		}
	}

	private void UpdateAllPlateStates()
	{
		if (platesByColor.Count == .0) return;

		foreach (var color in platesByColor.Keys)
		{
			foreach (var plate in platesByColor[color])
			{
				Vector2Int platePos = plate.GetData().position;
				bool isNowActive = false;

				foreach (var snake in snakesOnLevel)
				{
					foreach (var segment in snake.Body)
					{
						if (segment == platePos)
						{
							isNowActive = true;
							break;
						}
					}
					if (isNowActive) break;
				}

				if (!isNowActive)
				{
					var objOnGrid = grid.GetObject(platePos);
					if (objOnGrid is Box || objOnGrid is IceCube)
					{
						isNowActive = true;
					}
				}

				if (isNowActive && !plate.IsActive)
				{
					plate.Activate();
				}
				else if (!isNowActive && plate.IsActive)
				{
					plate.Deactivate();
				}
			}
		}
	}
}