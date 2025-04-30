using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance;

    [Header("Settings")]
    private LayerMask buildableSurface;
    public UnityEngine.Material validPlacementMaterial;
    public UnityEngine.Material invalidPlacementMaterial;

    private GameObject currentBuildingPrefab;
    private GameObject buildingPreview;
    private bool isInBuildingMode = false;

    // ECS системы
    private BuildPhysicsWorld buildPhysicsWorld;
    private CollisionWorld collisionWorld;

    private void Awake()
    {
        Instance = this;

        // Устанавливаем маску только для слоя Terrain
        buildableSurface = LayerMask.GetMask("Terrain");
    }

    private void Start()
    {
        // Инициализация ECS систем
        var entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
        PhysicsWorldSingleton physicsWorld = query.GetSingleton<PhysicsWorldSingleton>();
        collisionWorld = physicsWorld.CollisionWorld;

        Debug.Log($"Текущее значение buildableSurface: {buildableSurface.value}");
    }

    public void StartBuildingMode(Item buildingItem)
    {
        currentBuildingPrefab = buildingItem.buildingPrefab;

        // Создаем объект предпоказа
        buildingPreview = Instantiate(currentBuildingPrefab);
        buildingPreview.transform.position = Vector3.zero;

        // Отключаем коллайдеры для предпоказа
        foreach (var collider in buildingPreview.GetComponentsInChildren<UnityEngine.Collider>())
        {
            collider.enabled = false;
        }

        // Устанавливаем материалы для предпоказа
        SetPreviewMaterials(buildingPreview, validPlacementMaterial);

        isInBuildingMode = true;
        Debug.Log("Режим строительства активирован.");
    }

    private void Update()
    {
        if (!isInBuildingMode) return;

        // Движение предпоказа за курсором
        UnityEngine.Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        bool hitDetected = false;
        Vector3 hitPoint = Vector3.zero;

        // Классический Raycast
        if (Physics.Raycast(ray, out UnityEngine.RaycastHit hit, 100f, buildableSurface))
        {
            hitDetected = true;
            hitPoint = hit.point;
            Debug.Log($"Raycast попал в объект: {hit.collider.gameObject.name}, слой: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
        }
        else
        {
            // ECS Raycast
            RaycastInput raycastInput = new RaycastInput
            {
                Start = ray.origin,
                End = ray.origin + ray.direction * 100f,
                Filter = CollisionFilter.Default
            };

            if (collisionWorld.CastRay(raycastInput, out Unity.Physics.RaycastHit ecsHit))
            {
                hitDetected = true;
                hitPoint = ecsHit.Position;
                Debug.Log($"ECS Raycast попал в объект с Entity: {ecsHit.Entity.Index}");
            }
        }

        if (hitDetected)
        {
            buildingPreview.transform.position = hitPoint; // Обновляем позицию предпоказа

            // Проверка валидности позиции
            bool isValidPosition = CheckBuildPosition(hitPoint);
            SetPreviewMaterials(buildingPreview, isValidPosition ? validPlacementMaterial : invalidPlacementMaterial);

            // ЛКМ - построить
            if (Input.GetMouseButtonDown(0) && isValidPosition)
            {
                Build(hitPoint);
            }
        }
        else
        {
            Debug.LogWarning("Raycast не попал в объект с подходящим слоем.");
        }

        // ПКМ - отмена
        if (Input.GetMouseButtonDown(1))
        {
            CancelBuilding();
        }
    }

    private bool CheckBuildPosition(Vector3 position)
    {
        if (currentBuildingPrefab == null)
        {
            Debug.LogError("currentBuildingPrefab не установлен!");
            return false;
        }

        // Проверяем все коллайдеры в префабе
        foreach (var collider in currentBuildingPrefab.GetComponentsInChildren<UnityEngine.Collider>())
        {
            if (collider is UnityEngine.BoxCollider boxCollider)
            {
                // Проверка для BoxCollider
                if (Physics.OverlapBox(position + boxCollider.center, boxCollider.size / 2, Quaternion.identity).Length > 0)
                {
                    return false;
                }
            }
            else if (collider is UnityEngine.SphereCollider sphereCollider)
            {
                // Проверка для SphereCollider
                if (Physics.OverlapSphere(position, sphereCollider.radius, buildableSurface).Length > 0)
                {
                    return false;
                }
            }
            else if (collider is UnityEngine.CapsuleCollider capsuleCollider)
            {
                // Проверка для CapsuleCollider
                Vector3 point1 = position + Vector3.up * (capsuleCollider.height / 2 - capsuleCollider.radius);
                Vector3 point2 = position - Vector3.up * (capsuleCollider.height / 2 - capsuleCollider.radius);
                if (Physics.OverlapCapsule(point1, point2, capsuleCollider.radius, buildableSurface).Length > 0)
                {
                    return false;
                }
            }
            else if (collider is UnityEngine.MeshCollider meshCollider && meshCollider.convex)
            {
                // Проверка для MeshCollider (только если он convex)
                if (Physics.OverlapBox(position, meshCollider.bounds.extents, Quaternion.identity, buildableSurface).Length > 0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void Build(Vector3 position)
    {
        if (Inventory.Instance.selectedItem != null &&
            Inventory.Instance.selectedItem.itemType == ItemType.Building)
        {
            Instantiate(currentBuildingPrefab, position, Quaternion.identity);
            Inventory.Instance.Remove(Inventory.Instance.selectedItem); // Удаляем предмет из инвентаря
            ExitBuildingMode();
        }
    }

    public void CancelBuilding()
    {
        ExitBuildingMode();
    }

    private void ExitBuildingMode()
    {
        if (buildingPreview != null)
        {
            Destroy(buildingPreview);
        }
        isInBuildingMode = false;
        Debug.Log("Режим строительства отменен.");
    }

    private void SetPreviewMaterials(GameObject target, UnityEngine.Material material)
    {
        foreach (var renderer in target.GetComponentsInChildren<Renderer>())
        {
            var materials = new UnityEngine.Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = material;
            }
            renderer.sharedMaterials = materials;
        }
    }
}
