// --- LevelData.cs ---

using UnityEngine;
using System.Collections.Generic;

// This file contains several small "data-only" classes. Their purpose is to hold
// information in a structured way. The `[System.Serializable]` attribute is essential;
// it allows Unity to see these classes and show their fields in the Inspector when
// they are used in another class (like the Level_SO).


/// <summary>
/// A data container for defining a single snake in a level.
/// </summary>
[System.Serializable]
public class SnakeData
{
	[Tooltip("The color of the snake, which determines its abilities.")]
	public ColorType color;

	[Tooltip("The starting grid coordinate of the snake's head.")]
	public Vector2Int headPosition;

	[Tooltip("The starting grid coordinate of the snake's tail.")]
	public Vector2Int tailPosition;
}

/// <summary>
/// A data container for defining a single exit in a level.
/// </summary>
[System.Serializable]
public class ExitData
{
	[Tooltip("The color of the exit. Only snakes of the same color can use it.")]
	public ColorType color;

	[Tooltip("The minimum body length a snake must have to use this exit.")]
	public int requiredLength;

	[Tooltip("The grid coordinate where this exit is located.")]
	public Vector2Int position;
}






/// <summary>
/// A data container for defining a single fruit in a level.
/// </summary>
[System.Serializable]
public class FruitData
{
	[Tooltip("A list of snake colors that are allowed to eat this fruit.")]
	public List<ColorType> colors;

	[Tooltip("The grid coordinate where this fruit is located.")]
	public Vector2Int position;
}




[System.Serializable]
public class PressurePlateData
{
	public PlateColor color;
	public Vector2Int position;
}

[System.Serializable]
public class LaserGateData
{
	public PlateColor color;
	public Vector2Int position;
}