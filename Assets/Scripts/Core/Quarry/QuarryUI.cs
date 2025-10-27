using UnityEngine;
using Unity.Entities;
using TMPro;
using UnityEngine.UI;
using Energy.Core;
using Unity.Transforms;
using Unity.Mathematics;

/// <summary>
/// Управляет UI-панелью карьера, отображая его состояние,
/// потребление энергии, эффективность и предоставляя кнопки для взаимодействия.
/// Является связующим звеном между данными ECS и интерфейсом, который видит игрок.
/// </summary>
public class QuarryUI : MonoBehaviour
{
    /// <summary>
    /// Статический экземпляр для глобального доступа (синглтон).
    /// </summary>
    public static QuarryUI Instance { get; private set; }

    [Header("UI Элементы")]
    [SerializeField] private GameObject uiPanel;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private TextMeshProUGUI energyConsumptionText;
    [SerializeField] private TextMeshProUGUI resourceTypeText;
    [SerializeField] private TextMeshProUGUI harvestSpeedText;

    [Header("Кнопки")]
    [SerializeField] private Button closeButton;
    [SerializeField] private Button toggleButton;
    [SerializeField] private Button inventoryButton;

    private EntityManager _em;
    private bool _isInitialized;
    private Entity _currentTarget;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        #if UNITY_EDITOR
        if (uiPanel == null || nameText == null || statusText == null || energyConsumptionText == null ||
            resourceTypeText == null || harvestSpeedText == null || closeButton == null ||
            toggleButton == null || inventoryButton == null)
        {
            Debug.LogError("[QuarryUI] Не все UI элементы назначены в инспекторе!", this);
            enabled = false;
        }
        #endif
    }

    private void Start()
    {
        TryInitialize();
        if (_isInitialized)
        {
            uiPanel.SetActive(false);
            closeButton.onClick.AddListener(OnClosePressed);
            toggleButton.onClick.AddListener(OnTogglePressed);
            inventoryButton.onClick.AddListener(OnInventoryPressed);
        }
    }

    private void TryInitialize()
    {
        if (_isInitialized) return;
        var world = World.DefaultGameObjectInjectionWorld;
        if (world != null && world.IsCreated)
        {
            _em = world.EntityManager;
            _isInitialized = true;
        }
    }

    /// <summary>
    /// Отслеживает глобальное состояние UI и открывает/закрывает панель
    /// в зависимости от того, выбран ли карьер.
    /// </summary>
    private void Update()
    {
        if (!_isInitialized)
        {
            TryInitialize();
            return;
        }

        var gameStateQuery = _em.CreateEntityQuery(typeof(GameState));
        if (gameStateQuery.IsEmpty) return;
        var gameStateEntity = gameStateQuery.GetSingletonEntity();

        bool shouldBeOpen = _em.HasComponent<InUIMode>(gameStateEntity) &&
                            _em.HasComponent<UIState>(gameStateEntity) &&
                            _em.GetComponentData<UIState>(gameStateEntity).ActiveUIType == UIType.Quarry;

        if (uiPanel.activeSelf != shouldBeOpen)
        {
            if (shouldBeOpen)
            {
                var uiState = _em.GetComponentData<UIState>(gameStateEntity);
                OpenFor(uiState.ActiveUITarget);
            }
            else
            {
                Hide();
            }
        }
    }

    /// <summary>
    /// Обновляет UI в LateUpdate, чтобы гарантировать, что все системы ECS уже отработали.
    /// </summary>
    private void LateUpdate()
    {
        if (uiPanel.activeSelf && _currentTarget != Entity.Null && _em.Exists(_currentTarget))
        {
            Refresh();
        }
    }

    /// <summary>
    /// Открывает UI-панель для указанной сущности карьера.
    /// </summary>
    public void OpenFor(Entity quarryEntity)
    {
        if (!_isInitialized) TryInitialize();
        if (!_em.Exists(quarryEntity) || !_em.HasComponent<QuarryTag>(quarryEntity))
        {
            _currentTarget = Entity.Null;
            uiPanel.SetActive(false);
            return;
        }

        _currentTarget = quarryEntity;
        uiPanel.SetActive(true);
        Refresh();
    }

    private void Hide()
    {
        uiPanel.SetActive(false);
        _currentTarget = Entity.Null;
    }

    private void OnClosePressed()
    {
        GameBridge.Instance?.HandleUICloseAction();
    }

    private void OnInventoryPressed()
    {
        if (_currentTarget == Entity.Null) return;
        
        var requestEntity = _em.CreateEntity();
        _em.AddComponentData(requestEntity, new OpenTradeUIRequest { Target = _currentTarget });
    }

    private void OnTogglePressed()
    {
        if (_currentTarget == Entity.Null) return;
        
        var requestEntity = _em.CreateEntity();
        _em.AddComponentData(requestEntity, new ToggleQuarryRequest { Target = _currentTarget });
    }

    /// <summary>
    /// Основной метод обновления UI. Считывает данные из компонентов ECS
    /// целевого карьера и обновляет все текстовые поля и кнопки.
    /// </summary>
    private void Refresh()
    {
        if (!_em.Exists(_currentTarget))
        {
            Hide();
            return;
        }

        var quarryState = _em.GetComponentData<QuarryState>(_currentTarget);
        var quarrySettings = _em.GetComponentData<QuarrySettings>(_currentTarget);
        
        #if UNITY_EDITOR
        nameText.text = _em.GetName(_currentTarget);
        #else
        nameText.text = "Карьер";
        #endif

        energyConsumptionText.text = $"Потребление: {quarrySettings.EnergyConsumptionKW:F1} кВт";

        if (quarryState.TargetResourceNode != Entity.Null && _em.Exists(quarryState.TargetResourceNode))
        {
            var resourceNode = _em.GetComponentData<ResourceNode>(quarryState.TargetResourceNode);
            resourceTypeText.text = $"Ресурс: {resourceNode.resourceType}";
        }
        else
        {
            resourceTypeText.text = "Ресурс: Не найден";
        }
        
        string currentStatus;
        float efficiency = 0f;
        
        bool isInventoryFull = _em.HasComponent<QuarryInventoryFullTag>(_currentTarget);

        if (!quarryState.IsOnline)
        {
            currentStatus = "Остановлен";
            efficiency = 0f;
        }
        else if (isInventoryFull)
        {
            currentStatus = "Инвентарь полон";
            efficiency = 0f;
        }
        else // Карьер включен и не полон, теперь проверяем энергию
        {
            var nodeOwner = ResolveNodeOwner(_currentTarget);
            float powerDelivered = 0f;
            if (nodeOwner != Entity.Null && _em.HasComponent<NetLinkUsage>(nodeOwner))
            {
                powerDelivered = _em.GetComponentData<NetLinkUsage>(nodeOwner).InUsedKW;
            }
            
            float requiredPower = quarrySettings.EnergyConsumptionKW;
            efficiency = (requiredPower > 1e-6f) ? math.saturate(powerDelivered / requiredPower) : 1f;
            
            if (efficiency < 0.01f)
            {
                currentStatus = "Нет энергии";
            }
            else if (efficiency < 0.99f)
            {
                currentStatus = "Работает (нехватка энергии)";
            }
            else
            {
                currentStatus = "Работает";
            }
        }
        
        statusText.text = $"Статус: {currentStatus}";
        
        float baseInterval = quarrySettings.HarvestInterval;
        float effectiveInterval = (efficiency > 1e-6f) ? baseInterval / efficiency : float.PositiveInfinity;
        string intervalText = float.IsPositiveInfinity(effectiveInterval) ? "∞" : $"{effectiveInterval:F1}с";
        
        harvestSpeedText.text = $"Эффективность: {efficiency * 100:F0}% ({intervalText})";
        
        toggleButton.GetComponentInChildren<TextMeshProUGUI>().text = quarryState.IsOnline ? "Остановить" : "Запустить";
    }
    
    /// <summary>
    /// Вспомогательный метод для поиска сущности-владельца NetworkNode,
    /// двигаясь вверх по иерархии Parent.
    /// </summary>
    private Entity ResolveNodeOwner(Entity entity)
    {
        if (entity == Entity.Null || !_em.Exists(entity)) return Entity.Null;
        if (_em.HasComponent<NetworkNode>(entity)) return entity;

        int safety = 16;
        Entity current = entity;
        while (safety-- > 0 && _em.HasComponent<Parent>(current))
        {
            current = _em.GetComponentData<Parent>(current).Value;
            if (_em.Exists(current) && _em.HasComponent<NetworkNode>(current))
            {
                return current;
            }
        }
        return Entity.Null;
    }
}