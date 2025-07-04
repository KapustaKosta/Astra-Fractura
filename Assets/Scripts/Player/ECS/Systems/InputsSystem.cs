using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using StarterAssets;
using System;

/// <summary>
/// ECS-система, отвечающая за обработку ввода от пользователя.
/// Преобразует события ввода из MonoBehaviour-мира (через StarterAssetsInputs)
/// в ECS-компоненты InputsData и запросы. Реализует буферизацию прыжка.
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

    private EntityQuery playerInitializedQuery;

    /// <summary>
    /// Длительность (в секундах), в течение которой сигнал прыжка считается "свежим" для буферизации.
    /// </summary>
    private const double JumpBufferDuration = 0.2;

    /// <summary>
    /// Вызывается при создании системы. Гарантирует, что синглтон GameState существует
    /// и инициализирует запрос для проверки инициализации игрока.
    /// </summary>
    protected override void OnCreate()
    {
        RequireForUpdate<GameState>();
        playerInitializedQuery = GetEntityQuery(typeof(PlayerInitializedTag));
    }

    /// <summary>
    /// Вызывается при первом запуске системы. Подписывается на события ввода от StarterAssetsInputs
    /// и сбрасывает все флаги запросов ввода.
    /// </summary>
    protected override void OnStartRunning()
    {
        StarterAssetsInputs.onMove += OnMove;
        StarterAssetsInputs.onLook += OnLook;
        StarterAssetsInputs.onSprint += OnSprint;
        StarterAssetsInputs.onJump += OnJump;
        StarterAssetsInputs.onInventory += OnInventory;
        StarterAssetsInputs.onRightClick += OnRightClick;
        // Debug.Log("InputsSystem: Input initialized");

        inventoryRequested = false;
        rightClickRequested = false;
        jumpRequested = false;
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
    }

    /// <summary>
    /// Обработчик события движения.
    /// </summary>
    /// <param name="input">Вектор движения.</param>
    private void OnMove(Vector2 input) => moveInput = input;

    /// <summary>
    /// Обработчик события обзора.
    /// </summary>
    /// <param name="input">Вектор обзора.</param>
    private void OnLook(Vector2 input) => lookInput = input;

    /// <summary>
    /// Обработчик события спринта.
    /// </summary>
    /// <param name="isPressed">True, если кнопка спринта нажата, false в противном случае.</param>
    private void OnSprint(bool isPressed) => sprintInput = isPressed;

    /// <summary>
    /// Обработчик события прыжка. Устанавливает флаг запроса прыжка и время последнего прыжка.
    /// </summary>
    private void OnJump() { jumpRequested = true; lastJumpTime = SystemAPI.Time.ElapsedTime; }

    /// <summary>
    /// Обработчик события открытия/закрытия инвентаря. Устанавливает флаг запроса инвентаря.
    /// </summary>
    private void OnInventory() => inventoryRequested = true;

    /// <summary>
    /// Обработчик события правой кнопки мыши (вторичное действие). Устанавливает флаг запроса.
    /// </summary>
    private void OnRightClick() => rightClickRequested = true;

    /// <summary>
    /// Вызывается каждый кадр. Обновляет InputsData для сущности игрока
    /// и создает ECS-запросы на основе флагов ввода.
    /// </summary>
    protected override void OnUpdate()
    {
        if (playerInitializedQuery.IsEmpty)
            return;

        double now = SystemAPI.Time.ElapsedTime;
        bool jumpBuffered = (now - lastJumpTime) <= JumpBufferDuration;

        var ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        if (inventoryRequested)
        {
            // Debug.Log("<color=yellow>[InputsSystem]</color> ToggleInventoryRequest");
            var invE = ecb.CreateEntity();
            ecb.AddComponent(invE, new ToggleInventoryRequest());
            inventoryRequested = false;
        }
        if (rightClickRequested)
        {
            var rcE = ecb.CreateEntity();
            ecb.AddComponent(rcE, new InteractionRequest());
            rightClickRequested = false;
        }

        bool isUI = SystemAPI.GetSingleton<GameState>().CurrentMode == GameMode.UI;
        float2 currentMove = isUI ? float2.zero : new float2(moveInput.x, moveInput.y);
        float2 currentLook = isUI ? float2.zero : new float2(lookInput.x, lookInput.y);
        bool currentSprint = isUI ? false : sprintInput;
        bool currentJump = !isUI && jumpBuffered;

        Entities
            .ForEach((ref InputsData inputsData) =>
            {
                inputsData.move = currentMove;
                inputsData.look = currentLook;
                inputsData.sprint = currentSprint;
                inputsData.jump = currentJump;
                inputsData.isMouseControl = true;
                inputsData.secondaryActionDown = false;
            })
            .WithoutBurst().Run();

        jumpRequested = false;
    }
}