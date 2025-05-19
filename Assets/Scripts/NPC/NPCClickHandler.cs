using UnityEngine;
using Unity.Entities;
using Unity.Physics;

public class NPCClickHandler : MonoBehaviour
{
    public static NPCClickHandler Instance { get; private set; } // ���������� Singleton

    public float interactionRange = 5f; // ��������� �������������� � NPC

    private EntityManager entityManager;
    private CollisionWorld collisionWorld;

    private void Awake()
    {
        // ��������, ��� ���������� ������ ���� ���������
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
    }

    private void Start()
    {
        // �������� CollisionWorld �� PhysicsWorldSingleton
    }

    public void HandleRightClick()
    {
        TryInteractWithNPC();
    }

    private void TryInteractWithNPC()
    {
        // ������� ��� �� ������� ������
        UnityEngine.Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        // �������� ������ ���� "NPC"
        int npcLayer = LayerMask.NameToLayer("NPC");
        if (npcLayer == -1)
        {
            Debug.LogError("���� 'NPC' �� ������. ���������, ��� �� ���������� � ���������� �������.");
            return;
        }

        // ������� RaycastInput ��� ECS
        RaycastInput raycastInput = new RaycastInput
        {
            Start = ray.origin,
            End = ray.origin + ray.direction * interactionRange,
            Filter = new CollisionFilter
            {
                BelongsTo = (uint)(1 << npcLayer), // ��������� ������ ���� "NPC"
                CollidesWith = (uint)(1 << npcLayer), // ������������ ������ � "NPC"
                GroupIndex = 0
            }
        };

        EntityQuery query = entityManager.CreateEntityQuery(typeof(PhysicsWorldSingleton));
        PhysicsWorldSingleton physicsWorld = query.GetSingleton<PhysicsWorldSingleton>();
        collisionWorld = physicsWorld.CollisionWorld;

        // ��������� Raycast
        if (collisionWorld.CastRay(raycastInput, out Unity.Physics.RaycastHit hit))
        {
            // �������� Entity �� RaycastHit
            Entity entity = hit.Entity;

            // ���������, ���� �� � Entity ��������� NPCComponent
            if (entityManager.HasComponent<NPCComponent>(entity))
            {
                // �������� ������ ���������� NPCComponent
                var npc = entityManager.GetComponentData<NPCComponent>(entity);

                // ������ �������������� � NPC
                Debug.Log($"�������������� � NPC. ���: {npc.Name}, ������: {npc.Skills}");

                // ���������� NPCUI ��� ����������� ����������
                NPCUI.Instance.Show(npc, entity);

                LockPlayerControls(true);
                return;
            }
        }
    }

    public void LockPlayerControls(bool isLocked)
    {
        // �������� ��� ��������� ������
        Cursor.lockState = isLocked ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isLocked;
    }
}
