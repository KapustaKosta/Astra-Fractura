using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// Система регистрации целей ИИ.
/// Загружает все определения целей из ресурсов и создает реестр для последующего использования.
/// Обновляется в группе InitializationSystemGroup.
/// </summary>
[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial class GoalRegistrySystem : SystemBase
{
    /// <summary>
    /// Словарь, сопоставляющий тип цели с её определением.
    /// Доступен только для чтения извне.
    /// </summary>
    public IReadOnlyDictionary<GoalType, GoalDefinition> GoalDefinitionsMap { get; private set; }
    
    /// <summary>
    /// Компонент, сигнализирующий об успешной инициализации системы.
    /// </summary>
    public struct Initialized : IComponentData { }

    /// <summary>
    /// Инициализация системы: загрузка и регистрация всех определений целей.
    /// Создает сущность с меткой Initialized при завершении инициализации.
    /// </summary>
    protected override void OnCreate()
    {
        // Загружаем все определения целей из папки Resources/GoalDefinition
        var definitions = Resources.LoadAll<GoalDefinition>("GoalDefinition");
        
        // Проверяем наличие определений
        if (definitions.Length == 0)
        {
            #if UNITY_EDITOR
            Debug.LogWarning("Не найдено ни одного GoalDefinition в папке 'Resources/GoalDefinition'. AI не сможет принимать решения.");
            #endif
            GoalDefinitionsMap = new Dictionary<GoalType, GoalDefinition>();
            EntityManager.CreateEntity(typeof(Initialized));
            return;
        }

        // Создаем словарь для хранения определений целей
        var map = new Dictionary<GoalType, GoalDefinition>(definitions.Length);
        
        // Регистрируем каждое определение
        foreach (var def in definitions)
        {
            if (!map.ContainsKey(def.Type))
            {
                map.Add(def.Type, def);
            }
            else
            {
                #if UNITY_EDITOR
                Debug.LogError($"Обнаружен дубликат GoalDefinition для типа {def.Type}. Будет использован только первый найденный.");
                #endif
            }
        }
        
        // Сохраняем словарь и сигнализируем об успешной инициализации
        GoalDefinitionsMap = map;
        EntityManager.CreateEntity(typeof(Initialized));
        #if UNITY_EDITOR
        Debug.Log("<color=green>[GoalRegistrySystem]</color> Реестр целей успешно создан. Арбитр теперь может запускаться.");
        #endif
    }
    
    /// <summary>
    /// Отключает систему после инициализации.
    /// Система больше не требуется для работы.
    /// </summary>
    protected override void OnUpdate()
    {
        this.Enabled = false;
    }
}