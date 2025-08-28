using Unity.Entities;
using Unity.Collections;

namespace Energy.Core
{
    // Теперь это не статический ID, а динамически присваиваемый ID подсети.
    public struct NetworkNode : IComponentData
    {
        public FixedString64Bytes Name;
        public int SubnetId; // Переименовали из NetworkId в SubnetId
    }
}