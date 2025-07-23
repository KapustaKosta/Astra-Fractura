using Unity.Entities;
using Unity.Mathematics;

/// <summary>
/// Система обрабатывает ввод игрока (цифры, колесо мыши) для выбора активного слота в квикбаре.
/// <para>
/// Ее единственная задача — преобразовать сырой ввод из компонента InputsData в изменение
/// индекса в компоненте ActiveQuickbarSlot. Она не знает о предметах или игровой логике.
/// </para>
/// </summary>
[UpdateInGroup(typeof(SimulationSystemGroup))]
[UpdateAfter(typeof(InputsSystem))]
public partial class QuickbarSelectionSystem : SystemBase
{
    // Размер квикбара жестко задан. (можем вынести в редактор и сделать адаптивный UI
    // ,но временно сделан фиксированным числом)
    private const int QuickbarSize = 8;

    /// <summary>
    /// Вызывается каждый кадр для обработки ввода и смены активного слота.
    /// </summary>
    protected override void OnUpdate()
    {
        // Система не должна обрабатывать геймплейный ввод, когда открыт UI (инвентарь, меню и т.д.).
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

                // Блок 1: Обработка прямого выбора слота по нажатию цифровой клавиши.
                if (inputs.QuickbarDigitKeyPressed > 0 && inputs.QuickbarDigitKeyPressed <= QuickbarSize)
                {
                    currentIndex = inputs.QuickbarDigitKeyPressed - 1; // Клавиша '1' -> индекс 0
                    changed = true;
                }
                
                // 2. Обработка прокрутки колеса мыши
                if (inputs.QuickbarScrollDelta != 0)
                {
                    // Прокрутка вниз дает отрицательное значение, вверх - положительное
                    if (inputs.QuickbarScrollDelta < 0) currentIndex++;
                    else if (inputs.QuickbarScrollDelta > 0) currentIndex--;
                    changed = true;
                }

                // Если произошло изменение, применяем его и "зацикливаем" индекс.
                if (changed)
                {
                    // Если индекс вышел за правую границу (7), он становится 0.
                    if (currentIndex >= QuickbarSize) currentIndex = 0;
                    // Если индекс вышел за левую границу (0), он становится 7.
                    if (currentIndex < 0) currentIndex = QuickbarSize - 1;
                    
                    activeSlot.Index = currentIndex;
                }

            }).Schedule();
    }
}