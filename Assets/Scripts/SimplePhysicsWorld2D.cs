using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class SimplePhysicsWorld2D : MonoBehaviour
{
    public static SimplePhysicsWorld2D Instance { get; private set; }
    public readonly HashSet<StaticRectCollider2D> Colliders = new();
    public readonly HashSet<HazardRect2D> Hazards = new();

    void Awake() { Instance = this; }

    public void Register(StaticRectCollider2D c) { if (c) Colliders.Add(c); }
    public void Unregister(StaticRectCollider2D c) { if (c) Colliders.Remove(c); }

    public void Register(HazardRect2D h) { if (h) Hazards.Add(h); }
    public void Unregister(HazardRect2D h) { if (h) Hazards.Remove(h); }

    public void Refresh()
    {
        Colliders.Clear();
        Hazards.Clear();
#if UNITY_2023_1_OR_NEWER
        Colliders.UnionWith(FindObjectsByType<StaticRectCollider2D>(FindObjectsSortMode.None));
        Hazards.UnionWith(FindObjectsByType<HazardRect2D>(FindObjectsSortMode.None));
#else
        Colliders.UnionWith(FindObjectsOfType<StaticRectCollider2D>());
        Hazards.UnionWith(FindObjectsOfType<HazardRect2D>());
#endif
    }
}
