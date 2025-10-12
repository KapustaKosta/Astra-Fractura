using UnityEngine;
using Unity.Entities;

/// <summary>
/// Управляет UI-элементом, который появляется при смерти игрока.
/// </summary>
public class DeathUIController : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Панель, которая будет показана при смерти")]
    [SerializeField] private GameObject deathUIPanel;

    private EntityManager entityManager;
    private EntityQuery deathUIQuery;
    private bool isInitialized;

    /// <summary>
    /// Вызывается при старте. Гарантирует, что UI изначально скрыт.
    /// </summary>
    void Start()
    {
        if (deathUIPanel != null)
        {
            deathUIPanel.SetActive(false);
        }
        else
        {
            Debug.LogError("DeathUIController: Панель UI смерти не назначена в инспекторе!");
            enabled = false;
        }
    }

    /// <summary>
    /// Вызывается каждый кадр. Пытается инициализироваться, а затем проверяет наличие запроса на показ UI.
    /// </summary>
    void Update()
    {
        if (!isInitialized)
        {
            TryInitialize();
            return;
        }
        
        if (deathUIPanel.activeSelf)
        {
            return;
        }

        // Проверяем, появился ли запрос на отображение UI смерти.
        if (!deathUIQuery.IsEmpty)
        {
            // Показываем UI
            deathUIPanel.SetActive(true);
            
            // Уничтожаем запрос, чтобы он не срабатывал повторно.
            entityManager.DestroyEntity(deathUIQuery.GetSingletonEntity());
        }
    }

    /// <summary>
    /// Пытается найти EntityManager и создать запрос к сущностям.
    /// </summary>
    private void TryInitialize()
    {
        if (World.DefaultGameObjectInjectionWorld != null && World.DefaultGameObjectInjectionWorld.IsCreated)
        {
            entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            deathUIQuery = entityManager.CreateEntityQuery(typeof(ShowDeathUIRequest));
            isInitialized = true;
        }
    }
}