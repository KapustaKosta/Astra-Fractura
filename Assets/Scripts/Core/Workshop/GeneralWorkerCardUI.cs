using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Workshop
{
    /// <summary>
    /// Управляет UI-элементом (карточкой), представляющим рабочего,
    /// назначенного на цех в целом (general worker).
    /// </summary>
    public class GeneralWorkerCardUI : MonoBehaviour
    {
        [Header("UI References")]
        public TextMeshProUGUI npcNameText;
        public Button unassignButton;

        private EntityManager _em;
        private Entity _workshop;
        private Entity _npcEntity;

        /// <summary>
        /// Связывает карточку с данными о цехе и NPC, настраивая отображение и обработчики событий.
        /// </summary>
        /// <param name="workshop">Сущность цеха.</param>
        /// <param name="npcEntity">Сущность назначенного NPC.</param>
        /// <param name="em">Ссылка на EntityManager.</param>
        public void Bind(Entity workshop, Entity npcEntity, EntityManager em)
        {
            _workshop = workshop;
            _npcEntity = npcEntity;
            _em = em;

            if (!_em.Exists(_npcEntity))
            {
                gameObject.SetActive(false);
                return;
            }

            var npcName = _em.GetComponentData<NPCComponent>(_npcEntity).Name.ToString();
            npcNameText.text = npcName;

            unassignButton.onClick.RemoveAllListeners();
            unassignButton.onClick.AddListener(OnUnassign);
        }

        /// <summary>
        /// Обработчик нажатия кнопки "снять назначение". Создает сущность-запрос
        /// UnassignWorkerFromWorkshopRequest для снятия NPC с цеха.
        /// </summary>
        private void OnUnassign()
        {
            var e = _em.CreateEntity();
            _em.AddComponentData(e, new UnassignWorkerFromWorkshopRequest
            {
                Workshop = _workshop,
                NpcEntity = _npcEntity
            });
        }
    }
}