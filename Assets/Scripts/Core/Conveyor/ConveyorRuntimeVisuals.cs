using Conveyor;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class ConveyorRuntimeVisuals : MonoBehaviour
{
    [Header("Appearance")]
    public float markerScale = 0.18f;
    public int initialPool = 32;

    private readonly List<GameObject> pool = new();
    private Material matUnlit;
    private World _world;
    private EntityManager _entityManager;

    void Awake()
    {
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        if (!shader)
        {
            Debug.LogError("[VISUALS/Awake] КРИТИЧЕСКАЯ ОШИБКА: Шейдер не найден. Маркеры не будут отображаться.", this.gameObject);
            this.enabled = false;
            return;
        }
        matUnlit = new Material(shader);
        for (int i = 0; i < initialPool; i++) pool.Add(MakeMarker());
    }

    void OnEnable()
    {
        _world = World.DefaultGameObjectInjectionWorld;
        if (_world != null && _world.IsCreated)
        {
            _entityManager = _world.EntityManager;
        }
    }

    GameObject MakeMarker()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = matUnlit;
        go.transform.localScale = Vector3.one * markerScale;
        go.SetActive(false);
        return go;
    }

    void EnsurePool(int need)
    {
        while (pool.Count < need) pool.Add(MakeMarker());
    }

    public void SetMarker(int i, Vector3 pos, Color col)
    {
        EnsurePool(i + 1);
        var go = pool[i];
        if (!go.activeSelf) go.SetActive(true);
        go.transform.position = pos;
        go.GetComponent<Renderer>().material.color = col;
    }

    void HideMarkersFrom(int start)
    {
        for (int i = start; i < pool.Count; i++)
            if (pool[i].activeSelf) pool[i].SetActive(false);
    }

    void Update()
    {
        if (_world == null || !_world.IsCreated)
        {
            OnEnable();
            if (_world == null || !_world.IsCreated) return;
        }

        var em = _entityManager;

        bool inBuildMode = TryGet(em, out var gs, out var st);

        Entity hoveredConnector = Entity.Null;
        var hoverQuery = em.CreateEntityQuery(typeof(HoveredConnectorTag));
        if (!hoverQuery.IsEmptyIgnoreFilter)
        {
            hoveredConnector = hoverQuery.GetSingletonEntity();
        }
        hoverQuery.Dispose();

        using var q = em.CreateEntityQuery(ComponentType.ReadOnly<ConveyorConnector>(), ComponentType.ReadOnly<LocalToWorld>());
        using var entArr = q.ToEntityArray(Unity.Collections.Allocator.Temp);
        using var ltwArr = q.ToComponentDataArray<LocalToWorld>(Unity.Collections.Allocator.Temp);

        int usedMarkers = 0;
        for (int i = 0; i < entArr.Length; i++)
        {
            var e = entArr[i];

            if (em.HasComponent<Disabled>(e)) continue;

            var w = ltwArr[i].Position;
            Color c;

            if (inBuildMode && st.HasStart)
            {
                if (e == st.StartConnector)
                {
                    c = new Color(0.1f, 0.5f, 1f);
                }
                else if (e == hoveredConnector)
                {
                    c = Color.white;
                }
                else if (em.HasComponent<ConveyorConnectorHighlighted>(e))
                {
                    c = Color.green;
                }
                else
                {
                    c = Color.red;
                }
            }
            else
            {
                if (e == hoveredConnector)
                {
                    c = Color.white;
                }
                else
                {
                    var cc = em.GetComponentData<ConveyorConnector>(e);
                    c = cc.Type == ConveyorConnectorType.In ? Color.cyan :
                        cc.Type == ConveyorConnectorType.Out ? Color.yellow : new Color(0.8f, 0.8f, 0.8f);
                }
            }
            SetMarker(usedMarkers++, w, c);
        }

        HideMarkersFrom(usedMarkers);
    }

    bool TryGet(EntityManager em, out Entity gs, out ConveyorState st)
    {
        gs = default;
        st = default;

        var query = em.CreateEntityQuery(typeof(GameState));
        if (query.IsEmptyIgnoreFilter)
        {
            query.Dispose();
            return false;
        }

        gs = query.GetSingletonEntity();
        query.Dispose();

        if (!em.HasComponent<InConveyorMode>(gs) || !em.HasComponent<ConveyorState>(gs)) return false;

        st = em.GetComponentData<ConveyorState>(gs);
        return true;
    }
}