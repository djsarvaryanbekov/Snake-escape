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
        GameManager.Instance.OnLiftGateStateChanged += OnLiftGateStateChangedHandler;
        GameManager.Instance.OnLaserGateStateChanged += OnLaserGateStateChangedHandler;
        GameManager.Instance.OnPlateStateChanged += OnPlateStateChangedHandler;
        GameManager.Instance.OnPortalStateChanged += OnPortalStateChangedHandler;
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
            GameManager.Instance.OnLiftGateStateChanged -= OnLiftGateStateChangedHandler;
            GameManager.Instance.OnLaserGateStateChanged -= OnLaserGateStateChangedHandler;
            GameManager.Instance.OnPlateStateChanged -= OnPlateStateChangedHandler;
            GameManager.Instance.OnPortalStateChanged -= OnPortalStateChangedHandler;
        }
    }

    private void DrawLevelVisuals(Level_SO levelData)
    {
        ClearVisuals();
        var grid = GameManager.Instance.grid;

        foreach (var wallPos in levelData.wallPositions)
            Instantiate(wallPrefab, grid.GetWorldPositionOfCellCenter(wallPos.x, wallPos.y), Quaternion.identity, levelObjectsParent);

        foreach (var holePos in levelData.holePositions)
            spawnedHoles[holePos] = Instantiate(holePrefab, grid.GetWorldPositionOfCellCenter(holePos.x, holePos.y), Quaternion.identity, levelObjectsParent);

        foreach (var exitData in levelData.exits)
            spawnedExits[exitData.position] = Instantiate(exitPrefab, grid.GetWorldPositionOfCellCenter(exitData.position.x, exitData.position.y), Quaternion.identity, levelObjectsParent);

        foreach (var fruitData in levelData.fruits) DrawFruitVisuals(fruitData);

        foreach (var pos in levelData.boxPositions)
            spawnedBoxes[pos] = Instantiate(boxPrefab, grid.GetWorldPositionOfCellCenter(pos.x, pos.y), Quaternion.identity, levelObjectsParent);

        foreach (var pos in levelData.iceCubePositions)
            spawnedIceCubes[pos] = Instantiate(iceCubePrefab, grid.GetWorldPositionOfCellCenter(pos.x, pos.y), Quaternion.identity, levelObjectsParent);

        foreach (var data in levelData.pressurePlates)
            spawnedPlates[data.position] = Instantiate(pressurePlatePrefab, grid.GetWorldPositionOfCellCenter(data.position.x, data.position.y), Quaternion.identity, levelObjectsParent);

        foreach (var data in levelData.liftGates)
            spawnedLiftGates[data.position] = Instantiate(liftGatePrefab, grid.GetWorldPositionOfCellCenter(data.position.x, data.position.y), Quaternion.identity, levelObjectsParent);

        foreach (var data in levelData.laserGates)
            spawnedLaserGates[data.position] = Instantiate(laserGatePrefab, grid.GetWorldPositionOfCellCenter(data.position.x, data.position.y), Quaternion.identity, levelObjectsParent);

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

    private void OnBoxMovedHandler(Vector2Int from, Vector2Int to)
    {
        if (spawnedBoxes.ContainsKey(from))
        {
            GameObject go = spawnedBoxes[from];
            spawnedBoxes.Remove(from);
            spawnedBoxes[to] = go;
            
            Vector3 targetPos = GameManager.Instance.grid.GetWorldPositionOfCellCenter(to.x, to.y);
            float dist = Vector3.Distance(go.transform.position, targetPos);
            
            if (dist > GameManager.Instance.grid.GetCellSize() * 1.5f)
                go.transform.position = targetPos; // Snap (Teleport)
            else
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
            float dist = Vector3.Distance(go.transform.position, targetPos);
            
            if (dist > GameManager.Instance.grid.GetCellSize() * 1.5f)
                go.transform.position = targetPos; // Snap (Teleport)
            else
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

    private void OnLaserGateStateChangedHandler(LaserGateData data, bool isActive)
    {
        if (spawnedLaserGates.TryGetValue(data.position, out GameObject go)) go.SetActive(isActive);
    }

    private void OnLiftGateStateChangedHandler(LiftGateData data, bool isOpen)
    {
        if (spawnedLiftGates.TryGetValue(data.position, out GameObject go)) go.SetActive(!isOpen);
    }

    private void OnPlateStateChangedHandler(PressurePlateData data, bool isActive)
    {
        if (spawnedPlates.TryGetValue(data.position, out GameObject go))
        {
            float targetY = isActive ? go.transform.position.y - 0.1f : 0f;
            go.transform.DOMoveY(targetY, plateMoveDuration).SetEase(Ease.OutQuad);
        }
    }

    private void OnPortalStateChangedHandler(PortalData data, bool isActive)
    {
        if (spawnedPortals.TryGetValue(data.position, out GameObject go))
        {
            if (go.transform.childCount > 0)
                go.transform.GetChild(1).gameObject.SetActive(isActive);
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
        
        // Get all renderers (MeshRenderer) in the prefab
        MeshRenderer[] renderers = go.GetComponentsInChildren<MeshRenderer>();
        
        // CASE 1: Single Color Fruit (Red, Green, Blue)
        // We want to color the WHOLE object this color, regardless of how many parts it has.
        if (data.colors.Count == 1)
        {
            Color singleColor = GetEngineColor(data.colors[0]);
            foreach (var r in renderers)
            {
                r.material.color = singleColor;
            }
        }
        // CASE 2: Multi-Color Fruit (Red-Green, Blue-Yellow)
        // We assign colors sequentially to the parts.
        else
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                // If we have a color defined for this part index, use it.
                if (i < data.colors.Count)
                {
                    renderers[i].material.color = GetEngineColor(data.colors[i]);
                }
                else
                {
                    // If the prefab has more parts than colors (e.g. 3 parts, 2 colors),
                    // color the extra parts with the last available color.
                    renderers[i].material.color = GetEngineColor(data.colors[data.colors.Count - 1]);
                }
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