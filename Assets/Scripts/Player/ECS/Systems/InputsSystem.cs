using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using StarterAssets;
using System;
using Wiring;
using Conveyor;

/// <summary>
/// ECS-система, отвечающая за обработку ввода от пользователя.
/// Просто записывает состояние ввода в компонент InputsData.
/// </summary>
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[UpdateBefore(typeof(PlayerMovementSystem))]
public partial class InputsSystem : SystemBase
{
    private Vector2 moveInput;
    private Vector2 lookInput;
    private bool sprintInput;
    private bool jumpRequested;
    private bool inventoryRequested;
    private bool rightClickRequested;
    private double lastJumpTime;
    private bool primaryActionInput;
    private bool rotateHeld;

    // Поля для хранения состояния ввода для квикбара.
    private int quickbarDigitPressed;
    private float quickbarScrollDelta;

    protected override void OnStartRunning()
    {
        StarterAssetsInputs.onMove += OnMove;
        StarterAssetsInputs.onLook += OnLook;
        StarterAssetsInputs.onSprint += OnSprint;
        StarterAssetsInputs.onJump += OnJump;
        StarterAssetsInputs.onInventory += OnInventory;
        StarterAssetsInputs.onRightClick += OnRightClick;
        StarterAssetsInputs.onPrimaryAction += OnPrimaryAction;
        StarterAssetsInputs.onQuickbarDigit += OnQuickbarDigit;
        StarterAssetsInputs.onQuickbarScroll += OnQuickbarScroll;
        StarterAssetsInputs.onRotate += OnRotate;

        inventoryRequested = false;
        rightClickRequested = false;
        jumpRequested = false;
        primaryActionInput = false;
        lastJumpTime = double.NegativeInfinity;

        // Инициализируем поля для квикбара.
        quickbarDigitPressed = 0;
        quickbarScrollDelta = 0f;
    }

    /// <summary>
    /// Вызывается при остановке системы. Отписывается от событий ввода.
    /// </summary>
    protected override void OnStopRunning()
    {
        StarterAssetsInputs.onMove -= OnMove;
        StarterAssetsInputs.onLook -= OnLook;
        StarterAssetsInputs.onSprint -= OnSprint;
        StarterAssetsInputs.onJump -= OnJump;
        StarterAssetsInputs.onInventory -= OnInventory;
        StarterAssetsInputs.onRightClick -= OnRightClick;
        StarterAssetsInputs.onPrimaryAction -= OnPrimaryAction;
        StarterAssetsInputs.onQuickbarDigit -= OnQuickbarDigit;
        StarterAssetsInputs.onQuickbarScroll -= OnQuickbarScroll;
        StarterAssetsInputs.onRotate -= OnRotate;
    }

    /// <summary>
    /// Обработчик события движения.
    /// </summary>
    private void OnMove(Vector2 input) => moveInput = input;

    /// <summary>
    /// Обработчик события обзора.
    /// </summary>
    private void OnLook(Vector2 input) => lookInput = input;

    /// <summary>
    /// Обработчик события спринта.
    /// </summary>
    private void OnSprint(bool isPressed) => sprintInput = isPressed;

    /// <summary>
    /// Обработчик события прыжка.
    /// </summary>
    private void OnJump() { jumpRequested = true; lastJumpTime = SystemAPI.Time.ElapsedTime; }
    private void OnInventory() { inventoryRequested = true; }
    private void OnRightClick() => rightClickRequested = true;

    /// <summary>
    /// Обработчик для основного действия (ЛКМ).
    /// </summary>
    private void OnPrimaryAction(bool isPressed) => primaryActionInput = isPressed;

    /// <summary>
    /// Обработчик события нажатия цифровой клавиши квикбара.
    /// </summary>
    private void OnQuickbarDigit(int digit) => quickbarDigitPressed = digit;

    /// <summary>
    /// Обработчик события прокрутки колеса мыши для квикбара.
    /// </summary>
    private void OnQuickbarScroll(float delta) => quickbarScrollDelta = delta;

    /// Обработчик события поворота (isPressed: true — кнопка зажата, false — отпущена).
    /// </summary>
    private void OnRotate(bool isPressed) => rotateHeld = isPressed;


    protected override void OnUpdate()
    {
        // ИСПРАВЛЕНО: Объявляем 'inputs' один раз в начале метода.
        var inputs = SystemAPI.GetSingletonRW<InputsData>();

        // ИСПРАВЛЕНО: Проверяем, что запрос НЕ ПУСТОЙ, вместо HasSingleton().
        if (!SystemAPI.QueryBuilder().WithAll<PlayerTag, DeadTag>().Build().IsEmpty)
        {
            // Используем уже объявленную переменную 'inputs' для сброса данных.
            inputs.ValueRW.move = float2.zero;
            inputs.ValueRW.look = float2.zero;
            inputs.ValueRW.sprint = false;
            inputs.ValueRW.jump = false;
            inputs.ValueRW.PrimaryAction = false;
            inputs.ValueRW.QuickbarDigitKeyPressed = 0;
            inputs.ValueRW.QuickbarScrollDelta = 0f;
            
            inventoryRequested = false;
            rightClickRequested = false;
            jumpRequested = false;
            
            return;
        }
        
        var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                           .CreateCommandBuffer(World.Unmanaged);

        // Создаем одноразовые запросы для действий, которые должны сработать один раз по нажатию
        if (inventoryRequested)
        {
            var invE = ecb.CreateEntity();
            ecb.AddComponent(invE, new ToggleInventoryRequest());
            inventoryRequested = false;
        }

        var gameStateEntity = SystemAPI.GetSingletonEntity<GameState>();
        bool isUI = SystemAPI.HasComponent<InUIMode>(gameStateEntity);
        bool isInBuildingMode = SystemAPI.HasComponent<InBuildingMode>(gameStateEntity);
        bool isInWireMode = SystemAPI.HasComponent<InWirePlacementMode>(gameStateEntity);
        bool isInConveyorMode = SystemAPI.HasComponent<InConveyorMode>(gameStateEntity);

        // Пока кнопка поворота удерживается и мы в режиме строительства — добавляем RotateRequest к превью
        if (rotateHeld && isInBuildingMode)
        {
            if (SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var previewEntity) && !SystemAPI.HasComponent<RotateRequest>(previewEntity))
            {
                ecb.AddComponent<RotateRequest>(previewEntity);
            }
        }


        // Если мы в режиме строительства зданий (не конвейеров!) и есть скролл, создаем запрос на изменение высоты.
        if (isInBuildingMode && math.abs(quickbarScrollDelta) > 0.01f)
        {
            var heightReq = ecb.CreateEntity();
            ecb.AddComponent(heightReq, new AdjustBuildingHeightRequest
            {
                // Нормализуем значение, чтобы получить -1 или 1
                ScrollDelta = math.sign(quickbarScrollDelta)
            });
            // Обнуляем, чтобы этот же скролл не вызвал смену предмета в хотбаре
            quickbarScrollDelta = 0f;
        }


        // Обработка правой кнопки мыши: если в режиме строительства, то это запрос на выход,
        // иначе - запрос на взаимодействие.
        if (rightClickRequested)
        {
            if (isInWireMode)
            {
                if (SystemAPI.TryGetSingletonEntity<PendingWire>(out var pendingWireEntity))
                {
                    ecb.DestroyEntity(pendingWireEntity);
                }
                else if (SystemAPI.TryGetSingletonEntity<PendingWireRemoval>(out var pendingRemovalEntity))
                {
                    ecb.DestroyEntity(pendingRemovalEntity);
                }
                else
                {
                    var r = ecb.CreateEntity();
                    ecb.AddComponent(r, new ExitWirePlacementModeRequest());
                }
            }
            else if (isInConveyorMode)
            {
                var r = ecb.CreateEntity();
                ecb.AddComponent(r, new RemoveConveyorUnderCursorRequest());
            }
            else if (isInBuildingMode)
            {
                var r = ecb.CreateEntity();
                ecb.AddComponent(r, new ExitBuildingModeRequest());
            }
            else if (!isUI)
            {
                var r = ecb.CreateEntity();
                ecb.AddComponent(r, new InteractionRequest());
            }
            rightClickRequested = false;
        }

        var controllerData = SystemAPI.GetSingleton<PlayerControllerData>();
        double now = SystemAPI.Time.ElapsedTime;
        bool jumpBuffered = (now - lastJumpTime) <= controllerData.JumpBufferDuration;

        float2 currentMove = isUI ? float2.zero : moveInput;
        float2 currentLook = isUI ? float2.zero : lookInput;
        bool currentSprint = !isUI && sprintInput;
        bool currentJump = !isUI && jumpBuffered;
        bool currentPrimaryAction = !isUI && primaryActionInput;
        int currentQuickbarDigit = isUI ? 0 : quickbarDigitPressed;
        float currentQuickbarScroll = isUI ? 0f : quickbarScrollDelta;
        
        inputs.ValueRW.move = currentMove;
        inputs.ValueRW.look = currentLook;
        inputs.ValueRW.sprint = currentSprint;
        inputs.ValueRW.jump = currentJump;
        inputs.ValueRW.isMouseControl = true;
        inputs.ValueRW.secondaryActionDown = false;
        inputs.ValueRW.PrimaryAction = currentPrimaryAction;

        // Обновляем поля квикбара в компоненте InputsData.
        inputs.ValueRW.QuickbarDigitKeyPressed = currentQuickbarDigit;
        inputs.ValueRW.QuickbarScrollDelta = currentQuickbarScroll;

        jumpRequested = false;
        
        // Сбрасываем одноразовый ввод для квикбара.
        quickbarDigitPressed = 0;
        quickbarScrollDelta = 0f;
    }
}