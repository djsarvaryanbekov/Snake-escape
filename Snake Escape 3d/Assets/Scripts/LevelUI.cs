// --- LevelUI.cs ---

using UnityEngine;

/// <summary>
/// A simple controller for the level's UI elements, specifically the win screen.
/// </summary>
public class LevelUI : MonoBehaviour
{
	[Tooltip("The parent GameObject for the UI that should be shown when the level is won (e.g., a 'Level Complete!' panel).")]
	public GameObject gameObjectUI;

	// A cached reference to the GameManager.
	GameManager gameManager;

	void Start()
	{
		// Get the singleton instance of the GameManager.
		gameManager = GameManager.Instance;

		// Subscribe our GameManager_LevelWin method to the LevelWin event.
		// Now, whenever the GameManager fires the LevelWin event, our method will be called.
		gameManager.LevelWin += GameManager_LevelWin;

		// Ensure the UI is hidden at the start of the level.
		if (gameObjectUI != null)
		{
			gameObjectUI.SetActive(false);
		}
	}

	/// <summary>
	/// This is the event handler that gets executed when the GameManager.LevelWin event is fired.
	/// </summary>
	private void GameManager_LevelWin(object sender, System.EventArgs e)
	{
		// Activate the assigned UI GameObject, making the win screen visible.
		if (gameObjectUI != null)
		{
			gameObjectUI.SetActive(true);
		}
	}

	// It's good practice to unsubscribe from events in OnDestroy,
	// though for a simple UI script that lives for the whole scene, it's less critical.
	private void OnDestroy()
	{
		if (gameManager != null)
		{
			gameManager.LevelWin -= GameManager_LevelWin;
		}
	}
}