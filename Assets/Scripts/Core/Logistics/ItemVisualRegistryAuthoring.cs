using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Плоская запись реестра: ItemID -> EntityPrefab (используется рантаймом).
/// Каждая запись живёт на своей сущности.
/// </summary>
public struct ItemVisualPrefabReference : IComponentData
{
    public int ItemID;
    public Entity EntityPrefab;
}

/// <summary>
/// Authoring-реестр визуалов предметов.
/// Позволяет:
/// 1) Подобрать визуал непосредственно из ScriptableObject Item (VisualPrefab),
/// 2) Либо задать вручную пары ItemID → GameObject префаба.
/// В процессе выпечки создаётся отдельная сущность на каждую пару.
/// </summary>
public class ItemVisualRegistryAuthoring : MonoBehaviour
{
    [System.Serializable]
    public class FromItemAsset
    {
        [Tooltip("Item (ScriptableObject) с заполненным itemID и VisualPrefab")]
        public Item Item;

        [Tooltip("Необязательно. Если задан — переопределит Item.VisualPrefab")]
        public GameObject VisualOverride;
    }

    [System.Serializable]
    public class ManualBinding
    {
        public int itemID;
        public GameObject VisualPrefab;
    }

    [Header("Выбор из Item (ScriptableObject)")]
    public FromItemAsset[] FromItems;

    [Header("Ручные переопределения (по ItemID)")]
    public ManualBinding[] ManualOverrides;

    [Tooltip("Логировать конфликты/пропуски во время выпечки")]
    public bool Verbose = false;

    class Baker : Baker<ItemVisualRegistryAuthoring>
    {
        public override void Bake(ItemVisualRegistryAuthoring a)
        {
            // Собираем финальную таблицу: последний записанный источник побеждает
            var map = new Dictionary<int, GameObject>();

            // 1) Из ScriptableObject Item
            if (a.FromItems != null)
            {
                foreach (var e in a.FromItems)
                {
                    if (e == null || e.Item == null) continue;
                    var id = e.Item.itemID;
                    var go = e.VisualOverride != null ? e.VisualOverride : e.Item.VisualPrefab;
                    if (id <= 0 || go == null)
                    {
                        if (a.Verbose)
                            Debug.LogWarning("[ItemVisualRegistry/Bake] Skip FromItem: id=" + id + ", go=" + (go != null ? go.name : "NULL"), a);
                        continue;
                    }
                    map[id] = go;
                }
            }

            // 2) Ручные переопределения
            if (a.ManualOverrides != null)
            {
                foreach (var m in a.ManualOverrides)
                {
                    if (m == null || m.itemID <= 0 || m.VisualPrefab == null)
                    {
                        if (a.Verbose)
                            Debug.LogWarning("[ItemVisualRegistry/Bake] Skip Manual: id=" + (m != null ? m.itemID : -1) + ", go=" + (m != null && m.VisualPrefab != null ? m.VisualPrefab.name : "NULL"), a);
                        continue;
                    }
                    map[m.itemID] = m.VisualPrefab; // override
                }
            }

            // 3) На каждую пару — ОТДЕЛЬНАЯ запись-сущность
            foreach (var kv in map)
            {
                var prefabGO = kv.Value;
                var prefabEntity = GetEntity(prefabGO, TransformUsageFlags.Dynamic); // корректный префаб-entity для рантайма

                var entryEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                AddComponent(entryEntity, new ItemVisualPrefabReference
                {
                    ItemID = kv.Key,
                    EntityPrefab = prefabEntity
                });

                if (a.Verbose)
                    Debug.Log("[ItemVisualRegistry/Bake] + " + kv.Key + " → " + prefabGO.name, a);
            }
        }
    }
}
