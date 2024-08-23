using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Transforms;
using UnityEngine;
using BoxCollider = UnityEngine.BoxCollider;
using Material = Unity.Physics.Material;
using SphereCollider = UnityEngine.SphereCollider;

namespace Dev100.ECS
{
    public struct ProjectileTag : IComponentData {}
    public struct AddPointTag : IComponentData {}
    
    public struct ProjectileReference : IComponentData
    {
        public UnityObjectRef<Transform> Transform;
    }

    public struct ProjectileDamage : IComponentData
    {
        public int Value;
    }

    public struct HitBufferElement : IBufferElementData
    {
        public bool IsHandled;
        public Entity HitEntity;
    }

    public class ProjectileEntityAuthoring : MonoBehaviour
    {
        private class ProjectileEntityBaker : Baker<ProjectileEntityAuthoring>
        {
            public override void Bake(ProjectileEntityAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent<ProjectileTag>(entity);
                AddBuffer<HitBufferElement>(entity);
            }
        }
    }

    public partial class ProjectileCreationSystem : SystemBase
    {
        private CollisionFilter _projectileCollisionFilter;
        private Material _projectileCollisionMaterial;

        protected override void OnCreate()
        {
            _projectileCollisionFilter = CollisionFilter.Default;
            _projectileCollisionMaterial = new Material
            {
                CollisionResponse = CollisionResponsePolicy.RaiseTriggerEvents
            };
        }

        protected override void OnUpdate()
        {

        }

        public void CreateNewProjectile(GameObject projectile)
        {
            var projectileBasePrefab = SystemAPI.GetSingleton<EntityPrefabs>().ProjectileBase;
            var newProjectileEntity = EntityManager.Instantiate(projectileBasePrefab);
            EntityManager.SetName(newProjectileEntity, $"{projectile.name}-Entity");
            EntityManager.AddComponentData(newProjectileEntity, new ProjectileReference
            {
                Transform = new UnityObjectRef<Transform>
                {
                    Value = projectile.transform
                }
            });
            if (projectile.TryGetComponent(out SphereCollider sphereCollider))
            {
                var dotsSphereCollider = Unity.Physics.SphereCollider.Create(new SphereGeometry
                {
                    Center = sphereCollider.center,
                    Radius = sphereCollider.radius
                }, _projectileCollisionFilter, _projectileCollisionMaterial);
                EntityManager.AddComponentData(newProjectileEntity, new PhysicsCollider
                {
                    Value = dotsSphereCollider
                });
            }
            else if (projectile.TryGetComponent(out BoxCollider boxCollider))
            {
                var dotsBoxCollider = Unity.Physics.BoxCollider.Create(new BoxGeometry
                {
                    Center = boxCollider.center,
                    Size = boxCollider.size,
                    Orientation = quaternion.identity,
                    BevelRadius = 0.05f
                }, _projectileCollisionFilter, _projectileCollisionMaterial);
                EntityManager.AddComponentData(newProjectileEntity, new PhysicsCollider
                {
                    Value = dotsBoxCollider
                });
            }

            EntityManager.AddSharedComponent(newProjectileEntity, new PhysicsWorldIndex { Value = 0 });
            EntityManager.AddComponentData(newProjectileEntity, new ProjectileDamage { Value = projectile.GetComponent<Projectile>().Damage });
        }
    }

    [UpdateBefore(typeof(TransformSystemGroup))]
    public partial struct ProjectileFollowSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            foreach (var (transform, projectileReference, entity) in SystemAPI.Query<RefRW<LocalTransform>, ProjectileReference>().WithAll<ProjectileTag>().WithEntityAccess())
            {
                if (projectileReference.Transform.Value == null)
                {
                    ecb.AddComponent<DestroyEntityTag>(entity);
                    continue;
                }

                transform.ValueRW = LocalTransform.FromPositionRotation(projectileReference.Transform.Value.position, projectileReference.Transform.Value.rotation);
            }
            ecb.Playback(state.EntityManager);
        }
    }

    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    [UpdateAfter(typeof(PhysicsSystemGroup))]
    public partial struct ProjectileDamageSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SimulationSingleton>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var detectProjectileTriggerJob = new DetectProjectileTriggerJob
            {
                HitBufferLookup = SystemAPI.GetBufferLookup<HitBufferElement>(),
                HitPointsLookup = SystemAPI.GetComponentLookup<EntityHitPoints>(true)
            };

            var simSingleton = SystemAPI.GetSingleton<SimulationSingleton>();
            state.Dependency = detectProjectileTriggerJob.Schedule(simSingleton, state.Dependency);
        }
    }

    public struct DetectProjectileTriggerJob : ITriggerEventsJob
    {
        public BufferLookup<HitBufferElement> HitBufferLookup;
        [ReadOnly] public ComponentLookup<EntityHitPoints> HitPointsLookup;
        
        public void Execute(TriggerEvent triggerEvent)
        {
            Entity projectileEntity;
            Entity hitEntity;

            if (HitBufferLookup.HasBuffer(triggerEvent.EntityA) && HitPointsLookup.HasComponent(triggerEvent.EntityB))
            {
                projectileEntity = triggerEvent.EntityA;
                hitEntity = triggerEvent.EntityB;
            }
            else if (HitBufferLookup.HasBuffer(triggerEvent.EntityB) && HitPointsLookup.HasComponent(triggerEvent.EntityA))
            {
                hitEntity = triggerEvent.EntityA;
                projectileEntity = triggerEvent.EntityB;
            }
            else
            {
                return;
            }

            var hitBuffer = HitBufferLookup[projectileEntity];
            foreach (var hit in hitBuffer)
            {
                if (hit.HitEntity == hitEntity) return;
            }

            var newHitElement = new HitBufferElement
            {
                IsHandled = false,
                HitEntity = hitEntity
            };

            HitBufferLookup[projectileEntity].Add(newHitElement);
        }
    }

    public partial struct ApplyDamageSystem : ISystem
    {
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var hitPointsLookup = SystemAPI.GetComponentLookup<EntityHitPoints>();
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            
            foreach (var (hitBuffer, damage) in SystemAPI.Query<DynamicBuffer<HitBufferElement>, ProjectileDamage>())
            {
                foreach (var hit in hitBuffer)
                {
                    if (hit.IsHandled) continue;
                    if (hitPointsLookup.TryGetComponent(hit.HitEntity, out var curHitPoints))
                    {
                        curHitPoints.Value -= damage.Value;
                        hitPointsLookup[hit.HitEntity] = curHitPoints;
                        if (curHitPoints.Value <= 0)
                        {
                            ecb.AddComponent<AddPointTag>(hit.HitEntity);
                            ecb.AddComponent<DestroyEntityTag>(hit.HitEntity);
                        }
                    }
                }
            }
            
            ecb.Playback(state.EntityManager);
        }
    }
}