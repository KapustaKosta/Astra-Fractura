using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// Простая перцепция игрока по радиусу (без NPCBrain).
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateBefore(typeof(EnemyTaskArbiterSystem))] 
public partial class EnemyPerceptionSystem : SystemBase
{
    private bool _warnedNoPlayer;

    protected override void OnUpdate()
    {
        if (!SystemAPI.TryGetSingletonEntity<PlayerTag>(out var player))
        {
            if (!_warnedNoPlayer)
            {
                //Debug.LogWarning("[EnemyPerception] No PlayerTag singleton found. Place player entity with PlayerTag.");
                _warnedNoPlayer = true;
            }
            return;
        }
        
        // Если у игрока есть DeadTag, считаем, что игрока нет на сцене.
        if (SystemAPI.HasComponent<DeadTag>(player))
        {
            return; 
        }

        if (!SystemAPI.HasComponent<LocalToWorld>(player)) return;

        var playerLTW = SystemAPI.GetComponent<LocalToWorld>(player);

        var ecb = SystemAPI
            .GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(World.Unmanaged);
        
        Entities
            .WithAll<HostileNPCTag>()
            .WithNone<IsDeadTag>() 
            .ForEach((Entity e, in LocalToWorld ltw, in EnemyStats stats) =>
            {
                float dist = math.distance(ltw.Position, playerLTW.Position);
                bool inAggro = dist <= stats.AggroRadius;

                bool has = SystemAPI.HasComponent<EnemySeenPlayer>(e);
                if (inAggro)
                {
                    if (has)
                    {
                        var data = SystemAPI.GetComponent<EnemySeenPlayer>(e);
                        if (data.Player != player)
                        {
                            ecb.SetComponent(e, new EnemySeenPlayer { Player = player });
                            //Debug.Log($"[Perception] {e.Index} update target=player dist={dist:F2}");
                        }
                    }
                    else
                    {
                        ecb.AddComponent(e, new EnemySeenPlayer { Player = player });
                        //Debug.Log($"[Perception] {e.Index} ENTER aggro: dist={dist:F2}");
                    }
                }
                else if (has)
                {
                    ecb.RemoveComponent<EnemySeenPlayer>(e);
                    //Debug.Log($"[Perception] {e.Index} EXIT aggro: dist={dist:F2}");
                }
            })
            .WithoutBurst().Run();
    }
}