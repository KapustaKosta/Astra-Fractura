using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using P = Unity.Physics;
using Unity.Transforms;
using Unity.Rendering;
using Unity.Collections;

/// <summary>
/// Управляет функциональностью режима строительства, включая предварительный просмотр зданий,
/// проверку валидности размещения и инициацию запросов на размещение.
/// </summary>
public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance;

    [Header("Settings")]
    public Material validPlacementMaterial;
    public Material invalidPlacementMaterial;
    [Range(0f, 90f)]
    public float maxPlacementSlopeAngle = 25f;

    [Header("Collision Layers")]
    public LayerMask buildableSurfaceLayer;
    public LayerMask obstacleLayer;

    private UnityEngine.Rendering.BatchMaterialID _validMatID;
    private UnityEngine.Rendering.BatchMaterialID _invalidMatID;

    private EntityManager _em;
    private Entity _previewEntity;
    private BlobAssetReference<P.Collider> _previewCollider;
    private int _currentItemId;

    private bool _isBuildingMode;
    private bool _lastPlacementValid;

    /// <summary>
    /// Инициализирует Singleton-экземпляр.
    /// </summary>
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); }
    }

    /// <summary>
    /// Инициализирует EntityManager и регистрирует материалы для предпросмотра.
    /// </summary>
    private void Start()
    {
        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
        }
        else
        {
            enabled = false;
            return;
        }

        _validMatID = RegisterMaterial(validPlacementMaterial);
        _invalidMatID = RegisterMaterial(invalidPlacementMaterial);
    }

    /// <summary>
    /// Каждый кадр обрабатывает логику режима строительства.
    /// </summary>
    private void Update()
    {
        if (!_em.World.IsCreated) return;

        var gameStateQuery = _em.CreateEntityQuery(typeof(GameState));
        if (gameStateQuery.IsEmpty) return;
        var gameStateEntity = gameStateQuery.GetSingletonEntity();

        // Проверяем состояние через наличие компонентов-тегов на глобальной сущности
        bool shouldBeInBuildingMode = _em.HasComponent<InBuildingMode>(gameStateEntity);

        if (shouldBeInBuildingMode && !_isBuildingMode)
        {
            // Получаем данные для строительства из нового компонента BuildingState
            var buildingState = _em.GetComponentData<BuildingState>(gameStateEntity);
            StartBuildingMode(buildingState.BuildingPrefabToPlace, buildingState.BuildingItemID);
        }
        else if (!shouldBeInBuildingMode && _isBuildingMode)
        {
            ExitBuildingMode();
            return;
        }

        if (!_isBuildingMode) return;

        var physicsWorldQuery = _em.CreateEntityQuery(typeof(P.PhysicsWorldSingleton));
        if (physicsWorldQuery.IsEmpty) return;
        var physicsWorld = physicsWorldQuery.GetSingleton<P.PhysicsWorldSingleton>();

        if (Camera.main == null) return;

        var camRay = Camera.main.ScreenPointToRay(Input.mousePosition);
        var rayInput = new P.RaycastInput
        {
            Start = camRay.origin,
            End = camRay.origin + camRay.direction * 300f,
            Filter = new P.CollisionFilter { BelongsTo = ~0u, CollidesWith = (uint)buildableSurfaceLayer.value, GroupIndex = 0 }
        };

        bool placementValid = false;
        if (_em.Exists(_previewEntity) && physicsWorld.CollisionWorld.CastRay(rayInput, out var hit))
        {
            _em.SetComponentData(_previewEntity, LocalTransform.FromPosition(hit.Position));

            float slope = Vector3.Angle(Vector3.up, hit.SurfaceNormal);
            bool slopeOk = slope <= maxPlacementSlopeAngle;

            var collisionWorld = physicsWorld.CollisionWorld;
            bool noOverlap = CheckOverlap(hit.Position, ref collisionWorld, out _);
            placementValid = slopeOk && noOverlap;

            if (placementValid && Input.GetMouseButtonDown(0))
                Build(hit.Position);
        }

        if (placementValid != _lastPlacementValid)
        {
            SetPreviewMaterial(placementValid);
            _lastPlacementValid = placementValid;
        }

        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
        {
            var entity = _em.CreateEntity();
            _em.AddComponentData(entity, new ExitBuildingModeRequest());
        }
    }

    /// <summary>
    /// Входит в режим строительства, создавая сущность для предпросмотра.
    /// </summary>
    /// <param name="prefabToBuild">Префаб здания для постройки.</param>
    /// <param name="itemID">ID предмета, который строится.</param>
    private void StartBuildingMode(Entity prefabToBuild, int itemID)
    {
        if (prefabToBuild == Entity.Null) return;

        _currentItemId = itemID;
        if (_em.HasComponent<P.PhysicsCollider>(prefabToBuild))
            _previewCollider = _em.GetComponentData<P.PhysicsCollider>(prefabToBuild).Value;

        if (!_previewCollider.IsCreated)
        {
            _currentItemId = 0;
            return;
        }

        _previewEntity = _em.Instantiate(prefabToBuild);
        _em.SetComponentData(_previewEntity, LocalTransform.FromPosition(new float3(0, -1000, 0)));
        if (_em.HasComponent<P.PhysicsCollider>(_previewEntity))
            _em.RemoveComponent<P.PhysicsCollider>(_previewEntity);

        _isBuildingMode = true;
        _lastPlacementValid = false;
        SetPreviewMaterial(false);
    }

    /// <summary>
    /// Размещает здание в указанной позиции, маркируя его тегом NewlyBuiltTag для дальнейшей обработки.
    /// </summary>
    /// <param name="pos">Мировая позиция для размещения здания.</param>
    private void Build(float3 pos)
    {
        var gameStateQuery = _em.CreateEntityQuery(typeof(GameState));
        if (gameStateQuery.IsEmpty) return;
        var gameStateEntity = gameStateQuery.GetSingletonEntity();

        if (!_em.HasComponent<BuildingState>(gameStateEntity)) return;
        var buildingState = _em.GetComponentData<BuildingState>(gameStateEntity);

        var ent = _em.Instantiate(buildingState.BuildingPrefabToPlace);
        _em.SetComponentData(ent, LocalTransform.FromPositionRotation(pos, quaternion.identity));
        _em.AddComponent<NewlyBuiltTag>(ent);

        if (Inventory.Instance != null)
        {
            var itemInInventory = Inventory.Instance.items.Find(invItem => invItem.item.itemID == _currentItemId);
            if (itemInInventory != null)
                Inventory.Instance.Remove(itemInInventory.item, 1);
        }

        var entity = _em.CreateEntity();
        _em.AddComponentData(entity, new PlaceBuildingRequest());
    }

    /// <summary>
    /// Выходит из режима строительства, уничтожая сущность предпросмотра.
    /// </summary>
    private void ExitBuildingMode()
    {
        if (!_isBuildingMode) return;
        if (_previewEntity != Entity.Null && _em.Exists(_previewEntity))
            _em.DestroyEntity(_previewEntity);

        _isBuildingMode = false;
        _previewEntity = Entity.Null;
        _previewCollider = default;
        _currentItemId = 0;
    }

    /// <summary>
    /// Проверяет пересечения с препятствиями в заданной позиции.
    /// </summary>
    /// <returns>True, если пересечений не обнаружено.</returns>
    private bool CheckOverlap(float3 pos, ref P.CollisionWorld world, out Entity overlappingEntity)
    {
        overlappingEntity = Entity.Null;
        if (!_previewCollider.IsCreated) return true;
        var aabb = _previewCollider.Value.CalculateAabb(new RigidTransform(quaternion.identity, pos));
        var bodies = new NativeList<int>(Allocator.Temp);
        if (world.OverlapAabb(
                new P.OverlapAabbInput
                {
                    Aabb = aabb,
                    Filter = new P.CollisionFilter { BelongsTo = ~0u, CollidesWith = (uint)obstacleLayer.value, GroupIndex = 0 }
                }, ref bodies))
        {
            bodies.Dispose();
            return false;
        }

        bodies.Dispose();
        return true;
    }

    /// <summary>
    /// Регистрирует материал в рендер-системе ECS.
    /// </summary>
    /// <returns>ID зарегистрированного материала.</returns>
    private UnityEngine.Rendering.BatchMaterialID RegisterMaterial(Material m)
    {
        if (m == null) return default;
        if (World.DefaultGameObjectInjectionWorld == null || !World.DefaultGameObjectInjectionWorld.IsCreated) return default;
        var sys = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<EntitiesGraphicsSystem>();
        return sys?.RegisterMaterial(m) ?? default;
    }

    /// <summary>
    /// Устанавливает материал для сущности предпросмотра в зависимости от валидности размещения.
    /// </summary>
    /// <param name="valid">True, если размещение валидно.</param>
    private void SetPreviewMaterial(bool valid)
    {
        if (!_isBuildingMode || !_em.Exists(_previewEntity)) return;
        var id = valid ? _validMatID : _invalidMatID;
        if (id.Equals(default)) return;
        if (_em.HasComponent<MaterialMeshInfo>(_previewEntity))
        {
            var mmi = _em.GetComponentData<MaterialMeshInfo>(_previewEntity);
            mmi.MaterialID = id;
            _em.SetComponentData(_previewEntity, mmi);
        }
    }
}