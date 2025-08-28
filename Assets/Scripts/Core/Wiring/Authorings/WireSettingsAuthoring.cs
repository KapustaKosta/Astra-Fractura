using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Wiring
{
    /// <summary>
    /// MonoBehaviour used to author global settings for the wiring system.  Place this on a GameObject
    /// in your scene and configure the exposed fields to control connector layers, sizes and capacities.
    /// </summary>
    public class WireSettingsAuthoring : MonoBehaviour
    {
        [Header("Connector")]
        [Tooltip("Layer used for wire connector entities.  Set this to a dedicated layer so raycasts can hit connectors without interference.")]
        public LayerMask connectorLayer = 0;

        [Tooltip("Scale of the connector visual (diameter in meters).")]
        public float connectorScale = 0.1f;

        [Header("Wire capacities")]
        [Tooltip("Maximum power that a level 1 wire can transport per second.")]
        public float level1Capacity = 10f;

        [Tooltip("Maximum power that a level 2 wire can transport per second.")]
        public float level2Capacity = 25f;

        [Tooltip("Maximum power that a level 3 wire can transport per second.")]
        public float level3Capacity = 50f;

        /// <summary>
        /// Baker converts authoring data into an ECS singleton.  Only one instance of this component
        /// should exist in the world.  All systems reference the singleton to read wiring parameters.
        /// </summary>
        private class Baker : Baker<WireSettingsAuthoring>
        {
            public override void Bake(WireSettingsAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new WireSettings
                {
                    ConnectorLayer = GetFirstLayer(authoring.connectorLayer),
                    ConnectorScale = math.max(0.01f, authoring.connectorScale),
                    Level1Capacity = math.max(0.0f, authoring.level1Capacity),
                    Level2Capacity = math.max(0.0f, authoring.level2Capacity),
                    Level3Capacity = math.max(0.0f, authoring.level3Capacity)
                });
            }

            /// <summary>
            /// Returns the index of the first enabled layer in a LayerMask.  If no layer is enabled,
            /// returns -1 to indicate that a valid layer could not be determined.
            /// </summary>
            private static int GetFirstLayer(LayerMask mask)
            {
                int value = mask.value;
                if (value == 0) return -1;
                for (int i = 0; i < 32; i++)
                {
                    if ((value & (1 << i)) != 0)
                        return i;
                }
                return -1;
            }
        }
    }
}