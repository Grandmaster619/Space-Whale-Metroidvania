using System.Collections.Generic;
using UnityEngine;

struct VisionCandidate
{
    public Collider2D collider;
    public float sqrDistance;
    public Vector2 dir;
}

public class VisionManager : MonoBehaviour
{
    [Header("Grid Settings")]
    public float cellSize = 10f;
    public LayerMask targetMask;
    public LayerMask occluderMask;

    [Header("Manager")]
    [SerializeField] int creaturesPerFrame = 24;

    readonly List<Eyesight> eyes = new List<Eyesight>();
    readonly Dictionary<Vector2Int, List<Collider2D>> grid = new Dictionary<Vector2Int, List<Collider2D>>();
    //Dictionary<Layers, Dictionary<Vector2Int, List<Collider2D>>> layerGrid = new();
    readonly Dictionary<Collider2D, Vector2Int> colliderToCell = new Dictionary<Collider2D, Vector2Int>();

    // Struct-based candidate buffer
    readonly VisionCandidate[] candidateBuffer = new VisionCandidate[64];
    int candidateCount = 0;

    int currentIndex = 0;


    public static VisionManager Instance;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        RegisterAllTargetsInScene();
    }

    public static void Register(Eyesight e) { if (!Instance.eyes.Contains(e)) Instance.eyes.Add(e); }
    public static void Unregister(Eyesight e) { if (Instance.eyes.Contains(e)) Instance.eyes.Remove(e); }

    public void RegisterTarget(Collider2D col)
    {
        if (col == null || (targetMask.value & (1 << col.gameObject.layer)) == 0 || colliderToCell.ContainsKey(col))
            return;

        Vector2Int cell = WorldToCell(col.transform.position);
        if (!grid.TryGetValue(cell, out var list))
        {
            list = new List<Collider2D>(8);
            grid[cell] = list;
        }
        list.Add(col);
        colliderToCell[col] = cell;
    }

    public void UnregisterTarget(Collider2D col)
    {
        if (col == null || !colliderToCell.TryGetValue(col, out var cell)) return;
        if (grid.TryGetValue(cell, out var list))
        {
            list.Remove(col);
            if (list.Count == 0) grid.Remove(cell);
        }
        colliderToCell.Remove(col);
    }

    void RegisterAllTargetsInScene()
    {
        var all = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
            if ((targetMask.value & (1 << all[i].gameObject.layer)) != 0)
                RegisterTarget(all[i]);
    }

    void LateUpdate()
    {
        if (eyes.Count == 0) return;
        UpdateGridIncremental();

        int count = Mathf.Min(creaturesPerFrame, eyes.Count);
        for (int i = 0; i < count; i++)
        {
            if (currentIndex >= eyes.Count) currentIndex = 0;
            ProcessVision(eyes[currentIndex]);
            currentIndex++;
        }
    }

    void UpdateGridIncremental()
    {
        var keys = new List<Collider2D>(colliderToCell.Keys);
        foreach (var col in keys)
        {
            if (col == null) { colliderToCell.Remove(col); continue; }
            Vector2Int prevCell = colliderToCell[col];
            Vector2Int newCell = WorldToCell(col.transform.position);
            if (newCell == prevCell) continue;

            if (grid.TryGetValue(prevCell, out var oldList))
            {
                oldList.Remove(col);
                if (oldList.Count == 0) grid.Remove(prevCell);
            }

            if (!grid.TryGetValue(newCell, out var newList))
            {
                newList = new List<Collider2D>(8);
                grid[newCell] = newList;
            }
            newList.Add(col);
            colliderToCell[col] = newCell;
        }
    }

    void ProcessVision(Eyesight e)
    {
        e.GetEye(out Vector2 eyePos, out Vector2 forward);

        int cellRadius = Mathf.CeilToInt(e.viewRadius / cellSize);
        Vector2Int eyeCell = WorldToCell(eyePos);

        candidateCount = 0;

        // gather candidates from nearby cells
        for (int dx = -cellRadius; dx <= cellRadius; dx++)
        {
            for (int dy = -cellRadius; dy <= cellRadius; dy++)
            {
                Vector2Int cell = new Vector2Int(eyeCell.x + dx, eyeCell.y + dy);
                if (!grid.TryGetValue(cell, out var list)) continue;

                for (int j = 0; j < list.Count; j++)
                {
                    Collider2D c = list[j];
                    if (c == null || c == e.ownCollider || c.gameObject == e.gameObject) continue;

                    Vector2 to = (Vector2)c.bounds.center - eyePos;
                    float sqr = to.sqrMagnitude;
                    if (sqr > e.viewRadius * e.viewRadius) continue;

                    Vector2 dir = to / Mathf.Sqrt(sqr);
                    if (Vector2.Dot(forward, dir) < e.cosHalfFov) continue;

                    if (candidateCount < candidateBuffer.Length)
                    {
                        candidateBuffer[candidateCount] = new VisionCandidate { collider = c, sqrDistance = sqr, dir = dir };
                        candidateCount++;
                    }
                }
            }
        }

        int found = 0;

        for (int i = 0; i < candidateCount && found < e.maxTrackedTargets; i++)
        {
            VisionCandidate vc = candidateBuffer[i];
            if (Physics2D.Raycast(eyePos, vc.dir, Mathf.Sqrt(vc.sqrDistance), e.occluderMask)) continue;

            e.Memory.HandleMemory(vc.collider);
            Debug.DrawLine(
                new Vector3(eyePos.x, eyePos.y, e.transform.position.z), 
                new Vector3(vc.collider.bounds.center.x, vc.collider.bounds.center.y, e.transform.position.z), 
                Color.yellow
            );
            found++;
        }

        DebugDrawVision(e, new Vector3(eyePos.x, eyePos.y, e.transform.position.z), forward);
    }

    void DebugDrawVision(Eyesight e, Vector3 eyePos, Vector2 forward)
    {
        //int segs = 20;
        float startAngle = Mathf.Atan2(forward.y, forward.x) * Mathf.Rad2Deg - e.fov * 0.5f;
        //float segAngle = e.fov / segs;
        /*Vector2 prev = eyePos + AngleToVector2(startAngle) * e.viewRadius;

        for (int i = 1; i <= segs; i++)
        {
            float angle = startAngle + segAngle * i;
            Vector2 next = eyePos + AngleToVector2(angle) * e.viewRadius;
            Debug.DrawLine(prev, next, Color.cyan);
            prev = next;
        }*/

        Vector3 left = AngleToVector2(startAngle);
        Vector3 right = AngleToVector2(startAngle + e.fov);
        Debug.DrawLine(eyePos, eyePos + left * e.viewRadius, Color.green);
        Debug.DrawLine(eyePos, eyePos + right * e.viewRadius, Color.green);
    }

    static Vector2 AngleToVector2(float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(r), Mathf.Sin(r));
    }

    Vector2Int WorldToCell(Vector2 pos) =>
        new Vector2Int(Mathf.FloorToInt(pos.x / cellSize), Mathf.FloorToInt(pos.y / cellSize));
}