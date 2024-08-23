using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Dev100.ECS
{
    public struct EnemyTag : IComponentData {}

    public struct EnemyRisingProperties : IComponentData
    {
        public float Speed;
        public float YValue;
    }

    public struct EnemyMoveProperties : IComponentData
    {
        public float Speed;
        public float YValue;
    }

    public struct EntityHitPoints : IComponentData
    {
        public int Value;
    }
    
    public class EnemyEntityAuthoring : MonoBehaviour
    {
        public float RisingSpeed;
        public float MoveSpeed;
        public float YValue;
        public int HitPoints;
        
        private class EnemyEntityBaker : Baker<EnemyEntityAuthoring>
        {
            public override void Bake(EnemyEntityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<EnemyTag>(entity);
                AddComponent(entity, new EnemyRisingProperties
                {
                    Speed = authoring.RisingSpeed,
                    YValue = authoring.YValue
                });
                AddComponent(entity, new EnemyMoveProperties
                {
                    Speed = authoring.MoveSpeed,
                    YValue = authoring.YValue
                });
                AddComponent(entity, new EntityHitPoints
                {
                    Value = authoring.HitPoints
                });
            }
        }
    }

    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct EnemyMovementSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerTag>();
            state.RequireForUpdate<BonusLevelActiveTag>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            var playerEntity = SystemAPI.GetSingletonEntity<PlayerTag>();
            var playerPosition = SystemAPI.GetComponent<LocalTransform>(playerEntity).Position;
            
            foreach (var (transform, risingProperties, entity) in SystemAPI.Query<RefRW<LocalTransform>, EnemyRisingProperties>().WithEntityAccess())
            {
                transform.ValueRW.Position += math.up() * risingProperties.Speed * deltaTime;
                var lookDirection = playerPosition.xz - transform.ValueRO.Position.xz;
                var lookRotation = quaternion.LookRotation(new float3(lookDirection.x, 0f, lookDirection.y), math.up());
                transform.ValueRW.Rotation = lookRotation;
                if (!(transform.ValueRW.Position.y >= risingProperties.YValue)) continue;
                transform.ValueRW.Position.y = risingProperties.YValue;
                ecb.RemoveComponent<EnemyRisingProperties>(entity);
            }

            foreach (var (transform, moveProperties) in SystemAPI.Query<RefRW<LocalTransform>, EnemyMoveProperties>().WithNone<EnemyRisingProperties>())
            {
                var lookDirection = playerPosition.xz - transform.ValueRO.Position.xz;
                lookDirection = math.normalize(lookDirection);
                var moveDirection = new float3(lookDirection.x, 0f, lookDirection.y);
                var lookRotation = quaternion.LookRotation(moveDirection, math.up());
                transform.ValueRW.Rotation = lookRotation;
                transform.ValueRW.Position += moveDirection * moveProperties.Speed * deltaTime;
                transform.ValueRW.Position.y = moveProperties.YValue;
            }
            
            ecb.Playback(state.EntityManager);
        }
    }
}