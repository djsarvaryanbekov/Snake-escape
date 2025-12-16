// --- UtilExample.cs ---

using UnityEngine;

/// <summary>
/// A static utility class containing helper methods that can be accessed from anywhere in the project.
/// Static classes do not need to be attached to a GameObject in the scene.
/// The methods here are focused on creating text objects and handling mouse-to-world position conversion.
/// </summary>
public static class CreateTextUtils
{
	// --- Text Creation Methods ---
	// These methods are for creating 3D text (TextMesh) directly in the game world.

	/// <summary>
	/// An overloaded, simpler version of the text creation method with default values.
	/// </summary>
	/// <param name="text">The string of text to display.</param>
	/// <param name="parent">The parent transform for the new text object (optional).</param>
	/// <param name="localPosition">The position relative to the parent (optional).</param>
	/// <param name="fontSize">The font size of the text (optional).</param>
	/// <param name="color">The color of the text (optional, defaults to white).</param>
	/// <param name="spriteColor">NOTE: This parameter seems to be a remnant of old code and is not used.</param>
	/// <param name="textAnchor">The anchor point of the text (e.g., UpperLeft, MiddleCenter) (optional).</param>
	/// <param name="textAlignment">The alignment of the text (Left, Center, Right) (optional).</param>
	/// <param name="sortingOrder">The sorting order for rendering, useful for layering with sprites (optional).</param>
	/// <returns>The created TextMesh component.</returns>
	public static TextMesh CreateWorldText(string text, Transform parent = null, Vector3 localPosition = default(Vector3), int fontSize = 40, Color? color = null, Color? spriteColor = null, TextAnchor textAnchor = TextAnchor.UpperLeft, TextAlignment textAlignment = TextAlignment.Left, int sortingOrder = 0)
	{
		// Set default colors if none are provided.
		if (color == null) color = Color.white;
		if (spriteColor == null) spriteColor = Color.white; // This value is set but never used in the next call.

		// Call the more detailed version of the method with the provided parameters.
		return CreateWorldText(parent, text, localPosition, fontSize, (Color)color, (Color)spriteColor, textAnchor, textAlignment, sortingOrder);
	}

	/// <summary>
	/// The core method for creating a TextMesh object in the world.
	/// </summary>
	public static TextMesh CreateWorldText(Transform parent, string text, Vector3 localPosition, int fontSize, Color color, Color spriteColor, TextAnchor textAnchor, TextAlignment textAlignment, int sortingOrder)
	{
		// 1. Create a new GameObject named "World_Text" and immediately add a TextMesh component to it.
		GameObject gameObjectText = new GameObject("World_Text", typeof(TextMesh));

		// 2. Configure the GameObject's transform.
		Transform transform = gameObjectText.transform;
		transform.SetParent(parent, false); // Set its parent without changing its world position initially.
		transform.localPosition = localPosition; // Set its position relative to the parent.

		// NOTE: The following lines related to a SpriteRenderer are commented out.
		// This suggests a previous version of this function might have included a background sprite for the text.
		// SpriteRenderer sprite = gameObjectText.AddComponent<SpriteRenderer>();
		// sprite.color = spriteColor;

		// 3. Get the TextMesh component and configure its properties.
		TextMesh textMesh = gameObjectText.GetComponent<TextMesh>();
		textMesh.anchor = textAnchor;
		textMesh.alignment = textAlignment;
		textMesh.text = text;
		textMesh.fontSize = fontSize;
		textMesh.color = color;

		// 4. Set the sorting order of the MeshRenderer to control how it layers with other objects.
		textMesh.GetComponent<MeshRenderer>().sortingOrder = sortingOrder;

		return textMesh;
	}


	// --- Mouse Position Methods ---

	// NOTE: The block of code commented out below shows a simpler, but often incorrect,
	// way to get the mouse position in a 3D world, especially one that isn't screen-aligned.
	/*FOR 2D Get Mouse Position in World with Z = 0f
	public static Vector3 GetMouseWorldPosition() { ... }
	public static Vector3 GetMouseWorldPositionWithZ(Vector3 screenPosition, Camera worldCamera) { ... }
	*/

	/// <summary>
	/// Accurately calculates the mouse's position in world space, specifically on the plane where the grid exists.
	/// This is the correct method for this game's perspective camera setup.
	/// </summary>
	/// <returns>The world space Vector3 where the mouse cursor intersects the grid plane.</returns>
	public static Vector3 GetMouseWorldPosition()
	{
		// Safety check to ensure the GameManager and its grid are ready.
		if (GameManager.Instance == null || GameManager.Instance.grid == null)
		{
			// Return an obviously invalid position if the grid isn't initialized.
			return Vector3.one * -1;
		}

		// 1. Create a Ray. A ray is an infinite line starting from the camera's position
		//    and shooting out in the direction of the mouse cursor on the screen.
		Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

		// 2. Create a mathematical Plane. The grid in the game exists on a flat, horizontal plane.
		//    We need to define this plane in mathematical terms to find where the ray hits it.
		//    A plane is defined by a point on the plane (the grid's origin) and a normal vector
		//    (a vector pointing straight out of the plane's surface, which is Vector3.up for a flat grid).
		Vector3 gridOrigin = GameManager.Instance.grid.GetWorldPosition(0, 0);
		Plane gridPlane = new Plane(Vector3.up, gridOrigin);

		// 3. Perform a Raycast. This function checks if the `ray` intersects with the `gridPlane`.
		//    The `out float enter` variable will be filled with the distance from the ray's origin
		//    to the point of intersection.
		if (gridPlane.Raycast(ray, out float enter))
		{
			// 4. If the ray hits the plane, we can get the exact intersection point.
			//    `ray.GetPoint(enter)` gives us the point along the ray at the calculated distance.
			Vector3 worldPosition = ray.GetPoint(enter);
			return worldPosition;
		}
		else
		{
			// This case is unlikely unless the camera is looking parallel to or away from the grid.
			// The ray did not hit the plane, so we return an invalid position.
			return Vector3.one * -1;
		}
	}
}