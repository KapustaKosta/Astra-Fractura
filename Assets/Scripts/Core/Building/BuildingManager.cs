using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Physics.Systems;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Collections;
using System;
using UnityEngine.Rendering;
using Unity.VisualScripting;

public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance;

    private BatchMaterialID validMaterialID;
    private BatchMaterialID invalidMaterialID;

    [Header("Settings")]
    private LayerMask buildableSurface;
    public UnityEngine.Material validPlacementMaterial;
    public UnityEngine.Material invalidPlacementMaterial;

    private EntityManager entityManager;
    private Entity currentBuildingPrefab;
    private Entity buildingPreview;
    private bool isInBuildingMode = false;

    // ECS системы
    private CollisionWorld collisionWorld;

    private PhysicsCollider originalCollider;

    private void Awake()
    {
        Instance = this;

        // Устанавливаем маску только для слоя Terrain
        buildableSurface = LayerMask.GetMask("Terrain");
    }

    private void Start()
    {
        // Инициализация ECS систем
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityQuery query = entityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
        PhysicsWorldSingleton physicsWorld = query.GetSingleton<PhysicsWorldSingleton>();
        collisionWorld = physicsWorld.CollisionWorld;

        Debug.Log($"Текущее значение buildableSurface: {buildableSurface.value}");

        // Регистрируем материалы
        validMaterialID = RegisterMaterial(validPlacementMaterial);
        invalidMaterialID = RegisterMaterial(invalidPlacementMaterial);
    }

    private BatchMaterialID RegisterMaterial(UnityEngine.Material material)
    {
        var materialMeshSystem = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<EntitiesGraphicsSystem>();
        return materialMeshSystem.RegisterMaterial(material);
    }

    public void StartBuildingMode(Item buildingItem)
    {
        currentBuildingPrefab = ItemToEntityResolver.GetEntityPrefabFromID(entityManager, buildingItem.itemID);

        // Создаем объект предпоказа
        buildingPreview = entityManager.Instantiate(currentBuildingPrefab);
        entityManager.SetComponentData(buildingPreview, LocalTransform.FromPosition(float3.zero));

        // Отключаем PhysicsCollider для предпоказа
        if (entityManager.HasComponent<PhysicsCollider>(buildingPreview))
        {
            originalCollider = entityManager.GetComponentData<PhysicsCollider>(buildingPreview);
            entityManager.RemoveComponent<PhysicsCollider>(buildingPreview);
        }

        // Устанавливаем материалы для предпоказа
        SetPreviewMaterial(buildingPreview, true);

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
            //Debug.Log($"Raycast попал в объект: {hit.collider.gameObject.name}, слой: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
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
            // Обновляем позицию предпоказа
            entityManager.SetComponentData(buildingPreview, LocalTransform.FromPosition(hitPoint));

            // Проверка валидности позиции
            bool isValidPosition = CheckBuildPosition(hitPoint);
            SetPreviewMaterial(buildingPreview, isValidPosition);

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
        bool result = true;

        if (currentBuildingPrefab == Entity.Null)
        {
            Debug.LogError("currentBuildingPrefab не установлен!");
            return false;
        }

        if (!entityManager.HasComponent<PhysicsCollider>(currentBuildingPrefab))
        {
            Debug.LogError("Entity не содержит PhysicsCollider!");
            return false;
        }

        var colliderComponent = entityManager.GetComponentData<PhysicsCollider>(currentBuildingPrefab);
        var colliderPtr = colliderComponent.Value;

        if (!colliderPtr.IsCreated)
        {
            Debug.LogError("Collider не создан.");
            return false;
        }

        ref var collider = ref colliderPtr.Value;

        var input = new OverlapAabbInput
        {
            Aabb = collider.CalculateAabb(new RigidTransform
            {
                pos = position,
                rot = quaternion.identity
            }),
            Filter = collider.GetCollisionFilter()
        };

        NativeList<int> hitResults = new NativeList<int>(Allocator.Temp);

        EntityQuery query = entityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
        PhysicsWorldSingleton physicsWorld = query.GetSingleton<PhysicsWorldSingleton>();
        collisionWorld = physicsWorld.CollisionWorld;

        try
        {
            if (collisionWorld.OverlapAabb(input, ref hitResults))
            {
                Debug.Log($"Найдено пересечений: {hitResults.Length}");
                result = false;
            }
        }
        finally
        {
            hitResults.Dispose();
        }

        return result;
    }



    private void Build(Vector3 position)
    {
        if (Inventory.Instance.selectedItem != null &&
            Inventory.Instance.selectedItem.itemType == ItemType.Building)
        {
            // Создаём основную сущность для здания  
            Entity building = entityManager.Instantiate(currentBuildingPrefab);

            // Устанавливаем позицию и поворот  
            entityManager.SetComponentData(building, LocalTransform.FromPositionRotation(position, quaternion.identity));

            // Убираем предмет из инвентаря  
            Inventory.Instance.Remove(Inventory.Instance.selectedItem);

            // Выходим из режима строительства  
            ExitBuildingMode();
        }
    }




    public void CancelBuilding()
    {
        ExitBuildingMode();
    }

    private void ExitBuildingMode()
    {
        if (buildingPreview != Entity.Null)
        {
            entityManager.DestroyEntity(buildingPreview);
        }
        isInBuildingMode = false;
        Debug.Log("Режим строительства отменен.");
    }

    public void SetPreviewMaterial(Entity entity, bool isValid)
    {
        var materialID = isValid ? validMaterialID : invalidMaterialID;

        if (entityManager.HasComponent<MaterialMeshInfo>(entity))
        {
            var materialMeshInfo = entityManager.GetComponentData<MaterialMeshInfo>(entity);
            materialMeshInfo.MaterialID = materialID;
            entityManager.SetComponentData(entity, materialMeshInfo);
        }
    }
}