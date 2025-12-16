/*using Unity.VisualScripting;
using UnityEngine;

public class Drag : MonoBehaviour
{
    GameObject selectedObject;
    bool isSnake;
    Snake snake;

	private void Start()
	{
		
	}

	private void Update()
	{
        

        if (Input.GetMouseButtonDown(0)) { 

            if (selectedObject == null)
            { 
				RaycastHit hit = CastRay();

				if (hit.collider != null )
				{
                    isSnake = hit.collider.GetComponent<Snake>();
					if ( isSnake)
                    {
                        selectedObject = hit.collider.gameObject;
                        snake = hit.collider.GetComponent<Snake>();

                    }       
				}

			}
			else
			{
				


			}
		}
	}

	public RaycastHit CastRay()
    {
        Vector3 screenMousePosFar = new Vector3(
            Input.mousePosition.x,
            Input.mousePosition.y,
            Camera.main.farClipPlane
            );
        Vector3 screenMousePosNear = new Vector3(
            Input.mousePosition.x,
            Input.mousePosition.y, Camera.main.nearClipPlane);
        Vector3 worldMousePosFar = Camera.main.ScreenToWorldPoint(screenMousePosFar);
        Vector3 worldMousePosNear = Camera.main.ScreenToWorldPoint(screenMousePosNear);
        RaycastHit hit;
        Physics.Raycast(worldMousePosNear, worldMousePosFar - worldMousePosNear, out hit);

        return hit;
    }
}
*/

