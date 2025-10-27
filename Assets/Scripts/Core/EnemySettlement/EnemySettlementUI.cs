using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

public class EnemySettlementUI : MonoBehaviour
{
    [Header("UI Elements")]
    [SerializeField] private GameObject uiPanel;
    [SerializeField] private TextMeshProUGUI settlementNameText;
    [SerializeField] private TextMeshProUGUI requirementStatusText;
    [SerializeField] private Button captureButton;
    [SerializeField] private Button closeButton;

    private EntityManager entityManager;
    private Entity currentSettlementEntity;
    private EntityQuery aliveEnemiesQuery;
    private bool isInitialized = false;

    void Start()
    {
        TryInitialize();
        if (isInitialized)
        {
            closeButton.onClick.AddListener(OnCloseButtonPressed);
            captureButton.onClick.AddListener(OnCaptureButtonPressed);
            uiPanel.SetActive(false);
        }
    }

    void Update()
    {
        if (!isInitialized) { TryInitialize(); return; }

        var gameStateQuery = entityManager.CreateEntityQuery(typeof(GameState));
        if (gameStateQuery.IsEmpty) return;
        var gameStateEntity = gameStateQuery.GetSingletonEntity();
        if (!entityManager.HasComponent<UIState>(gameStateEntity)) 
        {
            if (uiPanel.activeSelf) uiPanel.SetActive(false);
            return;
        }

        var uiState = entityManager.GetComponentData<UIState>(gameStateEntity);
        bool shouldBeOpen = entityManager.HasComponent<InUIMode>(gameStateEntity) &&
                            uiState.ActiveUIType == UIType.EnemySettlement;

        if (shouldBeOpen)
        {
            if (!uiPanel.activeSelf || currentSettlementEntity != uiState.ActiveUITarget)
            {
                currentSettlementEntity = uiState.ActiveUITarget;
                Show();
            }
            RefreshUI();
        }
        else if (uiPanel.activeSelf)
        {
            Hide();
        }
    }

    private void TryInitialize()
    {
        if (isInitialized) return;
        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null && world.IsCreated)
        {
            entityManager = world.EntityManager;
            aliveEnemiesQuery = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<HostileNPCTag>(),
                ComponentType.Exclude<IsDeadTag>()
            );
            isInitialized = true;
        }
    }

    private void Show()
    {
        uiPanel.SetActive(true);
        RefreshUI();
    }
    
    private void Hide()
    {
        uiPanel.SetActive(false);
        currentSettlementEntity = Entity.Null;
    }

    private void RefreshUI()
    {
        if (!entityManager.Exists(currentSettlementEntity)) { Hide(); return; }

        settlementNameText.text = entityManager.GetComponentData<SettlementComponent>(currentSettlementEntity).Name.ToString();

        int enemyCount = 0;
        
        var enemies = aliveEnemiesQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        foreach (var enemy in enemies)
        {
            if (entityManager.HasComponent<SpawnedBySettlement>(enemy) && 
                entityManager.GetComponentData<SpawnedBySettlement>(enemy).SettlementEntity == currentSettlementEntity)
            {
                enemyCount++;
            }
        }
        enemies.Dispose();
        
        if (enemyCount > 0)
        {
            requirementStatusText.text = $"Для захвата нужно уничтожить врагов: {enemyCount}";
            captureButton.interactable = false;
        }
        else
        {
            requirementStatusText.text = "Все враги уничтожены. Готово к захвату!";
            captureButton.interactable = true;
        }
    }

    private void OnCloseButtonPressed() => GameBridge.Instance?.HandleUICloseAction();

    private void OnCaptureButtonPressed()
    {
        if (!isInitialized || currentSettlementEntity == Entity.Null) return;
        var request = entityManager.CreateEntity();
        entityManager.AddComponentData(request, new CaptureSettlementRequest
        {
            SettlementEntity = currentSettlementEntity
        });
    }
}