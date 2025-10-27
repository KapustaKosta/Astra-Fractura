using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Energy.Core;

/// <summary>
/// Основная система, управляющая логикой добычи ресурсов карьером.
/// Она определяет, должен ли карьер работать, запрашивает энергию,
/// и с учетом полученной мощности накапливает прогресс добычи,
/// создавая запрос на получение ресурса по завершении цикла.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(QuarryTargetingSystem))] // Должна работать после определения цели
[UpdateBefore(typeof(Energy.Core.Systems.EnergyDispatchSystem))] // Но до распределения энергии
public partial class QuarryHarvestingSystem : SystemBase
{
    /// <summary>
    /// Выполняется каждый кадр. Проверяет условия для работы карьера (включен ли он, не полон ли инвентарь),
    /// запрашивает энергию, рассчитывает эффективность на основе полученной мощности и накапливает прогресс добычи.
    /// </summary>
    protected override void OnUpdate()
    {
        var ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged).AsParallelWriter();

        float deltaTime = SystemAPI.Time.DeltaTime;

        var resourceNodeLookup = SystemAPI.GetComponentLookup<ResourceNode>(true);
        resourceNodeLookup.Update(this);
        
        var powerUsageLookup = SystemAPI.GetComponentLookup<Energy.Core.NetLinkUsage>(true);
        powerUsageLookup.Update(this);
        
        var parentLookup = SystemAPI.GetComponentLookup<Parent>(true);
        parentLookup.Update(this);
        
        var networkNodeLookup = SystemAPI.GetComponentLookup<Energy.Core.NetworkNode>(true);
        networkNodeLookup.Update(this);
        
        Entities
            .WithAll<QuarryTag>()
            .WithReadOnly(resourceNodeLookup)
            .WithReadOnly(powerUsageLookup)
            .WithReadOnly(parentLookup)
            .WithReadOnly(networkNodeLookup) 
            .ForEach((Entity entity, int entityInQueryIndex, ref QuarryState state, ref ConsumerLoad load, in QuarrySettings settings) =>
            {
                bool isInventoryFull = SystemAPI.HasComponent<QuarryInventoryFullTag>(entity);
                bool hasValidTarget = state.TargetResourceNode != Entity.Null && resourceNodeLookup.HasComponent(state.TargetResourceNode);

                // Карьер хочет работать, если он включен, инвентарь не полон и есть цель.
                bool wantsToWork = state.IsOnline && !isInventoryFull && hasValidTarget;

                // Запрашиваем энергию или обнуляем запрос.
                if (wantsToWork)
                {
                    load.CurrentKW = settings.EnergyConsumptionKW;
                }
                else
                {
                    load.CurrentKW = 0f;
                    state.LastHarvestTime = 0f; 
                    return;
                }
                
                // Ищем владельца NetworkNode, чтобы прочитать полученную энергию.
                Entity nodeOwner = Entity.Null;
                if (networkNodeLookup.HasComponent(entity))
                {
                    nodeOwner = entity;
                }
                else
                {
                    // Если на самой сущности нет, ищем по иерархии родителей
                    Entity current = entity;
                    for (int i = 0; i < 8; i++) // Цикл безопасности
                    {
                        if (parentLookup.HasComponent(current))
                        {
                            current = parentLookup[current].Value;
                            if (networkNodeLookup.HasComponent(current))
                            {
                                nodeOwner = current;
                                break;
                            }
                        }
                        else break; 
                    }
                }
                
                // Читаем фактически доставленную мощность из NetLinkUsage.
                float powerDelivered = 0f;
                if (nodeOwner != Entity.Null && powerUsageLookup.HasComponent(nodeOwner))
                {
                    powerDelivered = powerUsageLookup[nodeOwner].InUsedKW;
                }

                // Рассчитываем эффективность как отношение полученной мощности к требуемой.
                float requiredPower = settings.EnergyConsumptionKW;
                float efficiency = (requiredPower > 1e-6f) ? math.saturate(powerDelivered / requiredPower) : 0f;
                
                if (efficiency <= 1e-6f) return; // Если энергии нет, ничего не делаем.
                
                // Накапливаем прогресс добычи с учетом эффективности.
                float progressPerSecond = 1f / settings.HarvestInterval;
                float currentProgress = progressPerSecond * efficiency * deltaTime;
                state.LastHarvestTime += currentProgress;

                // Если цикл завершен (прогресс >= 1), создаем запрос на получение ресурса.
                if (state.LastHarvestTime >= 1f)
                {
                    var requestEntity = ecb.CreateEntity(entityInQueryIndex);
                    ecb.AddComponent(entityInQueryIndex, requestEntity, new ValidateHarvestAttemptRequest
                    {
                        Harvester = entity,
                        TargetResourceNode = state.TargetResourceNode
                    });
                    
                    state.LastHarvestTime -= 1f; // Сбрасываем прогресс для следующего цикла.
                }
                
            }).ScheduleParallel();
    }
}