using Unity.Entities;
using UnityEngine;

/// <summary>
/// Этот MonoBehaviour-скрипт действует как мост между миром ECS и миром GameObjects.
/// Он считывает данные из компонента NPCAnimationState и обновляет параметры
/// в Animator'е, прикрепленном к этому же GameObject'у.
/// </summary>
[RequireComponent(typeof(Animator))]
public class NPCAnimationController : MonoBehaviour
{
    private Animator _animator;
    private EntityManager _entityManager;
    private Entity _entity = Entity.Null;
    private World _ecsWorld;

    // Хэши для оптимизации доступа к параметрам аниматора
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int IsHarvestingHash = Animator.StringToHash("IsHarvesting");
    private static readonly int AttackTriggerHash = Animator.StringToHash("Attack");

    public void Init(Entity entity, World world)
    {
        _entity = entity;
        _ecsWorld = world;
        _entityManager = world.EntityManager;
    }

    void Start()
    {
        _animator = GetComponent<Animator>();
    }

    void Update()
    {
        if (_entityManager == null || !_ecsWorld.IsCreated || _entity == Entity.Null)
        {
            return;
        }
        
        if (!_entityManager.Exists(_entity) || !_entityManager.HasComponent<NPCAnimationState>(_entity))
        {
            // Если сущность уничтожена, уничтожаем и GameObject
            if (_entityManager.Exists(_entity) == false)
                Destroy(gameObject);
            return;
        }
        
        var animationState = _entityManager.GetComponentData<NPCAnimationState>(_entity);

        _animator.SetFloat(SpeedHash, animationState.Speed);
        _animator.SetBool(IsHarvestingHash, animationState.IsHarvesting);

        if (animationState.AttackTrigger)
        {
            _animator.SetTrigger(AttackTriggerHash);
        }
    }
}