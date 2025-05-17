using UnityEngine;
using Unity.Entities;
using TMPro;

public class SettlementUI : MonoBehaviour
{
    public static SettlementUI Instance;

    [Header("UI Elements")]
    [SerializeField] private GameObject uiPanel; // Панель UI для отображения информации
    [SerializeField] private TextMeshProUGUI settlementNameText; // Название поселения
    [SerializeField] private TextMeshProUGUI statsText; // Статистика поселения (уровень, население)
    [SerializeField] private GameObject closeButton; // Кнопка закрытия UI

    private EntityManager entityManager;

    private void Awake()
    {
        // Singleton для доступа к UI
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        uiPanel.SetActive(false); // Скрываем UI по умолчанию
    }

    public void Show(SettlementComponent settlement)
    {
        // Обновляем текст UI
        settlementNameText.text = $"Поселение #{settlement.Name}";
        statsText.text = $"Уровень: {settlement.Level}\nНаселение: {settlement.Population}";

        // Показываем UI
        uiPanel.SetActive(true);
    }

    /// <summary>
    /// Скрывает UI.
    /// </summary>
    public void Hide()
    {
        uiPanel.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// Вызывается кнопкой закрытия UI.
    /// </summary>
    public void OnCloseButtonPressed()
    {
        Hide();
    }
}
