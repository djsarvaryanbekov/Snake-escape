using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class SnakeVisualizer : MonoBehaviour
{
    [Header("Prefabs")]
    [SerializeField] private GameObject headPrefab;
    [SerializeField] private GameObject bodyPrefab;
    [SerializeField] private GameObject tailPrefab;

    [Header("Animation Settings")]
    [SerializeField] private float moveDuration = 0.15f;

    private Snake logicalSnake;
    private List<GameObject> visualSegments = new List<GameObject>();
    private bool isRedrawing = false;
    public bool IsAnimating { get; private set; } = false;

    private Dictionary<Renderer, Color> originalColors = new Dictionary<Renderer, Color>();

    public void Initialize(Snake snakeToVisualize)
    {
        this.logicalSnake = snakeToVisualize;
        logicalSnake.OnMoved += AnimateMove;
        logicalSnake.OnGrew += AnimateGrowth;
        logicalSnake.OnRemoved += AnimateRemoval;
        logicalSnake.OnSliced += OnSlicedHandler;
        FullRedraw();
    }

    public void ToggleSelectionVisuals(bool isSelected, SnakeEnd activePart)
    {
        if (originalColors.Count == 0)
        {
            foreach (var segment in visualSegments)
            {
                var r = segment.GetComponentInChildren<Renderer>();
                if (r != null) originalColors[r] = r.material.color;
            }
        }

        for (int i = 0; i < visualSegments.Count; i++)
        {
            var segment = visualSegments[i];
            var renderer = segment.GetComponentInChildren<Renderer>();
            if (renderer == null) continue;

            bool shouldHighlight = false;
            if (isSelected)
            {
                if (i == 0 && activePart == SnakeEnd.Head) shouldHighlight = true;
                else if (i == visualSegments.Count - 1 && activePart == SnakeEnd.Tail) shouldHighlight = true;
            }

            if (shouldHighlight)
            {
                renderer.material.color = Color.white;
                segment.transform.localScale = Vector3.one * 1.2f;
            }
            else
            {
                if (originalColors.ContainsKey(renderer)) renderer.material.color = originalColors[renderer];
                segment.transform.localScale = Vector3.one;
            }
        }
    }
    private void AnimateMove()
    {
        if (IsAnimating || isRedrawing) return;
        IsAnimating = true;

        var snakeBody = logicalSnake.Body;
        if (visualSegments.Count != snakeBody.Count) { FullRedraw(); IsAnimating = false; return; }

        Sequence moveSequence = DOTween.Sequence();

        for (int i = 0; i < snakeBody.Count; i++)
        {
            Vector3 currentPos = visualSegments[i].transform.position;
            Vector3 targetPos = GameManager.Instance.grid.GetWorldPositionOfCellCenter(snakeBody[i].x, snakeBody[i].y);
            targetPos.y = 0.1f;

            // --- FIXED HEAD ROTATION LOGIC ---
            if (i == 0 && snakeBody.Count > 1) 
            {
                // Instead of movement direction, look away from the NECK (Segment 1)
                Vector3 neckPos = GameManager.Instance.grid.GetWorldPositionOfCellCenter(snakeBody[1].x, snakeBody[1].y);
                Vector3 lookDir = targetPos - neckPos;
                
                // Keep y=0 to prevent tilting
                lookDir.y = 0;

                if (lookDir != Vector3.zero) 
                {
                    Quaternion targetRotation = Quaternion.LookRotation(lookDir);
                    moveSequence.Join(visualSegments[i].transform.DORotateQuaternion(targetRotation, moveDuration));
                }
            }
            // ---------------------------------

            if (Vector3.Distance(currentPos, targetPos) > 2.0f)
            {
                visualSegments[i].transform.position = targetPos; // Snap
            }
            else
            {
                moveSequence.Join(visualSegments[i].transform.DOMove(targetPos, moveDuration));
            }
        }

        moveSequence.OnComplete(() => { IsAnimating = false; });
    }

    private void AnimateGrowth()
    {
        if (IsAnimating || isRedrawing) { StartCoroutine(DelayedGrowthAnimation()); return; }
        isRedrawing = true;
        FullRedraw();
        isRedrawing = false;
        AnimateMove();
    }

    private System.Collections.IEnumerator DelayedGrowthAnimation()
    {
        yield return new WaitUntil(() => !IsAnimating && !isRedrawing);
        AnimateGrowth();
    }

    private void OnSlicedHandler()
    {
        transform.DOKill();
        IsAnimating = false;
        FullRedraw();
    }

    private void AnimateRemoval()
    {
        foreach (var segment in visualSegments) Destroy(segment);
        visualSegments.Clear();
    }

    private void FullRedraw()
    {
        foreach (var segment in visualSegments) Destroy(segment);
        visualSegments.Clear();
        originalColors.Clear();

        var snakeBody = logicalSnake.Body;
        if (snakeBody.Count == 0) return;

        for (int i = 0; i < snakeBody.Count; i++)
        {
            GameObject segmentPrefab = (i == 0) ? headPrefab : (i == snakeBody.Count - 1) ? tailPrefab : bodyPrefab;
            GameObject segment = Instantiate(segmentPrefab, transform);

            Vector3 worldPos = GameManager.Instance.grid.GetWorldPositionOfCellCenter(snakeBody[i].x, snakeBody[i].y);
            worldPos.y = 0.1f;
            segment.transform.position = worldPos;

            Renderer segmentRenderer = segment.GetComponentInChildren<Renderer>();
            if (segmentRenderer != null) segmentRenderer.material.color = GetColorFromEnum(logicalSnake.Color);

            visualSegments.Add(segment);
        }
    }

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

    private void OnDestroy()
    {
        if (logicalSnake != null)
        {
            logicalSnake.OnMoved -= AnimateMove;
            logicalSnake.OnGrew -= AnimateGrowth;
            logicalSnake.OnRemoved -= AnimateRemoval;
            logicalSnake.OnSliced -= OnSlicedHandler;
        }
    }
}