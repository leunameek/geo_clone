using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)] // esto hace que este script se ejecute muuuy temprano en el orden
public class SimplePhysicsWorld2D : MonoBehaviour
{
    public static SimplePhysicsWorld2D Instance { get; private set; }
    // patrón tipo singletoon
    // básicamente solo va a existir UN mundo de física 2D simple
    // y lo accedemos como SimplePhysicsWorld2D.Instance desde otros scripts

    public readonly HashSet<StaticRectCollider2D> Colliders = new();
    public readonly HashSet<HazardRect2D>        Hazards   = new();
    public readonly HashSet<GoalRect2D>          Goals     = new();

    // Usamos HashSet en vez de List:
    //  - no permite duplicados
    //  - Add/Remove/Contains son O(1) aprox en promedio
    //  - ideal para "registro" de cosas activas en la escena

    void Awake() 
    { 
        // cuando despierta, nos auto-asignamos como instancia global
        // si hubiera dos mundos, el último en hacer Awake ganaria (no deberia pasar)
        Instance = this; 
    }

    // ----- Registro de colliders estaticos -----
    public void Register(StaticRectCollider2D c) 
    { 
        if (c) Colliders.Add(c); // si no es null, lo metemos al set
    }

    public void Unregister(StaticRectCollider2D c)
    { 
        if (c) Colliders.Remove(c); // lo sacamos del set si estaba
    }

    // ----- Registro de hazards (peligros) -----
    public void Register(HazardRect2D h) 
    { 
        if (h) Hazards.Add(h); 
    }

    public void Unregister(HazardRect2D h) 
    { 
        if (h) Hazards.Remove(h); 
    }

    // ----- Registro de metas / goals -----
    public void Register(GoalRect2D g) 
    { 
        if (g) Goals.Add(g); 
    }

    public void Unregister(GoalRect2D g) 
    { 
        if (g) Goals.Remove(g); 
    }

    // Esta función sirve como “rescanear todo el mundo”
    // por si algo se des-sincroniza, o quieres reconstruir la lista
    public void Refresh()
    {
        // limpiamos todo
        Colliders.Clear(); 
        Hazards.Clear(); 
        Goals.Clear();

#if UNITY_2023_1_OR_NEWER
        // En versiones nuevas de Unity usamos FindObjectsByType
        // que básicamente hace: “busca todos los objetos en la escena que tengan este componente”
        Colliders.UnionWith(FindObjectsByType<StaticRectCollider2D>(FindObjectsSortMode.None));
        Hazards  .UnionWith(FindObjectsByType<HazardRect2D>(FindObjectsSortMode.None));
        Goals    .UnionWith(FindObjectsByType<GoalRect2D>(FindObjectsSortMode.None));
#else
        // En versiones más viejitas, usamos el clásico FindObjectsOfType
        Colliders.UnionWith(FindObjectsOfType<StaticRectCollider2D>());
        Hazards  .UnionWith(FindObjectsOfType<HazardRect2D>());
        Goals    .UnionWith(FindObjectsOfType<GoalRect2D>());
#endif

        // UnionWith básicamente hace: set = set ∪ {todos los encontrados}
        // pero como el set estaba limpio, es como hacer “copia todo” sin repetidos.
    }
}
