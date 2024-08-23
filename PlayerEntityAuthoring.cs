using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Dev100.ECS
{
    public struct PlayerTag : IComponentData {}
    public struct InitializePlayerTag : IComponentData {}

    public struct BonusLevelPoints : IComponentData
    {
        public int Value;
    }

    public struct PlayerTransformReference : IComponentData
    {
        public UnityObjectRef<Transform> Value;
    }

    public class PlayerEntityAuthoring : MonoBehaviour
    {
        private class PlayerEntityBaker : Baker<PlayerEntityAuthoring>
        {
            public override void Bake(PlayerEntityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<PlayerTag>(entity);
                AddComponent<InitializePlayerTag>(entity);
                AddComponent<BonusLevelPoints>(entity);
            }
        }
    }

    [UpdateInGroup(typeof(InitializationSystemGroup), OrderLast = true)]
    public partial struct InitializePlayerSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<InitializePlayerTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if(Player.Instance == null) return;
            var playerEntity = SystemAPI.GetSingletonEntity<InitializePlayerTag>();
            state.EntityManager.AddComponentData(playerEntity, new PlayerTransformReference
            {
                Value = Player.Instance.transform
            });
            state.EntityManager.RemoveComponent<InitializePlayerTag>(playerEntity);
            state.EntityManager.AddComponentObject(playerEntity, Player.Instance.transform);
        }
    }
    
    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct UpdatePlayerEntityTransformSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (transform, playerTransformReference) in SystemAPI.Query<RefRW<LocalTransform>, SystemAPI.ManagedAPI.UnityEngineComponent<Transform>>().WithAll<PlayerTag>())
            {
                if (playerTransformReference.Value == null) continue;
                transform.ValueRW.Position = playerTransformReference.Value.position;
                transform.ValueRW.Rotation = playerTransformReference.Value.rotation;
            }
        }
    }
}