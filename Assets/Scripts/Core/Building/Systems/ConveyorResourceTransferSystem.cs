using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Система перемещения ресурсов по конвеерам между зданиями.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
public partial class ConveyorResourceTransferSystem : SystemBase
{
    protected override void OnUpdate()
    {
        var entityQuery = SystemAPI.QueryBuilder()
            .WithAll<ConveyorComponent, ConveyorResourceBuffer>()
            .Build();
        var entityArray = entityQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
        foreach (var entity in entityArray)
        {
            var buffer = EntityManager.GetBuffer<ConveyorResourceBuffer>(entity);
            for (int i = 0; i < buffer.Length; i++)
            {
                var res = buffer[i];
                res.Progress += SystemAPI.Time.DeltaTime; // Можно добавить скорость
                if (res.Progress >= 1f)
                {
                    // Ресурс достиг конца конвеера ? передать в здание-приёмник
                    // (Реализация передачи зависит от вашей архитектуры)
                    buffer.RemoveAt(i);
                    i--;
                }
                else
                {
                    buffer[i] = res;
                }
            }
        }
        entityArray.Dispose();
    }
}
