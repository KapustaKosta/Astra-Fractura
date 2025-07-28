using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using StarterAssets;
using System;

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

    /// <summary>
    /// Вызывается при создании системы. Гарантирует, что синглтон GameState и игрок существуют.
    /// </summary>
    protected override void OnCreate()
    {
        RequireForUpdate<GameState>();
        RequireForUpdate<PlayerControllerData>();
    }

    /// <summary>
    /// Вызывается при первом запуске системы. Подписывается на события ввода.
    /// </summary>
    protected override void OnStartRunning()
    {
        StarterAssetsInputs.onMove += OnMove;
        StarterAssetsInputs.onLook += OnLook;
        StarterAssetsInputs.onSprint += OnSprint;
        StarterAssetsInputs.onJump += OnJump;
        StarterAssetsInputs.onInventory += OnInventory;
        StarterAssetsInputs.onRightClick += OnRightClick;
        StarterAssetsInputs.onPrimaryAction += OnPrimaryAction;
        StarterAssetsInputs.onRotate += OnRotate;
        
        inventoryRequested = false;
        rightClickRequested = false;
        jumpRequested = false;
        primaryActionInput = false;
        lastJumpTime = double.NegativeInfinity;
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

    /// <summary>
    /// Обработчик события инвентаря.
    /// </summary>
    private void OnInventory() => inventoryRequested = true;

    /// <summary>
    /// Обработчик события вторичного действия.
    /// </summary>
    private void OnRightClick() => rightClickRequested = true;

    /// <summary>
    /// Обработчик для основного действия (ЛКМ).
    /// </summary>
    private void OnPrimaryAction(bool isPressed) => primaryActionInput = isPressed;

    /// <summary>
    /// Обработчик события поворота (isPressed: true — кнопка зажата, false — отпущена).
    /// </summary>
    private void OnRotate(bool isPressed) => rotateHeld = isPressed;

    /// <summary>
    /// Вызывается каждый кадр. Обновляет InputsData и создает одноразовые запросы для UI.
    /// </summary>
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(World.Unmanaged);

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

        // Пока кнопка поворота удерживается и мы в режиме строительства — добавляем RotateRequest к превью
        if (rotateHeld && isInBuildingMode)
        {
            if (SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var previewEntity))
            {
                if (!SystemAPI.HasComponent<RotateRequest>(previewEntity))
                    ecb.AddComponent<RotateRequest>(previewEntity);
            }
        }
        else
        {
            if (SystemAPI.TryGetSingletonEntity<BuildingPreviewTag>(out var previewEntity))
            {
                if (SystemAPI.HasComponent<RotateRequest>(previewEntity))
                    ecb.RemoveComponent<RotateRequest>(previewEntity);
            }
        }

        // Обработка правой кнопки мыши: если в режиме строительства, то это запрос на выход,
        // иначе - запрос на взаимодействие.
        if (rightClickRequested)
        {
            if (isInBuildingMode) 
            {
                var exitE = ecb.CreateEntity();
                ecb.AddComponent(exitE, new ExitBuildingModeRequest());
            }
            else // Иначе, это обычное взаимодействие.
            {
                var rcE = ecb.CreateEntity();
                ecb.AddComponent(rcE, new InteractionRequest());
            }
            rightClickRequested = false;
        }
        
        var controllerData = SystemAPI.GetSingleton<PlayerControllerData>();
        double now = SystemAPI.Time.ElapsedTime;
        bool jumpBuffered = (now - lastJumpTime) <= controllerData.JumpBufferDuration;
        
        // Определяем финальные значения для записи, блокируя ввод в режиме UI
        float2 currentMove = isUI ? float2.zero : new float2(moveInput.x, moveInput.y);
        float2 currentLook = isUI ? float2.zero : new float2(lookInput.x, lookInput.y);
        bool currentSprint = isUI ? false : sprintInput;
        bool currentJump = !isUI && jumpBuffered;
        // Мы передаем сырое состояние кнопки, а другие системы решат, можно ли выполнять действие
        bool currentPrimaryAction = primaryActionInput;

        // Обновляем синглтон InputsData, который служит источником правды о вводе для других систем
        var inputs = SystemAPI.GetSingletonRW<InputsData>();
        inputs.ValueRW.move = currentMove;
        inputs.ValueRW.look = currentLook;
        inputs.ValueRW.sprint = currentSprint;
        inputs.ValueRW.jump = currentJump;
        inputs.ValueRW.isMouseControl = true;
        inputs.ValueRW.secondaryActionDown = false;
        inputs.ValueRW.PrimaryAction = currentPrimaryAction;

        
        jumpRequested = false;
    }
}