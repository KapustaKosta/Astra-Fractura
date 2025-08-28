using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using Unity.Rendering;
using UnityEngine;

/// <summary>
/// Пространство имен, содержащее все определения ECS-компонентов для системы проводки и симуляции электричества.
/// Каждый компонент представляет собой либо данные конфигурации, либо состояние, либо тег, используемый системами.
/// </summary>
namespace Wiring
{
    #region Настройки и глобальные компоненты

    /// <summary>
    /// Глобальная конфигурация для системы проводки.
    /// Хранит индексы слоев, настройки визуализации коннекторов и пропускную способность для каждого уровня проводов.
    /// </summary>
    public struct WireSettings : IComponentData
    {
        /// <summary>
        /// Индекс слоя Unity, используемый для коннекторов проводов. Коннекторы будут размещены на этом слое,
        /// чтобы по ним можно было выполнять трассировку лучей без помех со стороны других физических слоев.
        /// Слой настраивается через соответствующий authoring-компонент.
        /// </summary>
        public int ConnectorLayer;

        /// <summary>
        /// Единый масштаб, применяемый к визуальным сущностям коннекторов. Коннекторы отображаются как простые сферы,
        /// и это значение контролирует их размер в мире. Рекомендуется использовать небольшие значения (например, 0.1f).
        /// </summary>
        public float ConnectorScale;

        /// <summary>
        /// Пропускная способность проводов 1-го уровня в единицах энергии в секунду (например, Ваттах).
        /// </summary>
        public float Level1Capacity;

        /// <summary>
        /// Пропускная способность проводов 2-го уровня в единицах энергии в секунду (например, Ваттах).
        /// </summary>
        public float Level2Capacity;

        /// <summary>
        /// Пропускная способность проводов 3-го уровня в единицах энергии в секунду (например, Ваттах).
        /// </summary>
        public float Level3Capacity;
    }

    /// <summary>
    /// Компонент-синглтон, используемый для хранения ссылки на визуальный префаб для проводов.
    /// </summary>
    public struct WireVisualPrefab : IComponentData
    {
        public Entity Prefab;
    }

    /// <summary>
    /// Тег-компонент, добавляемый к глобальной сущности GameState, когда игрок находится в режиме прокладки проводов.
    /// Пока этот тег присутствует, коннекторы проводов становятся видимыми, и игрок может их соединять.
    /// </summary>
    public struct InWirePlacementMode : IComponentData { }

    /// <summary>
    /// Хранит временное состояние, используемое во время прокладки провода.
    /// Хранится на сущности GameState для удобства доступа.
    /// </summary>
    public struct WireState : IComponentData
    {
        /// <summary>
        /// Выбранный игроком уровень провода. Определяет пропускную способность следующего прокладываемого провода.
        /// </summary>
        public int SelectedWireLevel;

        /// <summary>
        /// Коннектор, по которому кликнули первым при начале прокладки провода. Если null (Entity.Null),
        /// это означает, что игрок еще не выбрал стартовый коннектор.
        /// </summary>
        public Entity StartConnector;

        /// <summary>
        /// Сущность, представляющая визуальное превью прокладываемого провода. Эта сущность создается
        /// при выборе стартового коннектора и уничтожается, когда провод подтвержден или отменен.
        /// </summary>
        public Entity WirePreviewEntity;
    }

    #endregion

    #region Коннекторы и компоненты проводов

    /// <summary>
    /// Компонент-синглтон, указывающий на незавершенную операцию прокладки провода.
    /// Содержит ссылку на стартовый коннектор и выбранный уровень провода.
    /// </summary>
    public struct PendingWire : IComponentData
    {
        public Entity StartConnector;
        public int Level;
    }

    /// <summary>
    /// Тег для сущностей, являющихся визуальным представлением готового провода.
    /// Отличает их от временных превью-сущностей.
    /// </summary>
    public struct WireVisualTag : IComponentData { }

    /// <summary>
    /// Компонент, связывающий визуальное представление провода с его логической сущностью (у которой есть компонент Wire).
    /// </summary>
    public struct WireOwner : IComponentData
    {
        public Entity Wire;
    }

    /// <summary>
    /// Тег, присваиваемый сущностям, которые представляют собой коннекторы проводов. Используется для
    /// фильтрации трассировки лучей и для управления видимостью при входе/выходе из режима прокладки проводов.
    /// </summary>
    public struct WireConnectorTag : IComponentData { }

    /// <summary>
    /// Тег, добавляемый к коннектору, когда он является валидной целью для подключения.
    /// Используется системой подсветки для окрашивания в зеленый цвет.
    /// </summary>
    public struct WireConnectorHighlightedTag : IComponentData, IEnableableComponent { }

    /// <summary>
    /// Компонент-состояние, указывающий, что начат процесс удаления провода.
    /// </summary>
    public struct PendingWireRemoval : IComponentData
    {
        public Entity FirstConnector;
        public Entity WireToRemove;
        public Entity WireVisual;
    }

    /// <summary>
    /// Динамический буфер, хранящий ссылки на провода, подключенные к коннектору.
    /// Позволяет быстро найти все провода, присоединенные к определенной точке.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ConnectedWires : IBufferElementData
    {
        public Entity Wire;
    }

    /// <summary>
    /// Компонент, описывающий провод. Хранит ссылки на начальный и конечный коннекторы,
    /// уровень (пропускную способность) провода и его текущую нагрузку (полезно для отладки или расширенной симуляции).
    /// </summary>
    public struct Wire : IComponentData
    {
        public Entity StartConnector;
        public Entity EndConnector;
        public int Level;
        public float CurrentLoad;
    }

    /// <summary>
    /// Тег для сущностей-превью проводов. Превью-провода — это временные визуалы,
    /// отображаемые, пока игрок выбирает второй коннектор. Они уничтожаются после
    /// размещения провода или отмены действия.
    /// </summary>
    public struct WirePreviewTag : IComponentData { }

    /// <summary>
    /// Конфиг превью провода. Синглтон. Префаб ориентирован вдоль +Z.
    /// </summary>
    public struct WirePreviewConfig : IComponentData
    {
        public Entity Prefab;              // визуал превью (Entities Graphics)
        public float Thickness;           // толщина (по X/Y)
        public float MagnetRadius;        // радиус «прилипания» к коннекторам (0 = выкл.)
        public uint GroundCategory;      // Unity.Physics category bits для земли
        public uint ConnectorCategory;   // Unity.Physics category bits для коннекторов
        public float SmoothRotationSpeed; // s^-1 для slerp
        public float SmoothLengthSpeed;   // s^-1 для lerp
        public float CostLabelHeight;     // высота подсказки над точкой b
    }

    /// <summary>
    /// Данные для HUD-подсказки стоимости. Синглтон, читает Mono HUD (без CompanionLink).
    /// </summary>
    public struct WireCostHintData : IComponentData
    {
        public bool Visible;
        public float3 WorldPos;
        public FixedString128Bytes Text;
        public float4 Color; // RGBA 0..1
    }

    #endregion

    #region Компоненты энергии

    /// <summary>
    /// Компонент для зданий, генерирующих электричество. OutputPower измеряется
    /// в единицах энергии в секунду (например, Ваттах). Генераторы постоянно
    /// поставляют это количество энергии в свою сеть.
    /// </summary>
    public struct Generator : IComponentData
    {
        public float OutputPower;
    }

    /// <summary>
    /// Компонент для зданий, потребляющих электричество. Потребление измеряется
    /// в единицах энергии в секунду (например, Ваттах). Потребители будут
    /// запрашивать это количество энергии из своей сети при каждом обновлении.
    /// </summary>
    public struct Consumer : IComponentData
    {
        public float Consumption;
    }

    /// <summary>
    /// Компонент для зданий, которые хранят электричество. Батареи могут
    /// как поставлять, так и поглощать энергию.
    /// </summary>
    public struct Battery : IComponentData
    {
        /// <summary>
        /// Максимальная энергия, которая может быть сохранена (в Джоулях).
        /// </summary>
        public float Capacity;

        /// <summary>
        /// Текущий сохраненный заряд (в Джоулях).
        /// </summary>
        public float CurrentCharge;

        /// <summary>
        /// Максимальное количество энергии, которое батарея может поглотить в секунду (в Ваттах).
        /// </summary>
        public float MaxChargeRate;

        /// <summary>
        /// Максимальное количество энергии, которое батарея может поставить в секунду (в Ваттах).
        /// </summary>
        public float MaxDischargeRate;
    }

    /// <summary>
    /// Тег-компонент, указывающий, что здание в данный момент имеет достаточное питание.
    /// Системы могут запрашивать этот тег, чтобы изменять поведение или визуализацию
    /// в зависимости от доступности энергии.
    /// </summary>
    public struct PoweredTag : IComponentData { }



    #endregion
}