// --- PlayerController.cs ---

using System;
using UnityEngine;

/// <summary>
/// Handles all player input, such as clicking and dragging snakes.
/// It acts as the bridge between the player's mouse and the game's logical systems.
/// It uses a state machine to manage behavior (e.g., ignoring input during UI,
/// detecting clicks when idle, processing movement when dragging).
/// </summary>
public class PlayerController : MonoBehaviour
{
	/// <summary>
	/// A simple state machine to define what the controller should be doing.
	/// </summary>
	private enum State
	{
		Idle,     // Waiting for the player to click on a snake.
		Dragging, // The player is actively holding the mouse button down and moving a snake.
		UI,       // A UI screen (like the win screen) is active. All gameplay input should be ignored.
	}

	[Tooltip("A reference to the LevelManager, used to know when a level starts.")]
	public LevelManager levelManager;

	// --- Private State ---
	private State currentState; // The current state of the controller.
	private Vector2Int lastMouseGridPosition; // The grid cell the mouse was over in the previous frame. Used to detect when the mouse has moved to a new cell.

	private Snake selectedSnake; // A reference to the logical Snake object the player is currently dragging.
	private SnakeEnd partToMove; // Which end of the selected snake (Head or Tail) the player is moving.

	private void Awake()
	{
		currentState = State.UI;
		levelManager.OnLevelStarted += LevelManager_OnLevelStarted;
		GameManager.Instance.LevelWin += Instance_LevelWin;
        
		// NEW: Subscribe to Death Event
		GameManager.Instance.OnSnakeDied += Instance_OnSnakeDied;
	}

	private void Instance_OnSnakeDied(Snake deadSnake)
	{
		// If the snake we are currently dragging just died...
		if (selectedSnake == deadSnake)
		{
			StopDragging(); // Release it immediately
		}
	}

	/// <summary>
	/// Event handler for when the GameManager announces a level win.
	/// </summary>
	private void Instance_LevelWin(object sender, EventArgs e)
	{
		// Switch to the UI state to prevent further gameplay input.
		currentState = State.UI;
	}

	/// <summary>
	/// Event handler for when the LevelManager announces a level has started.
	/// </summary>
	private void LevelManager_OnLevelStarted(object sender, EventArgs e)
	{
		// Switch to the Idle state, ready to accept player input.
		currentState = State.Idle;
	}

	/// <summary>
	/// The main Update loop, which runs every frame. It acts as the "manager" for our state machine.
	/// </summary>
	private void Update()
	{
		// The 'switch' statement directs program flow to the correct handler method based on the current state.
		switch (currentState)
		{
			case State.Idle:
				HandleIdleState();
				break;
			case State.Dragging:
				HandleDraggingState();
				break;
			case State.UI:
				// Do nothing in the UI state.
				break;
		}
	}

	/// <summary>
	/// This method is called every frame when the controller is in the 'Idle' state.
	/// It is responsible for detecting the initial mouse click that starts a drag.
	/// </summary>
	private void HandleIdleState()
	{
		// Check if the left mouse button was just pressed down this frame.
		if (Input.GetMouseButtonDown(0))
		{
			// Convert the mouse's screen position to a grid coordinate.
			Vector2Int mouseGridPos = GetMouseGridPosition();

			// Ask the GameManager if there is a snake head or tail at this grid position.
			Snake foundSnake = GameManager.Instance.GetSnakeAtPosition(mouseGridPos, out SnakeEnd clickedPart);

			// If the GameManager found a snake...
			if (foundSnake != null)
			{
				// ...we have successfully selected a snake to move.
				selectedSnake = foundSnake;
				partToMove = clickedPart;

				// Subscribe to the snake's OnMoveFailed event. This is crucial for handling
				// cases where the player drags to an invalid square.
				selectedSnake.OnMoveFailed += SelectedSnake_OnMoveFailed;

				// Store the current mouse position to compare against in the next frame.
				lastMouseGridPosition = mouseGridPos;

				// Transition to the 'Dragging' state.
				currentState = State.Dragging;
			}
		}
	}

	/// <summary>
	/// Event handler for when the selected snake fails to move.
	/// </summary>
	private void SelectedSnake_OnMoveFailed(object sender, Snake.OnMoveFailedEventArgs e)
	{
		// When a move fails, we immediately stop the drag operation.
		// This prevents the player from continuing to drag an invalid path.
		StopDragging();
		return;
	}

	/// <summary>
	/// This method is called every frame when the controller is in the 'Dragging' state.
	/// It handles the logic of continuously trying to move the snake as the mouse moves.
	/// </summary>
	private void HandleDraggingState()
	{
		// Check if the player has released the left mouse button.
		if (Input.GetMouseButtonUp(0))
		{
			// If so, stop the dragging process.
			StopDragging();
			return;
		}

		// Get the current grid position of the mouse.
		Vector2Int currentMouseGridPosition = GetMouseGridPosition();

		// Check if the mouse has moved to a *new* grid cell since the last frame.
		// This is an optimization to avoid sending move commands every single frame.
		if (currentMouseGridPosition != lastMouseGridPosition)
		{
			if (selectedSnake != null)
			{
				// Tell the selected snake to try moving to this new grid position.
				// The snake's own internal logic (in TryMoveTo) will handle all the rules.
				selectedSnake.TryMoveTo(currentMouseGridPosition, partToMove);
			}

			// Update the last known mouse position for the next frame's check.
			lastMouseGridPosition = currentMouseGridPosition;
		}
	}

	/// <summary>
	/// A cleanup method to reset the controller's state after a drag is finished (or fails).
	/// </summary>
	private void StopDragging()
	{
		// If we had a snake selected...
		if (selectedSnake != null)
		{
			// ...it's very important to unsubscribe from its event to prevent memory leaks.
			selectedSnake.OnMoveFailed -= SelectedSnake_OnMoveFailed;

			// Clear our reference to the snake.
			selectedSnake = null;
		}

		// Transition back to the 'Idle' state, ready for the next click.
		currentState = State.Idle;
	}

	/// <summary>
	/// A helper function to convert the mouse's position on the screen to a grid coordinate.
	/// </summary>
	/// <returns>A Vector2Int representing the (x, z) coordinate on the grid.</returns>
	private Vector2Int GetMouseGridPosition()
	{
		// 1. Get the mouse's world position on the grid's plane using the utility function.
		Vector3 worldPosition = CreateTextUtils.GetMouseWorldPosition();

		// 2. Ask the grid to convert that world position into its corresponding (x, z) indices.
		GameManager.Instance.grid.GetXZ(worldPosition, out int x, out int z);
		return new Vector2Int(x, z);
	}
}