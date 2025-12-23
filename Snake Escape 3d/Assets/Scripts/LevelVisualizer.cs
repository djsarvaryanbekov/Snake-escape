// --- LevelVisualizer.cs ---

using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using System; // A popular third-party library for creating smooth animations (tweens).

/// <summary>
/// Manages the visual representation of all non-snake grid objects (Walls, Exits, Fruits, Boxes).
/// This class is a "listener". It subscribes to events from the GameManager and reacts by
/// creating, destroying, or animating GameObjects in the scene. It keeps the game's visual
/// state synchronized with its logical state.
/// </summary>
public class LevelVisualizer : MonoBehaviour
{
	[Header("Prefabs")]
	[SerializeField] private GameObject wallPrefab;
	[SerializeField] private GameObject exitPrefab;
	[SerializeField] private GameObject fruitPrefab;
	[SerializeField] private GameObject boxPrefab;
	[SerializeField] private GameObject iceCubePrefab;
	[SerializeField] private GameObject holePrefab;
	[SerializeField] private GameObject portalPrefab;
	//[SerializeField] private GameObject pressurePlatePrefab; // Optional: for visual plates
	[SerializeField] private GameObject liftGatePrefab; // Renamed
	[SerializeField] private GameObject laserGatePrefab; // New
	// In the [Header("Prefabs")] section
	[SerializeField] private GameObject pressurePlatePrefab;

	// In the [Header("Settings")] section
	[SerializeField] private float plateMoveDuration = 0.1f;


	[Header("Settings")]
	[Tooltip("A parent Transform in the hierarchy to keep spawned level objects organized.")]
	[SerializeField] private Transform levelObjectsParent;
	[Tooltip("The duration of the box push animation in seconds.")]
	[SerializeField] private float boxMoveDuration = 0.15f;
	[SerializeField] private float iceCubeMoveDuration = 0.15f;




	// --- Private State ---

	// Dictionaries are used to keep track of the GameObjects we've spawned for each logical object.
	// The key is the object's grid position, and the value is the GameObject itself.
	// This allows us to quickly find and destroy a specific fruit or exit's GameObject when it's removed.
	private Dictionary<Vector2Int, GameObject> spawnedExits = new Dictionary<Vector2Int, GameObject>();
	private Dictionary<Vector2Int, GameObject> spawnedFruits = new Dictionary<Vector2Int, GameObject>();
	private Dictionary<Vector2Int, GameObject> spawnedBoxes = new Dictionary<Vector2Int, GameObject>();
	private Dictionary<Vector2Int, GameObject> spawnedIceCubes = new Dictionary<Vector2Int, GameObject>();
	private Dictionary<Vector2Int, GameObject> spawnedHoles = new Dictionary<Vector2Int, GameObject>();
	private Dictionary<Vector2Int, GameObject> spawnedLiftGates = new Dictionary<Vector2Int, GameObject>();
	private Dictionary<Vector2Int, GameObject> spawnedLaserGates = new Dictionary<Vector2Int, GameObject>();

	// With the other dictionary declarations
	private Dictionary<Vector2Int, GameObject> spawnedPlates = new Dictionary<Vector2Int, GameObject>();
	private Dictionary<Vector2Int, GameObject> spawnedPortals = new Dictionary<Vector2Int, GameObject>();


	/// <summary>
	/// In Awake, we subscribe to all the relevant events from the GameManager.
	/// </summary>
	private void Awake()
	{
		// Safety check in case the GameManager doesn't exist yet.
		if (GameManager.Instance == null) return;

		// Subscribe our methods to be called when the GameManager fires these events.
		GameManager.Instance.OnLevelLoaded += DrawLevelVisuals;   // When a level is ready, draw it.
		GameManager.Instance.OnFruitEaten += OnFruitEatenHandler; // When a fruit is eaten, destroy its visual.
		GameManager.Instance.OnFruitSpawned += OnFruitSpawnedHandler; // When a new fruit appears, draw it.
		GameManager.Instance.OnExitRemoved += OnExitRemovedHandler;   // When an exit is used, destroy its visual.
		GameManager.Instance.OnBoxMoved += OnBoxMovedHandler;     // When a box is pushed, animate it.
		GameManager.Instance.OnIceCubeMoved += OnIceCubeMovedHandler;     // When a iceCube is pushed, animate it.
		GameManager.Instance.OnHoleFilled += Instance_OnHoleFilled;
		GameManager.Instance.OnLiftGateStateChanged += OnLiftGateStateChangedHandler;
		GameManager.Instance.OnLaserGateStateChanged += OnLaserGateStateChangedHandler;

		GameManager.Instance.OnPlateStateChanged += OnPlateStateChangedHandler;
	}



	/// <summary>
	/// In OnDestroy, we unsubscribe from the events to prevent errors or memory leaks.
	/// </summary>
	private void OnDestroy()
	{
		if (GameManager.Instance != null)
		{
			// Unsubscribe our methods from the events.
			GameManager.Instance.OnLevelLoaded -= DrawLevelVisuals;
			GameManager.Instance.OnFruitEaten -= OnFruitEatenHandler;
			GameManager.Instance.OnFruitSpawned -= OnFruitSpawnedHandler;
			GameManager.Instance.OnExitRemoved -= OnExitRemovedHandler;
			GameManager.Instance.OnBoxMoved -= OnBoxMovedHandler;
			GameManager.Instance.OnIceCubeMoved -= OnIceCubeMovedHandler;
			GameManager.Instance.OnGateStateChanged -= OnGateStateChangedHandler;
			GameManager.Instance.OnPlateStateChanged -= OnPlateStateChangedHandler;
		}
	}

	/// <summary>
	/// The main drawing function, called when a new level is loaded.
	/// It reads the level data and instantiates all the necessary prefabs.
	/// </summary>
	private void DrawLevelVisuals(Level_SO levelData)
	{
		// First, destroy all old visuals from the previous level.
		ClearVisuals();
		var grid = GameManager.Instance.grid;

		// Draw Walls
		foreach (var wallPos in levelData.wallPositions)
		{
			Vector3 worldPos = grid.GetWorldPositionOfCellCenter(wallPos.x, wallPos.y);
			Instantiate(wallPrefab, worldPos, Quaternion.identity, levelObjectsParent);

		}


		// Draw Holes
		foreach (var holePos in levelData.holePositions)
		{
			Vector3 worldPos = grid.GetWorldPositionOfCellCenter(holePos.x, holePos.y);
			GameObject holeGO = Instantiate(holePrefab, worldPos, Quaternion.identity, levelObjectsParent);

			spawnedHoles[holePos] = holeGO;

		}

		// Draw Exits
		foreach (var exitData in levelData.exits)
		{
			Vector3 worldPos = grid.GetWorldPositionOfCellCenter(exitData.position.x, exitData.position.y);
			GameObject exitGO = Instantiate(exitPrefab, worldPos, Quaternion.identity, levelObjectsParent);
			// Store a reference to the spawned exit GameObject in our dictionary.
			spawnedExits[exitData.position] = exitGO;
		}

		// Draw initial Fruits
		foreach (var fruitData in levelData.fruits)
		{
			// Use the helper function to handle the more complex logic of drawing fruits.
			DrawFruitVisuals(fruitData);
		}

		// Draw initial Boxes
		foreach (var boxPos in levelData.boxPositions)
		{
			Vector3 worldPos = grid.GetWorldPositionOfCellCenter(boxPos.x, boxPos.y);
			GameObject boxGO = Instantiate(boxPrefab, worldPos, Quaternion.identity, levelObjectsParent);
			// Store a reference to the spawned box GameObject.
			spawnedBoxes[boxPos] = boxGO;
		}
		// Draw initial IceCubes
		foreach (var iceCubesPos in levelData.iceCubePositions)
		{
			Vector3 worldPos = grid.GetWorldPositionOfCellCenter(iceCubesPos.x, iceCubesPos.y);
			GameObject iceCubeGO = Instantiate(iceCubePrefab, worldPos, Quaternion.identity, levelObjectsParent);
			// Store a reference to the spawned box GameObject.
			spawnedIceCubes[iceCubesPos] = iceCubeGO;
		}
		foreach (var plateData in levelData.pressurePlates)
		{
			Vector3 worldPos = grid.GetWorldPositionOfCellCenter(plateData.position.x, plateData.position.y);
			GameObject plateGO = Instantiate(pressurePlatePrefab, worldPos, Quaternion.identity, levelObjectsParent);
			spawnedPlates[plateData.position] = plateGO;
		}

		// Draw Pressure Plates (Optional Visuals)
		// foreach (var plateData in levelData.pressurePlates)
		// {
		// 	Vector3 worldPos = grid.GetWorldPositionOfCellCenter(plateData.position.x, plateData.position.y);
		// 	Instantiate(pressurePlatePrefab, worldPos, Quaternion.identity, levelObjectsParent);
		// }

		foreach (var gateData in levelData.liftGates)
		{
			Vector3 worldPos = grid.GetWorldPositionOfCellCenter(gateData.position.x, gateData.position.y);
			GameObject gateGO = Instantiate(liftGatePrefab, worldPos, Quaternion.identity, levelObjectsParent);
			spawnedLiftGates[gateData.position] = gateGO;
		}
		// Draw Laser Gates
		foreach (var laserData in levelData.laserGates)
		{
			Vector3 worldPos = grid.GetWorldPositionOfCellCenter(laserData.position.x, laserData.position.y);
			GameObject laserGO = Instantiate(laserGatePrefab, worldPos, Quaternion.identity, levelObjectsParent);
			spawnedLaserGates[laserData.position] = laserGO;
		}

		foreach (var portalData in levelData.portals)
		{
			Vector3 worldPos = grid.GetWorldPositionOfCellCenter(portalData.position.x, portalData.position.y);
			// Slightly offset Y if needed to prevent z-fighting with floor, or keep at 0
			worldPos.y = 0.01f;

			GameObject portalGO = Instantiate(portalPrefab, worldPos, Quaternion.identity, levelObjectsParent);

			// Color the portal based on ID
			var renderer = portalGO.GetComponentInChildren<Renderer>();
			if (renderer != null)
			{
				Color c = Color.white;
				switch (portalData.colorId)
				{
					case PortalColor.Orange: c = new Color(1f, 0.5f, 0f); break; // Orange
					case PortalColor.Cyan: c = Color.cyan; break;
					case PortalColor.Magenta: c = Color.magenta; break;
				}
				renderer.material.color = c;
			}
			spawnedPortals[portalData.position] = portalGO;
		}
	}

	// --- EVENT HANDLERS ---

	/// <summary>
	/// Handles the animation for a pushed box.
	/// </summary>
	private void OnBoxMovedHandler(Vector2Int from, Vector2Int to)
	{
		// Check if we have a visual for the box at its starting position.
		if (spawnedBoxes.ContainsKey(from))
		{
			GameObject boxGO = spawnedBoxes[from];

			// IMPORTANT: Update the dictionary to track the box at its new grid position.
			spawnedBoxes.Remove(from);
			spawnedBoxes[to] = boxGO;

			// Animate the movement using DOTween.
			Vector3 targetWorldPos = GameManager.Instance.grid.GetWorldPositionOfCellCenter(to.x, to.y);
			boxGO.transform.DOMove(targetWorldPos, boxMoveDuration).SetEase(Ease.Linear);
		}
	}
	private void OnIceCubeMovedHandler(Vector2Int from, Vector2Int to)
	{
		// Check if we have a visual for the box at its starting position.
		if (spawnedIceCubes.ContainsKey(from))
		{
			GameObject iceCube = spawnedIceCubes[from];

			// IMPORTANT: Update the dictionary to track the box at its new grid position.
			spawnedIceCubes.Remove(from);
			spawnedIceCubes[to] = iceCube;

			// Animate the movement using DOTween.
			Vector3 targetWorldPos = GameManager.Instance.grid.GetWorldPositionOfCellCenter(to.x, to.y);
			iceCube.transform.DOMove(targetWorldPos, iceCubeMoveDuration).SetEase(Ease.Linear);
		}
	}

	/// <summary>
	/// Handles drawing a fruit that spawns mid-game (e.g., after a snake exits).
	/// </summary>
	private void OnFruitSpawnedHandler(FruitData fruitData)
	{
		DrawFruitVisuals(fruitData);
	}

	/// <summary>
	/// Handles destroying the visual of an exit that has been used.
	/// </summary>
	private void OnExitRemovedHandler(Vector2Int position)
	{
		if (spawnedExits.ContainsKey(position))
		{
			Destroy(spawnedExits[position]);
			spawnedExits.Remove(position);
		}
	}

	/// <summary>
	/// Handles destroying the visual of a fruit that has been eaten.
	/// </summary>
	private void OnFruitEatenHandler(FruitData eatenFruit)
	{
		Vector2Int position = eatenFruit.position;
		if (spawnedFruits.ContainsKey(position))
		{
			Destroy(spawnedFruits[position]);
			spawnedFruits.Remove(position);
		}
	}

	/// <summary>
	/// Handles visually opening or closing a laser gate.
	/// </summary>
	private void OnGateStateChangedHandler(LaserGateData gateData, bool isOpen)
	{
		if (spawnedLaserGates.ContainsKey(gateData.position))
		{
			GameObject gateGO = spawnedLaserGates[gateData.position];
			// The simplest visual change is to just activate/deactivate the GameObject.
			// True (open) means the gate should be invisible/inactive.
			// False (closed) means the gate should be visible/active.
			gateGO.SetActive(!isOpen);
		}
	}
	private void OnPlateStateChangedHandler(PressurePlateData plateData, bool isActive)
	{
		if (spawnedPlates.TryGetValue(plateData.position, out GameObject plateGO))
		{
			// Animate the plate moving down when active, and to its original height when inactive.
			float targetY = isActive ? plateGO.transform.position.y - 0.1f : 0f;
			plateGO.transform.DOMoveY(targetY, plateMoveDuration).SetEase(Ease.OutQuad);
		}
	}
	// --- HELPER METHODS ---

	/// <summary>
	/// Clears all spawned objects and dictionaries to prepare for a new level.
	/// </summary>
	private void ClearVisuals()
	{
		// A safe way to destroy all children of a Transform.
		foreach (Transform child in levelObjectsParent)
		{
			Destroy(child.gameObject);
		}
		// Clear all tracking dictionaries.
		spawnedExits.Clear();
		spawnedFruits.Clear();
		spawnedBoxes.Clear();
		spawnedHoles.Clear();
		spawnedIceCubes.Clear();
		spawnedLaserGates.Clear();
		spawnedPortals.Clear();
		spawnedPlates.Clear();
	}

	/// <summary>
	/// Handles the specific logic for instantiating and coloring a fruit prefab.
	/// </summary>
	private void DrawFruitVisuals(FruitData fruitData)
	{
		var grid = GameManager.Instance.grid;
		Vector3 worldPos = grid.GetWorldPositionOfCellCenter(fruitData.position.x, fruitData.position.y);
		GameObject fruitGO = Instantiate(fruitPrefab, worldPos, Quaternion.identity, levelObjectsParent);

		// If the fruit is multi-colored, add a rotator component to make it spin.
		if (fruitData.colors.Count > 1)
		{
			// You would need to create a simple Rotator script for this to work.
			// Example:
			// public class Rotator : MonoBehaviour {
			//     void Update() { transform.Rotate(0, 90 * Time.deltaTime, 0); }
			// }
			// fruitGO.AddComponent<Rotator>();
		}

		// Get all the MeshRenderer components in the fruit prefab and its children.
		// This allows for multi-part fruit models.
		MeshRenderer[] renderers = fruitGO.GetComponentsInChildren<MeshRenderer>();

		if (fruitData.colors.Count == 1)
		{
			// If it's a single-color fruit, color all parts with that color.
			Color singleColor = GetEngineColor(fruitData.colors[0]);
			foreach (var renderer in renderers)
			{
				renderer.material.color = singleColor;
			}
		}
		else
		{
			// If it's a multi-color fruit, color each part with a different color from the list.
			for (int i = 0; i < renderers.Length; i++)
			{
				if (i < fruitData.colors.Count)
				{
					renderers[i].material.color = GetEngineColor(fruitData.colors[i]);
				}
			}
		}

		// Store the new fruit's GameObject in our dictionary for later reference.
		spawnedFruits[fruitData.position] = fruitGO;
	}


	private void Instance_OnHoleFilled(Vector2Int holepos, Vector2Int fillerpos)
	{

		if (spawnedBoxes.ContainsKey(fillerpos))
		{
			Destroy(spawnedBoxes[fillerpos]);
			spawnedBoxes.Remove(fillerpos);
		}
		else if (spawnedIceCubes.ContainsKey(fillerpos))
		{
			Destroy(spawnedIceCubes[fillerpos]);
			spawnedIceCubes.Remove(fillerpos);
		}
		else
		{
			Debug.LogError("Error at OnholeFilled dictionaries didnt contain key" + fillerpos);
		}
		if (holepos != Vector2Int.zero) // A simple check to avoid error on null
		{
			if (spawnedHoles.ContainsKey(holepos))
			{
				Destroy(spawnedHoles[holepos]);
				spawnedHoles.Remove(holepos);
			}
		}

	}
	
	private void OnLiftGateStateChangedHandler(LiftGateData data, bool isOpen)
	{
		if (spawnedLiftGates.TryGetValue(data.position, out GameObject go))
		{
			// If Open -> Physical wall goes down (Inactive/Invisible)
			go.SetActive(!isOpen); 
		}
	}

	private void OnLaserGateStateChangedHandler(LaserGateData data, bool isActive)
	{
		if (spawnedLaserGates.TryGetValue(data.position, out GameObject go))
		{
			// If Active -> Beam is Visible
			go.SetActive(isActive);
		}
	}


	/// <summary>
	/// A utility function to convert our custom ColorType enum into a Unity Engine Color.
	/// </summary>
	private Color GetEngineColor(ColorType colorType)
	{
		switch (colorType)
		{
			case ColorType.Red: return Color.red;
			case ColorType.Blue: return Color.blue;
			case ColorType.Green: return Color.green;
			case ColorType.Yellow: return Color.yellow;
			default: return Color.white;
		}
	}
}