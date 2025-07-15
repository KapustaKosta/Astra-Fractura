using Unity.Entities;

/// <summary>
/// Простая система для синхронизации поля NPCComponent.Target с текущей целью из ActiveGoal.
/// Это позволяет UI отображать актуальную цель NPC, не завися напрямую от сложной логики AI.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(NPCTaskArbiterSystem))] // Запускается после того, как Арбитр мог изменить цель.
public partial class NPCUISyncSystem : SystemBase
{
    protected override void OnUpdate()
    {
        Entities
            .ForEach((Entity npcEntity, ref NPCComponent npcData) =>
            {
                Entity actualTarget = Entity.Null;
                
                // Проверяем, есть ли у NPC активная цель.
                if (SystemAPI.HasComponent<ActiveGoal>(npcEntity))
                {
                    actualTarget = SystemAPI.GetComponent<ActiveGoal>(npcEntity).Target;
                }

                // Обновляем поле в NPCComponent только если оно изменилось.
                if (npcData.Target != actualTarget)
                {
                    npcData.Target = actualTarget;
                }
            }).Schedule();
    }
}