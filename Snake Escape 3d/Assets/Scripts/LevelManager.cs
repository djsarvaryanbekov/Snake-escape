// --- REPLACE ENTIRE FILE: LevelManager.cs ---

using System;
using System.Collections.Generic;
using UnityEngine;

public class LevelManager : MonoBehaviour
{
	[SerializeField] private int levelIndexToLoad = 0;

	[SerializeField] private List<Level_SO> allLevels;

	[SerializeField] private GameObject snakeVisualizerPrefab;
	[SerializeField] private float cellSize = 1f;

	[SerializeField] private GameObject gridPosition;

	private List<GameObject> spawnedVisualizers = new List<GameObject>();
	private GameManager gameManager;

	public event EventHandler OnLevelStarted;

	private void Start()
	{
		gameManager = GameManager.Instance;
		gameManager.ReloadLevel += OnReloadLevel;

		LoadLevelByIndex(levelIndexToLoad);
	}

	private void OnReloadLevel(object sender, System.EventArgs e)
	{
		LoadLevelByIndex(levelIndexToLoad);
	}

	public void LoadLevelByIndex(int index)
	{
		if (index < 0 || index >= allLevels.Count)
		{
			Debug.LogError($"Error index {index}. All levels count {allLevels.Count}.");
			return;
		}

		levelIndexToLoad = index;
		ClearCurrentLevel();

		Level_SO levelData = allLevels[index];
		Vector3 originPosition = gridPosition.transform.position;

		// 1. Create the grid (it initializes with EmptyCells automatically).
		gameManager.grid = new Grid(levelData.width, levelData.height, cellSize, originPosition);

		// 2. Place Wall objects.
		foreach (var wallPos in levelData.wallPositions)
		{
			gameManager.grid.AddObject(wallPos.x, wallPos.y, new Wall());
		}

		// 3. Create and place Exit objects.
		foreach (var exitData in levelData.exits)
		{
			gameManager.grid.AddObject(exitData.position.x, exitData.position.y, new Exit(exitData));
		}

		// 4. Create and place Fruit objects.
		foreach (var fruitData in levelData.fruits)
		{
			gameManager.grid.AddObject(fruitData.position.x, fruitData.position.y, new Fruit(fruitData));
		}

		// 5. Create and place Snake logic and visualizers.
		foreach (var snakeData in levelData.snakes)
		{
			Snake logicalSnake = new Snake();

			GameObject visualizerGO = Instantiate(snakeVisualizerPrefab);
			visualizerGO.name = $"SnakeVisualizer_{snakeData.color}";
			SnakeVisualizer snakeVisualizer = visualizerGO.GetComponent<SnakeVisualizer>();

			if (snakeVisualizer == null)
			{
				Debug.LogError($"On prefab '{snakeVisualizerPrefab.name}' missing component SnakeVisualizer!");
				Destroy(visualizerGO);
				continue;
			}

			logicalSnake.SetVisualizer(snakeVisualizer);
			logicalSnake.Initialize(snakeData);
			snakeVisualizer.Initialize(logicalSnake);

			gameManager.snakesOnLevel.Add(logicalSnake);
			spawnedVisualizers.Add(visualizerGO);
		}

		// 6. Create and place Box objects.
		foreach (var boxPosition in levelData.boxPositions)
		{
			gameManager.grid.AddObject(boxPosition.x, boxPosition.y, new Box());
		}

		// 7. Create and place IceCube objects.
		foreach (var iceCubePosition in levelData.iceCubePositions)
		{
			gameManager.grid.AddObject(iceCubePosition.x, iceCubePosition.y, new IceCube());
		}

		// 8. Create and place Hole objects.
		foreach (var holePosition in levelData.holePositions)
		{
			gameManager.grid.AddObject(holePosition.x, holePosition.y, new Hole());
		}

		// 9. Create and place Pressure Plate objects.
		foreach (var plateData in levelData.pressurePlates)
		{
			var plate = new PressurePlate(plateData);
			gameManager.grid.AddObject(plateData.position.x, plateData.position.y, plate);
			gameManager.RegisterPlate(plate, plateData);
		}
		foreach (var gateData in levelData.liftGates)
		{
			var gate = new LiftGate(gateData);
			gameManager.grid.AddObject(gateData.position.x, gateData.position.y, gate);
			gameManager.RegisterLiftGate(gate, gateData);
		}

		// 10b. Create and place LASER GATES (Energy)
		foreach (var laserData in levelData.laserGates)
		{
			var laser = new LaserGate(laserData);
			gameManager.grid.AddObject(laserData.position.x, laserData.position.y, laser);
			gameManager.RegisterLaserGate(laser, laserData);
		}

		// 11. Create and place Portal objects.
		foreach (var portalData in levelData.portals)
		{
			var portal = new Portal(portalData);
			gameManager.grid.AddObject(portalData.position.x, portalData.position.y, portal);
			gameManager.RegisterPortal(portal);
		}

		// Trigger logic in GameManager and notify listeners.
		gameManager.TriggerLevelLoad(levelData);
		OnLevelStarted?.Invoke(this, EventArgs.Empty);
	}

	private void ClearCurrentLevel()
	{
		// Destroy visual representations.
		foreach (var visualizer in spawnedVisualizers)
		{
			if (visualizer != null) Destroy(visualizer);
		}
		spawnedVisualizers.Clear();

		// Tell GameManager to reset its logical collections.
		if (gameManager != null)
		{
			gameManager.ClearLevelData();
		}
	}

	private void OnDestroy()
	{
		if (gameManager != null)
		{
			gameManager.ReloadLevel -= OnReloadLevel;
		}
	}
}