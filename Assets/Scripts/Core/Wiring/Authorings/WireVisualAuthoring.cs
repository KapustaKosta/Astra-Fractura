using Unity.Entities;
using UnityEngine;

namespace Wiring
{
    /// <summary>
    /// Authoring component for providing a visual prefab used when instantiating wires.
    /// Assign any cylinder or thin capsule mesh here; the baker will convert it into an ECS entity.
    /// At runtime the system will instantiate this prefab to render wires between connectors.
    /// </summary>
    public class WireVisualAuthoring : MonoBehaviour
    {
        [Tooltip("Prefab used to visualize a wire.  Typically a cylinder aligned along its local Y axis.")]
        public GameObject wirePrefab;

        /// <summary>
        /// Baker stores the entity reference to the provided prefab in a singleton for easy lookup.
        /// </summary>
        private class Baker : Baker<WireVisualAuthoring>
        {
            public override void Bake(WireVisualAuthoring authoring)
            {
                if (authoring.wirePrefab == null)
                    return;

                var entity = GetEntity(TransformUsageFlags.None);
                var prefabEntity = GetEntity(authoring.wirePrefab, TransformUsageFlags.Dynamic);

                // Create a singleton component containing the prefab reference.
                AddComponent(entity, new WireVisualPrefab
                {
                    Prefab = prefabEntity
                });
            }
        }
    }
}