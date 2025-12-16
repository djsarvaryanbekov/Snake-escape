// --- SnakeVisualizer.cs ---

using System.Collections.Generic;
using UnityEngine;
using DG.Tweening; // A popular third-party library for creating smooth animations (tweens).

/// <summary>
/// Handles the visual representation of a single snake.
/// This component is attached to a prefab that is spawned by the LevelManager.
/// Its job is to listen to events from a specific 'logicalSnake' instance and
/// update the on-screen GameObjects (head, body, tail) to match the snake's state.
/// It manages animations for moving, growing, and being removed.
/// </summary>
public class SnakeVisualizer : MonoBehaviour
{
	[Header("Prefabs")]
	[Tooltip("The GameObject to use for the snake's head.")]
	[SerializeField] private GameObject headPrefab;
	[Tooltip("The GameObject to use for a middle body segment.")]
	[SerializeField] private GameObject bodyPrefab;
	[Tooltip("The GameObject to use for the snake's tail.")]
	[SerializeField] private GameObject tailPrefab;

	[Header("Animation Settings")]
	[Tooltip("The duration of the movement animation in seconds.")]
	[SerializeField] private float moveDuration = 0.15f;

	// --- Private State ---

	private Snake logicalSnake; // A reference to the logical Snake class this visualizer is representing.

	// A list to keep track of all the instantiated segment GameObjects (head, body, tail).
	// The order in this list MUST match the order of segments in the logicalSnake.Body list.
	private List<GameObject> visualSegments = new List<GameObject>();

	// A flag to prevent starting new animations while a full redraw is in progress.
	private bool isRedrawing = false;

	/// <summary>
	/// A public property that indicates if the snake is currently playing a movement animation.
	/// Used by the Snake class to prevent logical moves while the visuals are still catching up.
	/// </summary>
	public bool IsAnimating { get; private set; } = false;

	/// <summary>
	/// This is the entry point, called by the LevelManager after creating the logical snake and this visualizer.
	/// It links the two together.
	/// </summary>
	/// <param name="snakeToVisualize">The logical snake instance this visualizer should follow.</param>
	public void Initialize(Snake snakeToVisualize)
	{
		this.logicalSnake = snakeToVisualize;

		// Subscribe to the events of the logical snake.
		// When the snake's state changes, our animation methods will be called automatically.
		logicalSnake.OnMoved += AnimateMove;
		logicalSnake.OnGrew += AnimateGrowth;
		logicalSnake.OnRemoved += AnimateRemoval;

		// Perform an initial draw to create the snake's visuals for the first time.
		FullRedraw();
	}

	/// <summary>
	/// Animates the smooth movement of all snake segments from their old positions to their new ones.
	/// </summary>
	private void AnimateMove()
	{
		// Safety check to avoid starting a new move animation while one is already playing.
		if (IsAnimating || isRedrawing)
		{
			Debug.LogWarning("Attempted to move while already animating or redrawing.");
			return;
		}

		IsAnimating = true;
		var snakeBody = logicalSnake.Body;

		// Data integrity check. If the number of visual parts doesn't match the logical parts,
		// something is wrong. A full redraw is the safest way to recover.
		if (visualSegments.Count != snakeBody.Count)
		{
			Debug.LogWarning("Visual segment count mismatch detected during move. Forcing a redraw.");
			FullRedraw();
			IsAnimating = false;
			return;
		}

		// Use DOTween's Sequence to animate all segments at the same time.
		Sequence moveSequence = DOTween.Sequence();

		for (int i = 0; i < snakeBody.Count; i++)
		{
			// Get the target world position for this segment from the logical grid.
			Vector3 targetWorldPos = GameManager.Instance.grid.GetWorldPositionOfCellCenter(snakeBody[i].x, snakeBody[i].y);
			targetWorldPos.y = 0.1f; // Ensure a consistent height above the grid.

			// Add this segment's move animation to the sequence. 'Join' makes it play simultaneously with others.
			moveSequence.Join(visualSegments[i].transform.DOMove(targetWorldPos, moveDuration));
		}

		// Set a callback to run when the entire animation sequence is complete.
		moveSequence.OnComplete(() => {
			IsAnimating = false; // Reset the flag so new moves can be made.
		});
	}

	/// <summary>
	/// Handles the visual update when the snake grows (eats a fruit).
	/// </summary>
	private void AnimateGrowth()
	{
		// If an animation is already in progress, wait for it to finish before redrawing.
		// This prevents visual glitches. We use a coroutine to handle the delay.
		if (IsAnimating || isRedrawing)
		{
			Debug.LogWarning("Growth animation requested while busy. It will be delayed.");
			StartCoroutine(DelayedGrowthAnimation());
			return;
		}

		isRedrawing = true;
		// The simplest way to handle growth is to destroy all old visuals and create them again
		// with the new, longer body. This is much easier than trying to add just one new segment.
		FullRedraw();
		isRedrawing = false;

		// After redrawing, the segments might be in their old positions.
		// A move animation will snap them all to their correct new positions.
		AnimateMove();
	}

	/// <summary>
	/// A coroutine that waits until all other animations are finished before triggering the growth animation.
	/// </summary>
	private System.Collections.IEnumerator DelayedGrowthAnimation()
	{
		yield return new WaitUntil(() => !IsAnimating && !isRedrawing);
		AnimateGrowth();
	}

	/// <summary>
	/// Handles the visual update when the snake is removed from the game (enters an exit).
	/// </summary>
	private void AnimateRemoval()
	{
		// Simply destroy all GameObjects associated with this snake.
		foreach (var segment in visualSegments)
		{
			Destroy(segment);
		}
		visualSegments.Clear();
	}

	/// <summary>
	/// The core drawing function. It destroys all existing visual segments and recreates them
	/// from scratch based on the current state of the logical snake's body.
	/// </summary>
	private void FullRedraw()
	{
		// --- Cleanup ---
		foreach (var segment in visualSegments)
		{
			Destroy(segment);
		}
		visualSegments.Clear();

		// --- Rebuilding ---
		var snakeBody = logicalSnake.Body;
		if (snakeBody.Count == 0) return; // Nothing to draw if the snake has no body.

		for (int i = 0; i < snakeBody.Count; i++)
		{
			GameObject segmentPrefab;

			// Determine which prefab to use based on the segment's position in the body list.
			if (i == 0) // The first segment is the head.
			{
				segmentPrefab = headPrefab;
			}
			else if (i == snakeBody.Count - 1) // The last segment is the tail.
			{
				segmentPrefab = tailPrefab;
			}
			else // Anything in between is a body part.
			{
				segmentPrefab = bodyPrefab;
			}

			// Instantiate the correct prefab as a child of this visualizer's transform.
			GameObject segment = Instantiate(segmentPrefab, transform);

			// Position the new segment at its correct world location.
			Vector3 worldPos = GameManager.Instance.grid.GetWorldPositionOfCellCenter(snakeBody[i].x, snakeBody[i].y);
			worldPos.y = 0.1f; // Set consistent height.
			segment.transform.position = worldPos;

			// Apply the correct color to the segment's material.
			Renderer segmentRenderer = segment.GetComponentInChildren<Renderer>();
			if (segmentRenderer != null)
			{
				segmentRenderer.material.color = GetColorFromEnum(logicalSnake.Color);
			}

			// Add the newly created segment to our tracking list.
			visualSegments.Add(segment);
		}
	}

	/// <summary>
	/// A utility function to convert our custom ColorType enum into a Unity Engine Color.
	/// </summary>
	private Color GetColorFromEnum(ColorType colorType)
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

	/// <summary>
	/// A good practice cleanup method. Unsubscribes from events when the object is destroyed
	/// to prevent errors if the logical snake is destroyed after the visualizer.
	/// </summary>
	private void OnDestroy()
	{
		if (logicalSnake != null)
		{
			logicalSnake.OnMoved -= AnimateMove;
			logicalSnake.OnGrew -= AnimateGrowth;
			logicalSnake.OnRemoved -= AnimateRemoval;
		}
	}
}