using UnityEngine;

namespace Game.Workshop
{
    [CreateAssetMenu(fileName = "STATION_", menuName = "Game/Workshop/Station Type")]
    public class StationType : ScriptableObject
    {
        [Tooltip("Уникальный ID для этого типа станции.")]
        public int StationTypeID;
        [Tooltip("Имя, отображаемое в UI.")]
        public string StationName;
        [Tooltip("Иконка для UI.")]
        public Sprite Icon;

    }
}