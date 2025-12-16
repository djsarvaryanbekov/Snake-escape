// --- Level_SO.cs ---

using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Defines the data structure for a single level using a Scriptable Object.
/// Scriptable Objects are data containers that you can create as assets in your Unity project.
/// This allows you to design and save multiple levels by creating different "New Level" assets,
/// each with its own unique configuration, without having to change any code.
/// </summary>
// The CreateAssetMenu attribute allows you to create instances of this object
// directly from the Unity Editor's Assets > Create menu.
[CreateAssetMenu(fileName = "New Level", menuName = "Snake/Level Data")]
public class Level_SO : ScriptableObject
{
	[Header("Grid Settings")]
	[Tooltip("The width of the level's grid in number of cells.")]
	public int width;
	[Tooltip("The height of the level's grid in number of cells.")]
	public int height;

	[Header("Level Objects")]
	[Tooltip("A list of all grid positions where pushable boxes should be placed.")]
	public List<Vector2Int> boxPositions;

	[Tooltip("A list of all grid positions where impassable walls should be placed.")]
	public List<Vector2Int> wallPositions;

	[Tooltip("A list containing the data for each snake that will be spawned in this level.")]
	public List<SnakeData> snakes;

	[Tooltip("A list containing the data for each exit in this level.")]
	public List<ExitData> exits;

	[Tooltip("A list containing the data for each fruit that exists at the start of the level.")]
	public List<FruitData> fruits;


	public List<Vector2Int> iceCubePositions;

	public List<Vector2Int> holePositions;


	[Header("Interactive Objects")]
	public List<PressurePlateData> pressurePlates;
	public List<LaserGateData> laserGates;


	[Header("Teleportation")]
	public List<PortalData> portals; // New
}