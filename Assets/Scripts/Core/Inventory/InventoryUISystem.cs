using Unity.Entities;

[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class InventoryUISystem : SystemBase
{
    private EntityQuery _qOpenTrade;
    private EntityQuery _qOpenInv;

    protected override void OnCreate()
    {
        // Запросы на открытие
        _qOpenTrade = GetEntityQuery(ComponentType.ReadOnly<OpenTradeUIRequest>());
        _qOpenInv = GetEntityQuery(ComponentType.ReadOnly<OpenInventoryUIRequest>());

        // Обновляться только когда есть хотя бы один из запросов
        RequireAnyForUpdate(_qOpenTrade, _qOpenInv);
    }

    protected override void OnUpdate()
    {
        var endEcb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);

        // 1) Обработка запросов трейда (поселение)
        if (!_qOpenTrade.IsEmptyIgnoreFilter)
        {
            var reqs = _qOpenTrade.ToComponentDataArray<OpenTradeUIRequest>(Unity.Collections.Allocator.Temp);
            var ents = _qOpenTrade.ToEntityArray(Unity.Collections.Allocator.Temp);

            for (int i = 0; i < reqs.Length; i++)
            {
                var req = reqs[i];
                var e = ents[i];

                if (TradeUI.Instance == null)
                {
                    UnityEngine.Debug.LogError("[InventoryUISystem] TradeUI.Instance == null — не могу открыть окно Trade.");
                    endEcb.DestroyEntity(e);
                    continue;
                }

                UnityEngine.Debug.Log($"<color=orange>[InventoryUISystem]</color> OpenTradeUIRequest -> target={req.Target} type=General");

                TradeUI.Instance.Show(req.Target, InventoryType.General);

                if (SystemAPI.TryGetSingletonEntity<GameState>(out var gs))
                {
                    endEcb.AddComponent<InUIMode>(gs);
                    endEcb.SetComponent(gs, new UIState { ActiveUIType = UIType.Trade, ActiveUITarget = req.Target });
                }
                endEcb.DestroyEntity(e);
            }
        }

        // 2) Обработка запросов инвентаря (печка Input/Output)
        if (!_qOpenInv.IsEmptyIgnoreFilter)
        {
            var reqs = _qOpenInv.ToComponentDataArray<OpenInventoryUIRequest>(Unity.Collections.Allocator.Temp);
            var ents = _qOpenInv.ToEntityArray(Unity.Collections.Allocator.Temp);

            for (int i = 0; i < reqs.Length; i++)
            {
                var req = reqs[i];
                var e = ents[i];

                if (TradeUI.Instance == null)
                {
                    UnityEngine.Debug.LogError("[InventoryUISystem] TradeUI.Instance == null — не могу открыть окно Trade (Input/Output).");
                    endEcb.DestroyEntity(e);
                    continue;
                }

                UnityEngine.Debug.Log($"<color=orange>[InventoryUISystem]</color> OpenInventoryUIRequest -> target={req.Target} type={req.Type}");

                TradeUI.Instance.Show(req.Target, req.Type);

                if (SystemAPI.TryGetSingletonEntity<GameState>(out var gs))
                {
                    endEcb.AddComponent<InUIMode>(gs);
                    endEcb.SetComponent(gs, new UIState { ActiveUIType = UIType.Trade, ActiveUITarget = req.Target });
                }
                endEcb.DestroyEntity(e);
            }
        }
    }
}
