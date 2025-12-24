using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

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
    [SerializeField] private GameObject pressurePlatePrefab;
    [SerializeField] private GameObject liftGatePrefab;
    [SerializeField] private GameObject laserGatePrefab;

    [Header("Settings")]
    [SerializeField] private Transform levelObjectsParent;
    [SerializeField] private float boxMoveDuration = 0.15f;
    [SerializeField] private float iceCubeMoveDuration = 0.15f;
    [SerializeField] private float plateMoveDuration = 0.1f;

    // Dictionary tracking
    private Dictionary<Vector2Int, GameObject> spawnedExits = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, GameObject> spawnedFruits = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, GameObject> spawnedBoxes = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, GameObject> spawnedIceCubes = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, GameObject> spawnedHoles = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, GameObject> spawnedPlates = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, GameObject> spawnedLiftGates = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, GameObject> spawnedLaserGates = new Dictionary<Vector2Int, GameObject>();
    private Dictionary<Vector2Int, GameObject> spawnedPortals = new Dictionary<Vector2Int, GameObject>();

    private void Awake()
    {
        if (GameManager.Instance == null) return;

        GameManager.Instance.OnLevelLoaded += DrawLevelVisuals;
        GameManager.Instance.OnFruitEaten += OnFruitEatenHandler;
        GameManager.Instance.OnFruitSpawned += OnFruitSpawnedHandler;
        GameManager.Instance.OnExitRemoved += OnExitRemovedHandler;
        GameManager.Instance.OnBoxMoved += OnBoxMovedHandler;
        GameManager.Instance.OnIceCubeMoved += OnIceCubeMovedHandler;
        GameManager.Instance.OnHoleFilled += Instance_OnHoleFilled;
        
        // NEW: Specific gate events
        GameManager.Instance.OnLiftGateStateChanged += OnLiftGateStateChangedHandler;
        GameManager.Instance.OnLaserGateStateChanged += OnLaserGateStateChangedHandler;
        
        GameManager.Instance.OnPlateStateChanged += OnPlateStateChangedHandler;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLevelLoaded -= DrawLevelVisuals;
            GameManager.Instance.OnFruitEaten -= OnFruitEatenHandler;
            GameManager.Instance.OnFruitSpawned -= OnFruitSpawnedHandler;
            GameManager.Instance.OnExitRemoved -= OnExitRemovedHandler;
            GameManager.Instance.OnBoxMoved -= OnBoxMovedHandler;
            GameManager.Instance.OnIceCubeMoved -= OnIceCubeMovedHandler;
            GameManager.Instance.OnHoleFilled -= Instance_OnHoleFilled;
            
            // NEW: Correct unsubscribes
            GameManager.Instance.OnLiftGateStateChanged -= OnLiftGateStateChangedHandler;
            GameManager.Instance.OnLaserGateStateChanged -= OnLaserGateStateChangedHandler;
            
            GameManager.Instance.OnPlateStateChanged -= OnPlateStateChangedHandler;
        }
    }

    private void DrawLevelVisuals(Level_SO levelData)
    {
        ClearVisuals();
        var grid = GameManager.Instance.grid;

        // Walls
        foreach (var wallPos in levelData.wallPositions)
        {
            Instantiate(wallPrefab, grid.GetWorldPositionOfCellCenter(wallPos.x, wallPos.y), Quaternion.identity, levelObjectsParent);
        }

        // Holes
        foreach (var holePos in levelData.holePositions)
        {
            GameObject go = Instantiate(holePrefab, grid.GetWorldPositionOfCellCenter(holePos.x, holePos.y), Quaternion.identity, levelObjectsParent);
            spawnedHoles[holePos] = go;
        }

        // Exits
        foreach (var exitData in levelData.exits)
        {
            GameObject go = Instantiate(exitPrefab, grid.GetWorldPositionOfCellCenter(exitData.position.x, exitData.position.y), Quaternion.identity, levelObjectsParent);
            spawnedExits[exitData.position] = go;
        }

        // Fruits
        foreach (var fruitData in levelData.fruits) DrawFruitVisuals(fruitData);

        // Boxes
        foreach (var pos in levelData.boxPositions)
        {
            GameObject go = Instantiate(boxPrefab, grid.GetWorldPositionOfCellCenter(pos.x, pos.y), Quaternion.identity, levelObjectsParent);
            spawnedBoxes[pos] = go;
        }

        // Ice Cubes
        foreach (var pos in levelData.iceCubePositions)
        {
            GameObject go = Instantiate(iceCubePrefab, grid.GetWorldPositionOfCellCenter(pos.x, pos.y), Quaternion.identity, levelObjectsParent);
            spawnedIceCubes[pos] = go;
        }

        // Plates
        foreach (var data in levelData.pressurePlates)
        {
            GameObject go = Instantiate(pressurePlatePrefab, grid.GetWorldPositionOfCellCenter(data.position.x, data.position.y), Quaternion.identity, levelObjectsParent);
            spawnedPlates[data.position] = go;
        }

        // Lift Gates
        foreach (var data in levelData.liftGates)
        {
            GameObject go = Instantiate(liftGatePrefab, grid.GetWorldPositionOfCellCenter(data.position.x, data.position.y), Quaternion.identity, levelObjectsParent);
            spawnedLiftGates[data.position] = go;
        }

        // Laser Gates
        foreach (var data in levelData.laserGates)
        {
            GameObject go = Instantiate(laserGatePrefab, grid.GetWorldPositionOfCellCenter(data.position.x, data.position.y), Quaternion.identity, levelObjectsParent);
            spawnedLaserGates[data.position] = go;
        }

        // Portals
        foreach (var data in levelData.portals)
        {
            Vector3 pos = grid.GetWorldPositionOfCellCenter(data.position.x, data.position.y);
            pos.y = 0.01f;
            GameObject go = Instantiate(portalPrefab, pos, Quaternion.identity, levelObjectsParent);
            
            var renderer = go.GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Color c = Color.white;
                switch (data.colorId)
                {
                    case PortalColor.Orange: c = new Color(1f, 0.5f, 0f); break;
                    case PortalColor.Cyan: c = Color.cyan; break;
                    case PortalColor.Magenta: c = Color.magenta; break;
                }
                renderer.material.color = c;
            }
            spawnedPortals[data.position] = go;
        }
    }

    // --- Handlers ---

    private void OnBoxMovedHandler(Vector2Int from, Vector2Int to)
    {
        if (spawnedBoxes.ContainsKey(from))
        {
            GameObject go = spawnedBoxes[from];
            spawnedBoxes.Remove(from);
            spawnedBoxes[to] = go;
            Vector3 targetPos = GameManager.Instance.grid.GetWorldPositionOfCellCenter(to.x, to.y);
            go.transform.DOMove(targetPos, boxMoveDuration).SetEase(Ease.Linear);
        }
    }

    private void OnIceCubeMovedHandler(Vector2Int from, Vector2Int to)
    {
        if (spawnedIceCubes.ContainsKey(from))
        {
            GameObject go = spawnedIceCubes[from];
            spawnedIceCubes.Remove(from);
            spawnedIceCubes[to] = go;
            Vector3 targetPos = GameManager.Instance.grid.GetWorldPositionOfCellCenter(to.x, to.y);
            go.transform.DOMove(targetPos, iceCubeMoveDuration).SetEase(Ease.Linear);
        }
    }

    private void OnFruitSpawnedHandler(FruitData data) => DrawFruitVisuals(data);

    private void OnExitRemovedHandler(Vector2Int pos)
    {
        if (spawnedExits.ContainsKey(pos)) { Destroy(spawnedExits[pos]); spawnedExits.Remove(pos); }
    }

    private void OnFruitEatenHandler(FruitData data)
    {
        if (spawnedFruits.ContainsKey(data.position)) { Destroy(spawnedFruits[data.position]); spawnedFruits.Remove(data.position); }
    }

    private void Instance_OnHoleFilled(Vector2Int holePos, Vector2Int fillerPos)
    {
        if (spawnedBoxes.ContainsKey(fillerPos)) { Destroy(spawnedBoxes[fillerPos]); spawnedBoxes.Remove(fillerPos); }
        else if (spawnedIceCubes.ContainsKey(fillerPos)) { Destroy(spawnedIceCubes[fillerPos]); spawnedIceCubes.Remove(fillerPos); }
        
        if (holePos != Vector2Int.zero && spawnedHoles.ContainsKey(holePos))
        {
            Destroy(spawnedHoles[holePos]);
            spawnedHoles.Remove(holePos);
        }
    }

    // Fixed: Correctly named handler for Laser Gates
    private void OnLaserGateStateChangedHandler(LaserGateData data, bool isActive)
    {
        if (spawnedLaserGates.TryGetValue(data.position, out GameObject go))
        {
            // Active = Beam Visible
            go.SetActive(isActive);
        }
    }

    // Fixed: Correctly named handler for Lift Gates
    private void OnLiftGateStateChangedHandler(LiftGateData data, bool isOpen)
    {
        if (spawnedLiftGates.TryGetValue(data.position, out GameObject go))
        {
            // Open = Gate Down/Invisible
            go.SetActive(!isOpen);
        }
    }

    private void OnPlateStateChangedHandler(PressurePlateData data, bool isActive)
    {
        if (spawnedPlates.TryGetValue(data.position, out GameObject go))
        {
            float targetY = isActive ? go.transform.position.y - 0.1f : 0f;
            go.transform.DOMoveY(targetY, plateMoveDuration).SetEase(Ease.OutQuad);
        }
    }

    private void ClearVisuals()
    {
        foreach (Transform child in levelObjectsParent) Destroy(child.gameObject);
        spawnedExits.Clear();
        spawnedFruits.Clear();
        spawnedBoxes.Clear();
        spawnedIceCubes.Clear();
        spawnedHoles.Clear();
        spawnedPlates.Clear();
        spawnedLiftGates.Clear();
        spawnedLaserGates.Clear();
        spawnedPortals.Clear();
    }

    private void DrawFruitVisuals(FruitData data)
    {
        Vector3 pos = GameManager.Instance.grid.GetWorldPositionOfCellCenter(data.position.x, data.position.y);
        GameObject go = Instantiate(fruitPrefab, pos, Quaternion.identity, levelObjectsParent);
        
        MeshRenderer[] renderers = go.GetComponentsInChildren<MeshRenderer>();
        if (data.colors.Count == 1)
        {
            Color c = GetEngineColor(data.colors[0]);
            foreach (var r in renderers) r.material.color = c;
        }
        else
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (i < data.colors.Count) renderers[i].material.color = GetEngineColor(data.colors[i]);
            }
        }
        spawnedFruits[data.position] = go;
    }

    private Color GetEngineColor(ColorType type)
    {
        switch (type)
        {
            case ColorType.Red: return Color.red;
            case ColorType.Blue: return Color.blue;
            case ColorType.Green: return Color.green;
            case ColorType.Yellow: return Color.yellow;
            default: return Color.white;
        }
    }
}