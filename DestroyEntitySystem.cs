using Unity.Entities;

namespace Dev100.ECS
{
    public struct DestroyEntityTag : IComponentData {}
    
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct DestroyEntitySystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<BonusLevelPoints>();
        }
        
        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            var bonusLevelPoints = SystemAPI.GetSingletonRW<BonusLevelPoints>();
            
            foreach (var (_, entity) in SystemAPI.Query<DestroyEntityTag>().WithEntityAccess())
            {
                if (SystemAPI.HasComponent<AddPointTag>(entity))
                {
                    bonusLevelPoints.ValueRW.Value++;
                    BonusLevelManager.Instance.UpdateGnomesDestroyedUI(bonusLevelPoints.ValueRO.Value);
                }
                ecb.DestroyEntity(entity);
            }

            ecb.Playback(state.EntityManager);
        }
    }
}