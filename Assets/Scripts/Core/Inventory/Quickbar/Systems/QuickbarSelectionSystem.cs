using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Система обрабатывает ввод игрока (цифры, колесо мыши) для выбора активного слота в квикбаре.
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(InputsSystem))]
public partial class QuickbarSelectionSystem : SystemBase
{
    // Квикбар имеет 8 слотов (индексы 0-7)
    private const int QuickbarSize = 8;

    protected override void OnUpdate()
    {
        // Система не должна работать, если открыт UI, блокирующий геймплей
        if (SystemAPI.HasComponent<InUIMode>(SystemAPI.GetSingletonEntity<GameState>()))
        {
            return;
        }

        Entities
            .WithAll<PlayerTag>()
            .ForEach((ref ActiveQuickbarSlot activeSlot, in InputsData inputs) =>
            {
                int currentIndex = activeSlot.Index;
                bool changed = false;

                // 1. Обработка нажатия цифровых клавиш
                if (inputs.QuickbarDigitKeyPressed > 0 && inputs.QuickbarDigitKeyPressed <= QuickbarSize)
                {
                    currentIndex = inputs.QuickbarDigitKeyPressed - 1; // Клавиша '1' -> индекс 0
                    changed = true;
                }
                
                // 2. Обработка прокрутки колеса мыши
                if (inputs.QuickbarScrollDelta != 0)
                {
                    // Прокрутка вниз дает отрицательное значение, вверх - положительное
                    if (inputs.QuickbarScrollDelta < 0)
                    {
                        currentIndex++;
                    }
                    else if (inputs.QuickbarScrollDelta > 0)
                    {
                        currentIndex--;
                    }
                    changed = true;
                }

                if (changed)
                {
                    // Логика "зацикливания" индекса в пределах [0, 7]
                    if (currentIndex >= QuickbarSize)
                    {
                        currentIndex = 0;
                    }
                    if (currentIndex < 0)
                    {
                        currentIndex = QuickbarSize - 1;
                    }
                    activeSlot.Index = currentIndex;
                }

            }).Schedule();
    }
}