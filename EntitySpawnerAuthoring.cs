using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Dev100.ECS
{
    public struct EntityPrefabs : IComponentData
    {
        public Entity Gnome;
        public Entity Goblin;
        public Entity ProjectileBase;
    }

    public struct EntitySpawnProperties : IComponentData
    {
        public float3 MinSpawnPosition;
        public float3 MaxSpawnPosition;
        public float SpawnFrequency;
        public int MaxEntityCount;
    }

    public struct EntitySpawnTimer : IComponentData
    {
        public float Value;
    }

    public struct EntityRandom : IComponentData
    {
        public Random Value;
    }
    
    public class EntitySpawnerAuthoring : MonoBehaviour
    {
        public GameObject GnomePrefab;
        public GameObject GoblinPrefab;
        public GameObject ProjectileBasePrefab;
        public Vector3 MinSpawnPosition;
        public Vector3 MaxSpawnPosition;
        public float SpawnFrequency;
        public int MaxEntityCount;
        
        public class EntitySpawnerBaker : Baker<EntitySpawnerAuthoring>
        {
            public override void Bake(EntitySpawnerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new EntityPrefabs
                {
                    Gnome = GetEntity(authoring.GnomePrefab, TransformUsageFlags.Dynamic),
                    Goblin = GetEntity(authoring.GoblinPrefab, TransformUsageFlags.Dynamic),
                    ProjectileBase = GetEntity(authoring.ProjectileBasePrefab, TransformUsageFlags.Dynamic)
                });
                AddComponent(entity, new EntitySpawnProperties
                {
                    MinSpawnPosition = authoring.MinSpawnPosition,
                    MaxSpawnPosition = authoring.MaxSpawnPosition,
                    SpawnFrequency = authoring.SpawnFrequency,
                    MaxEntityCount = authoring.MaxEntityCount
                });
                AddComponent(entity, new EntityRandom
                {
                    Value = Random.CreateFromIndex(1000)
                });
                AddComponent<EntitySpawnTimer>(entity);
            }
        }
    }

    public partial struct SpawnEntitySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BonusLevelActiveTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            var enemyQuery = SystemAPI.QueryBuilder().WithAll<EnemyTag>().Build();
            
            foreach (var (spawnTimer, random, spawnProperties, prefabs) in SystemAPI.Query<RefRW<EntitySpawnTimer>, RefRW<EntityRandom>, EntitySpawnProperties, EntityPrefabs>())
            {
                spawnTimer.ValueRW.Value -= deltaTime;
                if (spawnTimer.ValueRO.Value > 0f) continue;
                spawnTimer.ValueRW.Value = spawnProperties.SpawnFrequency;

                if (enemyQuery.CalculateEntityCount() >= spawnProperties.MaxEntityCount) continue;
                
                Debug.Log("Spawn");
                
                var newGnome = ecb.Instantiate(prefabs.Gnome);
                var gnomePosition = random.ValueRW.Value.NextFloat3(spawnProperties.MinSpawnPosition, spawnProperties.MaxSpawnPosition);
                var gnomeTransform = LocalTransform.FromPosition(gnomePosition);
                ecb.SetComponent(newGnome, gnomeTransform);
            }
            
            ecb.Playback(state.EntityManager);
        }
    }
}