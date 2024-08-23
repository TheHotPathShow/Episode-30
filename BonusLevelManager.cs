using TMPro;
using Unity.Entities;
using UnityEngine;

namespace Dev100.ECS
{
    public struct BonusLevelActiveTag : IComponentData {}
    public struct EndBonusLevelTag : IComponentData {}
    
    public class BonusLevelManager : MonoBehaviour
    {
        public static BonusLevelManager Instance;
        
        [SerializeField] private float _bonusLevelTime;
        [SerializeField] private TextMeshProUGUI _bonusTimeText;
        [SerializeField] private TextMeshProUGUI _gnomesDestroyedText;
        [SerializeField] private GameObject _bonusUI;
        [SerializeField] private GameObject _levelEndTrigger;
        
        private bool _isPlayingBonusLevel;
        private Entity _bonusLevelEntity;

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            if (World.DefaultGameObjectInjectionWorld == null)
            {
                World.DefaultGameObjectInjectionWorld = new World("Default World");
            }
            UpdateGnomesDestroyedUI(0);
        }

        // Invoked via unity event when player enters main play area
        public void StartBonusLevel()
        {
            _bonusUI.SetActive(true);
            _bonusLevelEntity = World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntity(typeof(BonusLevelActiveTag));
            _isPlayingBonusLevel = true;
        }

        private void Update()
        {
            if(!_isPlayingBonusLevel) return;
            _bonusLevelTime -= Time.deltaTime;
            _bonusTimeText.text = $"Bonus Time: {Mathf.Ceil(_bonusLevelTime)}";
            if (_bonusLevelTime > 0) return;
            EndBonusLevel();
        }

        public void UpdateGnomesDestroyedUI(int gnomesDestroyed)
        {
            _gnomesDestroyedText.text = $"Gnomes Destroyed: {gnomesDestroyed:N0}";
        }

        private void EndBonusLevel()
        {
            _isPlayingBonusLevel = false;
            _bonusTimeText.text = "Exit Stage at Entrance";
            _levelEndTrigger.SetActive(true);
            World.DefaultGameObjectInjectionWorld.EntityManager.DestroyEntity(_bonusLevelEntity);
            World.DefaultGameObjectInjectionWorld.EntityManager.CreateEntity(typeof(EndBonusLevelTag));
        }
    }

    public partial struct EndBonusLevelSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<EndBonusLevelTag>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var ecb = new EntityCommandBuffer(state.WorldUpdateAllocator);
            foreach (var (_, entity) in SystemAPI.Query<EnemyTag>().WithEntityAccess())
            {
                ecb.AddComponent<DestroyEntityTag>(entity);
            }

            var endBonusLevelEntity = SystemAPI.GetSingletonEntity<EndBonusLevelTag>();
            ecb.AddComponent<DestroyEntityTag>(endBonusLevelEntity);

            state.EntityManager.AddComponent<DestroyEntityTag>(SystemAPI.QueryBuilder().WithAll<EnemyTag>().Build());
            
            ecb.Playback(state.EntityManager);
        }
    }
}