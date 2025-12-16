// --- UIScripts.cs ---

using UnityEngine;

public class UIScripts : MonoBehaviour
{

	[SerializeField] GameObject winCanvas;
	private GameManager gameManager;

	private void Awake()
	{
		gameManager = GameManager.Instance;
		if (gameManager != null)
		{
			gameManager.LevelWin += Instance_LevelWin;
		}

		if (winCanvas != null)
		{
			winCanvas.SetActive(false);
		}
	}

	private void Instance_LevelWin(object sender, System.EventArgs e)
	{
		if (winCanvas != null)
		{
			winCanvas.SetActive(true);
		}
	}

	private void OnDestroy()
	{
		if (gameManager != null)
		{
			gameManager.LevelWin -= Instance_LevelWin;
		}
	}
}