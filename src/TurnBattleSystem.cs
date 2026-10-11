using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GlobalEnums;
using HarmonyLib;
using TeamCherry.Localization;
using UnityEngine;

namespace SilkThreadCrest
{
    internal sealed class TurnBattleSystem : MonoBehaviour
    {
        private enum BattleState
        {
            Idle,
            Preparing,
            PlayerTurn,
            CrestMenu,
            SummonMenu,
            PlayerAction,
            EnemyAction,
            Result
        }

        private enum MoveKind
        {
            Attack,
            Skill,
            Tool,
            Heal,
            Defend,
            Summon
        }

        private sealed class BattleMove
        {
            internal string Name;
            internal int Damage;
            internal int SilkCost;
            internal MoveKind Kind;
            internal ToolItem Skill;
            internal bool IsSkill => Kind == MoveKind.Skill && Skill;
            internal bool IsTool => Kind == MoveKind.Tool && Skill;
        }

        private sealed class UiIcon
        {
            internal Texture2D Texture;
            internal Rect TextureCoordinates;
        }

        private const float EncounterDistance = 8.0f;
        private const float SearchInterval = 0.25f;
        private const float PrepareDuration = 0.45f;
        private const float PlayerActionDuration = 0.72f;
        private const float EnemyActionDuration = 1.15f;
        private const float EnemyHitReactionDuration = 0.55f;
        private const float VictoryDuration = 3.2f;
        private const float OtherResultDuration = 1.0f;
        private const float DropSearchRadius = 12.0f;
        private const int HealSilkCost = 9;
        private const int HealAmount = 3;
        private const int SummonSilkCost = 3;
        private const int SilkPerNormalHit = 1;
        private const int DefendSilkReward = SilkPerNormalHit * 2;
        private const int MaxCrestSwapsPerBattle = 3;
        private const float SummonActionDuration = 0.95f;
        private const SummonSortMode DefaultSummonSortMode = SummonSortMode.DamageDescending;

        private static TurnBattleSystem activeSystem;
        private static PropertyInfo wasPressedProperty;
        private static readonly MethodInfo NativeAttackMethod = AccessTools.Method(
            typeof(HeroController), "Attack", new[] { typeof(AttackDirection) });
        private static readonly MethodInfo NativeThrowToolMethod = AccessTools.Method(
            typeof(HeroController), "ThrowTool", new[] { typeof(bool) });
        private static readonly FieldInfo WillThrowToolField = AccessTools.Field(
            typeof(HeroController), "willThrowTool");
        private static readonly MethodInfo PickupInstantMethod = AccessTools.Method(
            typeof(CollectableItemPickup), "DoPickupInstant");
        private static readonly FieldInfo PickupCanPickupTimeField = AccessTools.Field(
            typeof(CollectableItemPickup), "canPickupTime");
        private static readonly FieldInfo PickupActivatedReadField = AccessTools.Field(
            typeof(CollectableItemPickup), "activatedRead");
        private static readonly FieldInfo PickupItemField = AccessTools.Field(
            typeof(CollectableItemPickup), "item");
        private static readonly FieldInfo AcceptingInputField = AccessTools.Field(typeof(HeroController), "acceptingInput");
        private static readonly MethodInfo SetActorStateMethod = AccessTools.Method(typeof(HeroController), "SetState", new[] { typeof(ActorStates) });
        private static readonly FieldInfo LastActiveControllerField = AccessTools.Field(typeof(InputHandler), "lastActiveController");

        private BattleState state;
        private HeroController hero;
        private HealthManager enemy;
        private Rigidbody2D heroBody;
        private Rigidbody2D enemyBody;
        private BattleMove currentMove;
        private Vector2 heroHomePosition;
        private Vector2 enemyHomePosition;
        private Vector2 heroHomeVelocity;
        private Vector2 enemyHomeVelocity;
        private Vector2 heroDuelPosition;
        private Vector2 enemyDuelPosition;
        private Vector3 enemyHomeScale;
        private float heroHomeGravity;
        private float enemyHomeGravity;
        private RigidbodyConstraints2D heroHomeConstraints;
        private RigidbodyConstraints2D enemyHomeConstraints;
        private float originalTimeScale = 1f;
        private float stateEndTime;
        private float nextSearchTime;
        private int selectedMove;
        private int selectedCrestIndex;
        private int enemyBasicDamage = 1;
        private int enemyMaxHp = 1;
        private int enemyHpBeforePlayerAction;
        private int heroHealthBeforeEnemyAction;
        private int silkBeforePlayerAction;
        private string battleMessage = "";
        private string resultMessage = "";
        private string battleHomeCrestId = "";
        private string battleCrestBeforeMenu = "";
        private bool reminderShown;
        private bool reminderUnavailable;
        private bool runHeroUpdate;
        private bool enemyTurnHitResolved;
        private bool victoryResult;
        private bool homeFacingRight;
        private bool enemyIdlePoseForced;
        private float enemyReactionUntil;
        private int crestSwapsUsedThisBattle;
        private bool crestSwappedThisTurn;
        private bool defendPending;
        private bool defendRewardGranted;
        private readonly HashSet<string> limitedToolDamageThisTurn = new HashSet<string>(StringComparer.Ordinal);
        private float nextBindingRefreshTime;
        private float nextSummonVisualRefreshTime;
        private string upBindingLabel = "↑";
        private string downBindingLabel = "↓";
        private string jumpBindingLabel = "PULO";
        private string dashBindingLabel = "DASH";
        private string attackBindingLabel = "ATAQUE";
        private Vector2 lootOrigin;
        private ControlReminder.SingleConfig proximityReminder;
        private BattleState lastVisualState = BattleState.Idle;
        private float visualStateChangedAt;
        private float visualExitStartedAt = -10f;
        private float battleIntroStartedAt = -10f;
        private GUIStyle uiTitleStyle;
        private GUIStyle uiBodyStyle;
        private GUIStyle uiSmallStyle;
        private GUIStyle uiMenuStyle;
        private GUIStyle uiDialogueStyle;
        private readonly List<BattleMove> moves = new List<BattleMove>();
        private readonly List<ToolCrest> crestMenuItems = new List<ToolCrest>();
        private readonly List<SummonAttack> summonMenuItems = new List<SummonAttack>();
        private readonly HashSet<int> lootAlreadyPresent = new HashSet<int>();
        private readonly HashSet<int> pickupAttempts = new HashSet<int>();
        private int selectedSummonAttack;
        private SummonAttack summonAttackInProgress;
        private GameObject summonVisual;
        private LearnedEnemySummon pendingDefeatedSummon;
        private LearnedEnemySummon battleEnemyKnownSummon;
        private bool summonUnlockedThisBattle;

        internal static bool IsBattleActive => activeSystem && activeSystem.state != BattleState.Idle;
        internal static bool IsNativeCrestMenuOpen => activeSystem && activeSystem.state == BattleState.CrestMenu;
        internal static bool CanChooseCrestInNativeMenu => activeSystem && activeSystem.CanChooseCrestInMenu();
        internal static bool ShouldRunHeroUpdate
        {
            get
            {
                if (!activeSystem)
                    return false;
                if (activeSystem.runHeroUpdate && activeSystem.hero)
                    activeSystem.hero.IgnoreInputWithoutReset();
                return activeSystem.runHeroUpdate;
            }
        }

        private void Awake()
        {
            activeSystem = this;
            state = BattleState.Idle;
            CrestLocalization.RegisterBattlePrompt();
            proximityReminder = new ControlReminder.SingleConfig
            {
                Text = new LocalisedString("Prompts", "SILK_THREAD_BATTLE_PROMPT"),
                Button = HeroActionButton.TAUNT,
                DisappearOnButtonPress = false,
                FadeInDelay = 0f,
                FadeInTime = 0.15f,
                FadeOutTime = 0.1f
            };
        }

        private void OnDestroy()
        {
            HideProximityPrompt();
            if (state != BattleState.Idle)
                EndBattle(restorePositions: true);
            DestroySummonVisual();
            if (activeSystem == this)
                activeSystem = null;
        }

        private void Update()
        {
            if (!hero)
                hero = HeroController.instance;
            if (Time.unscaledTime >= nextBindingRefreshTime)
            {
                nextBindingRefreshTime = Time.unscaledTime + 0.75f;
                RefreshBindingLabels();
            }
            if (Time.unscaledTime >= nextSummonVisualRefreshTime)
            {
                nextSummonVisualRefreshTime = Time.unscaledTime + 1.0f;
                EnemySummonRegistry.TryRehydrateVisualTemplates();
            }

            switch (state)
            {
                case BattleState.Idle:
                    UpdateEncounterPrompt();
                    break;
                case BattleState.Preparing:
                    if (Time.unscaledTime >= stateEndTime)
                    {
                        state = BattleState.PlayerTurn;
                        ResetLimitedToolDamageForHornetTurn();
                        battleMessage = "Escolhe uma ação.";
                        Time.timeScale = originalTimeScale;
                        runHeroUpdate = false;
                        PlaceDuelists();
                        ShowBattleReminders();
                    }
                    break;
                case BattleState.PlayerTurn:
                    UpdatePlayerTurn();
                    break;
                case BattleState.CrestMenu:
                    UpdateCrestMenu();
                    break;
                case BattleState.SummonMenu:
                    UpdateSummonMenu();
                    break;
                case BattleState.PlayerAction:
                    if (Time.unscaledTime >= stateEndTime)
                        FinishPlayerAction();
                    break;
                case BattleState.EnemyAction:
                    if (Time.unscaledTime >= stateEndTime)
                        FinishEnemyAction();
                    break;
                case BattleState.Result:
                    if (victoryResult)
                        CollectNewDrops();
                    if (Time.unscaledTime >= stateEndTime)
                        EndBattle(restorePositions: true);
                    break;
            }
        }

        private void LateUpdate()
        {
            if (state == BattleState.Idle)
                return;

            LockHeroAtDuelPosition();
            if (!(state == BattleState.Result && victoryResult))
                LockEnemyAtDuelPosition();

            bool enemyIsActing = state == BattleState.EnemyAction && !enemyTurnHitResolved;
            bool enemyIsReactingToHit = state == BattleState.PlayerAction && Time.unscaledTime <= enemyReactionUntil;
            bool enemyIsFinishingAttack = state == BattleState.EnemyAction && enemyTurnHitResolved &&
                Time.unscaledTime <= enemyReactionUntil;
            if (!enemyIsActing && !enemyIsReactingToHit && !enemyIsFinishingAttack && !(state == BattleState.Result && victoryResult))
            {
                if (!enemyIdlePoseForced)
                    ForceEnemyIdlePose();
            }
            else if (enemyIsActing || enemyIsReactingToHit || enemyIsFinishingAttack)
            {
                enemyIdlePoseForced = false;
            }
        }

        private void UpdateEncounterPrompt()
        {
            if (!hero || !PlayerData.HasInstance || GameManager.instance == null ||
                GameManager.instance.GameState != GameState.PLAYING || Time.timeScale <= 0f)
            {
                if (reminderShown)
                    HideProximityPrompt();
                return;
            }

            if (!hero.cState.onGround)
            {
                enemy = null;
                nextSearchTime = Time.unscaledTime;
                HideProximityPrompt();
                return;
            }

            if (Time.unscaledTime >= nextSearchTime)
            {
                nextSearchTime = Time.unscaledTime + SearchInterval;
                HealthManager closest = FindClosestEnemy();
                enemy = closest;
                if (enemy)
                    ShowProximityPrompt();
                else
                    HideProximityPrompt();
            }

            if (hero.cState.onGround && InputHandlerReady())
            {
                InputHandler input = ManagerSingleton<InputHandler>.Instance;
                bool pressedNow = WasPressed(HeroActionButton.TAUNT);
                bool bufferedPress = input.GetWasButtonPressedQueued(HeroActionButton.TAUNT, consume: true);
                if (pressedNow || bufferedPress)
                {
                    // Do not depend on the last 0.25-second proximity scan: the prompt can be
                    // hidden or one frame out of date while the player presses the configured key.
                    if (!enemy || enemy.isDead || enemy.hp <= 0)
                        enemy = FindClosestEnemy();
                    if (enemy)
                    {
                        Debug.Log("Silk Thread battle received TAUNT press-edge; preparing in-world encounter with " + enemy.name + ".");
                        StartBattle(enemy);
                    }
                    else
                    {
                        Debug.Log("Silk Thread received TAUNT, but no eligible enemy was found within " + EncounterDistance + " world units.");
                    }
                }
            }
        }

        private HealthManager FindClosestEnemy()
        {
            HealthManager closest = null;
            float closestDistance = EncounterDistance;
            HealthManager[] candidates = FindObjectsByType<HealthManager>(FindObjectsSortMode.None);
            foreach (HealthManager candidate in candidates)
            {
                if (!candidate || candidate.isDead || candidate.hp <= 0 || !candidate.gameObject.activeInHierarchy)
                    continue;
                if (candidate.transform == hero.transform || candidate.transform.IsChildOf(hero.transform) ||
                    candidate.GetComponentInParent<HeroController>() == hero)
                    continue;

                bool enemyLayer = (GlobalEnums.PhysLayers)candidate.gameObject.layer == GlobalEnums.PhysLayers.ENEMIES;
                if (!enemyLayer)
                {
                    Transform root = candidate.transform.root;
                    enemyLayer = root && (GlobalEnums.PhysLayers)root.gameObject.layer == GlobalEnums.PhysLayers.ENEMIES;
                }
                if (!enemyLayer)
                    continue;

                float distance = Vector2.Distance(hero.transform.position, candidate.transform.position);
                if (distance <= closestDistance)
                {
                    closestDistance = distance;
                    closest = candidate;
                }
            }
            return closest;
        }

        private bool InputHandlerReady()
        {
            return ManagerSingleton<InputHandler>.Instance;
        }

        private void ShowProximityPrompt()
        {
            if (reminderUnavailable || reminderShown || !enemy)
                return;
            try
            {
                ControlReminder.AddReminder(proximityReminder, doAppear: true);
                reminderShown = true;
            }
            catch (Exception ex)
            {
                reminderUnavailable = true;
                Debug.LogWarning("Silk Thread battle prompt unavailable; using the fallback prompt: " + ex.GetBaseException().Message);
            }
        }

        private void HideProximityPrompt()
        {
            if (!reminderShown || proximityReminder == null)
                return;
            try
            {
                proximityReminder.Disappear(isInstant: true, keepMinTime: false);
            }
            catch
            {
            }
            reminderShown = false;
        }

        private void ShowBattleReminders()
        {
            // Battle controls are rendered inside the mod HUD; do not push floating game reminders.
        }

        private void HideBattleReminders()
        {
        }

        private void StartBattle(HealthManager selectedEnemy)
        {
            if (!hero || !hero.cState.onGround || !selectedEnemy || selectedEnemy.isDead || state != BattleState.Idle)
                return;

            enemy = selectedEnemy;
            enemyMaxHp = Mathf.Max(1, selectedEnemy.hp);
            DestroySummonVisual();
            summonAttackInProgress = null;
            if (pendingDefeatedSummon != null)
                EnemySummonRegistry.Discard(pendingDefeatedSummon);
            battleEnemyKnownSummon = EnemySummonRegistry.Find(selectedEnemy);
            pendingDefeatedSummon = battleEnemyKnownSummon != null
                ? null
                : EnemySummonRegistry.Capture(selectedEnemy);
            summonUnlockedThisBattle = false;
            heroBody = hero.Body;
            enemyBody = FindBody(enemy);
            heroHomePosition = hero.transform.position;
            enemyHomePosition = enemy.transform.position;
            heroHomeVelocity = heroBody ? heroBody.linearVelocity : Vector2.zero;
            enemyHomeVelocity = enemyBody ? enemyBody.linearVelocity : Vector2.zero;
            heroHomeGravity = heroBody ? heroBody.gravityScale : 1f;
            enemyHomeGravity = enemyBody ? enemyBody.gravityScale : 1f;
            heroHomeConstraints = heroBody ? heroBody.constraints : RigidbodyConstraints2D.None;
            enemyHomeConstraints = enemyBody ? enemyBody.constraints : RigidbodyConstraints2D.None;
            enemyHomeScale = enemy.transform.localScale;
            homeFacingRight = hero.cState.facingRight;
            enemyReactionUntil = 0f;
            originalTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            battleHomeCrestId = PlayerData.HasInstance ? PlayerData.instance.CurrentCrestID : "";
            crestSwapsUsedThisBattle = 0;
            crestSwappedThisTurn = false;
            defendPending = false;
            defendRewardGranted = false;
            limitedToolDamageThisTurn.Clear();
            BuildMoves();
            enemyBasicDamage = FindEnemyBasicDamage(enemy);
            CaptureExistingLoot();
            CalculateDuelPositions();
            PlaceDuelists();
            SetHornetIdlePose();
            hero.IgnoreInput();
            if (heroBody)
                LockBody(heroBody, heroHomeConstraints);
            if (enemyBody)
                LockBody(enemyBody, enemyHomeConstraints);

            HideProximityPrompt();
            HideBattleReminders();
            selectedMove = 0;
            CrestLocalization.SetBattleCrestSwapLabel(crestSwapsUsedThisBattle, crestSwappedThisTurn, CanSwapCrest());
            battleMessage = "A preparar o confronto na área atual.";
            resultMessage = "";
            victoryResult = false;
            state = BattleState.Preparing;
            enemyIdlePoseForced = false;
            ForceEnemyIdlePose();
            stateEndTime = Time.unscaledTime + PrepareDuration;
            battleIntroStartedAt = Time.unscaledTime;
            visualExitStartedAt = -10f;
            Time.timeScale = originalTimeScale;
            runHeroUpdate = false;
            Debug.Log("Silk Thread in-world battle started against " + enemy.name + "; moves=" + moves.Count + "; basic enemy damage=" + enemyBasicDamage + ".");
        }

        private void CalculateDuelPositions()
        {
            float direction = enemyHomePosition.x >= heroHomePosition.x ? 1f : -1f;
            if (Mathf.Abs(enemyHomePosition.x - heroHomePosition.x) < 0.01f)
                direction = homeFacingRight ? 1f : -1f;

            Collider2D heroCollider = hero ? hero.GetComponent<Collider2D>() : null;
            Collider2D enemyCollider = enemy ? enemy.GetComponentInChildren<Collider2D>() : null;
            float heroExtent = heroCollider ? heroCollider.bounds.extents.x : 0.35f;
            float enemyExtent = enemyCollider ? enemyCollider.bounds.extents.x : 0.4f;
            float spacing = Mathf.Clamp(heroExtent + enemyExtent + 0.85f, 1.6f, 3.0f);
            heroDuelPosition = heroHomePosition;
            enemyDuelPosition = heroDuelPosition + new Vector2(direction * spacing, 0f);
        }

        private void BuildMoves()
        {
            moves.Clear();
            int baseDamage = Mathf.Max(1, PlayerData.instance.nailDamage);
            int summonCost = GetCurrentSummonCost();
            int healCost = CrestSynergy.GetActionCost(CrestSynergyAction.Heal, HealSilkCost);
            int defendReward = GetCurrentDefendReward();
            moves.Add(new BattleMove
            {
                Name = "Golpe do brasão",
                Damage = baseDamage,
                SilkCost = 0,
                Kind = MoveKind.Attack,
                Skill = null
            });

            if (EnemySummonRegistry.HasAny)
            {
                moves.Add(new BattleMove
                {
                    Name = "Invocar",
                    Damage = 0,
                    SilkCost = summonCost,
                    Kind = MoveKind.Summon,
                    Skill = null
                });
            }

            if (PlayerData.instance.health < PlayerData.instance.CurrentMaxHealth)
            {
                moves.Add(new BattleMove
                {
                    Name = "Curar (3 pontos)",
                    Damage = 0,
                    SilkCost = healCost,
                    Kind = MoveKind.Heal,
                    Skill = null
                });
            }

            moves.Add(new BattleMove
            {
                Name = "Defender-se (50% dano, +" + defendReward + " seda)",
                Damage = 0,
                SilkCost = 0,
                Kind = MoveKind.Defend,
                Skill = null
            });

            IEnumerable<ToolItem> battleTools = ToolItemManager.GetAllTools()
                .Where(tool => tool && tool.IsEquipped &&
                    (tool.Type == ToolItemType.Skill || tool.Type == ToolItemType.Red) &&
                    (tool.Type != ToolItemType.Red || (!tool.IsEmpty && HasAttackToolBinding(tool))))
                .OrderBy(tool => tool.Type == ToolItemType.Red ? 0 : 1)
                .ThenBy(tool => GetToolDisplayName(tool), StringComparer.OrdinalIgnoreCase);
            foreach (ToolItem skill in battleTools)
            {
                string name = GetToolDisplayName(skill);
                bool isCraftedTool = skill.Type == ToolItemType.Red;
                string stockLabel = isCraftedTool ? " [" + GetToolStockLabel(skill) + "]" : "";
                moves.Add(new BattleMove
                {
                    Name = (isCraftedTool ? "Ferramenta — " : "Skill — ") + name + stockLabel,
                    Damage = Mathf.Max(1, baseDamage * 2),
                    SilkCost = isCraftedTool ? 0 : CrestSynergy.GetActionCost(CrestSynergyAction.Skill,
                        Mathf.Max(0, PlayerData.instance.SilkSkillCost)),
                    Kind = isCraftedTool ? MoveKind.Tool : MoveKind.Skill,
                    Skill = skill
                });
            }
        }

        private static string GetToolDisplayName(ToolItem tool)
        {
            if (!tool)
                return "Ferramenta";
            string name = tool.DisplayName.ToString(false);
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith("!!", StringComparison.Ordinal))
                name = tool.name;
            return string.IsNullOrWhiteSpace(name) ? "Ferramenta" : name;
        }

        private static bool HasAttackToolBinding(ToolItem tool)
        {
            if (!tool)
                return false;
            try
            {
                return ToolItemManager.GetAttackToolBinding(tool).HasValue;
            }
            catch
            {
                return false;
            }
        }

        private static int GetToolStock(ToolItem tool)
        {
            if (!tool || tool.Type != ToolItemType.Red || !tool.IsCounted)
                return int.MaxValue;
            try
            {
                return Mathf.Max(0, tool.SavedData.AmountLeft);
            }
            catch
            {
                return 0;
            }
        }

        private static string GetToolStockLabel(ToolItem tool)
        {
            int stock = GetToolStock(tool);
            return stock == int.MaxValue ? "∞" : stock.ToString();
        }

        private int GetCurrentSummonCost()
        {
            return CrestSynergy.GetActionCost(CrestSynergyAction.Summon, SummonSilkCost);
        }

        private int GetCurrentDefendReward()
        {
            return DefendSilkReward + CrestSynergy.GetDefendRewardBonus();
        }

        private int FindEnemyBasicDamage(HealthManager target)
        {
            if (!target)
                return 1;
            DamageHero[] damageSources = target.GetComponentsInChildren<DamageHero>(includeInactive: true);
            DamageHero basic = damageSources.FirstOrDefault(source => source && source.hazardType == GlobalEnums.HazardType.ENEMY);
            if (!basic)
                basic = damageSources.FirstOrDefault(source => source);
            return basic ? Mathf.Max(1, basic.damageDealt) : 1;
        }

        private void CaptureExistingLoot()
        {
            lootAlreadyPresent.Clear();
            pickupAttempts.Clear();
            foreach (CurrencyObjectBase drop in FindObjectsByType<CurrencyObjectBase>(FindObjectsSortMode.None))
                if (drop && drop.gameObject.activeInHierarchy)
                    lootAlreadyPresent.Add(drop.GetInstanceID());
            foreach (CollectableItemPickup pickup in FindObjectsByType<CollectableItemPickup>(FindObjectsSortMode.None))
                if (pickup && pickup.gameObject.activeInHierarchy)
                    lootAlreadyPresent.Add(pickup.GetInstanceID());
        }

        private void UpdatePlayerTurn()
        {
            if (!enemy || enemy.isDead || enemy.hp <= 0)
            {
                BeginResult("Vitória — inimigo derrotado.", victory: true);
                return;
            }
            if (PlayerData.instance.health <= 0 || hero.cState.dead)
            {
                BeginResult("Derrota — Hornet foi derrotada.");
                return;
            }

            if (WasPressed(HeroActionButton.ATTACK))
            {
                OpenCrestSelectionMenu();
                if (state == BattleState.CrestMenu)
                    return;
            }

            if (WasPressed(HeroActionButton.DASH))
            {
                BeginResult("Batalha terminada — Hornet recuou.");
                return;
            }
            int previousSelection = selectedMove;
            if (WasPressed(HeroActionButton.UP) || WasPressed(HeroActionButton.LEFT))
                selectedMove = (selectedMove + moves.Count - 1) % moves.Count;
            else if (WasPressed(HeroActionButton.DOWN) || WasPressed(HeroActionButton.RIGHT))
                selectedMove = (selectedMove + 1) % moves.Count;
            if (selectedMove != previousSelection)
                UpdateSelectedActionPrompt();
            if (WasPressed(HeroActionButton.JUMP))
                BeginPlayerMove(selectedMove);
        }

        private void UpdateSelectedActionPrompt()
        {
            if (moves.Count == 0)
                return;
                BattleMove move = moves[Mathf.Clamp(selectedMove, 0, moves.Count - 1)];
            string actionLabel = move.Kind == MoveKind.Skill
                ? "Skill: " + move.Name.Replace("Skill — ", "")
                : move.Kind == MoveKind.Tool ? "Ferramenta: " + move.Name.Replace("Ferramenta — ", "")
                : move.Kind == MoveKind.Heal ? "Curar"
                : move.Kind == MoveKind.Defend ? "Defender"
                : move.Kind == MoveKind.Summon ? "Invocar"
                : "Golpe do brasão";
            if (actionLabel.Length > 20)
                actionLabel = actionLabel.Substring(0, 19) + "…";
            CrestLocalization.SetBattleActionLabel(actionLabel);
            CrestLocalization.SetBattleCrestSwapLabel(crestSwapsUsedThisBattle, crestSwappedThisTurn, CanSwapCrest());
        }

        private List<ToolCrest> GetBattleSwapCrests()
        {
            if (!PlayerData.HasInstance)
                return new List<ToolCrest>();
            string currentId = PlayerData.instance.CurrentCrestID;
            return ToolItemManager.GetAllCrests()
                .Where(crest => crest && crest.IsUnlocked && (crest.IsVisible || crest.name == currentId))
                .OrderBy(crest => crest.DisplayName.ToString(false), StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private bool CanSwapCrest()
        {
            return !crestSwappedThisTurn && crestSwapsUsedThisBattle < MaxCrestSwapsPerBattle &&
                GetBattleSwapCrests().Count > 1;
        }

        private bool CanChooseCrestInMenu()
        {
            return state == BattleState.CrestMenu && CanSwapCrest();
        }

        private void OpenCrestSelectionMenu()
        {
            HideBattleReminders();
            battleCrestBeforeMenu = PlayerData.instance.CurrentCrestID;
            crestMenuItems.Clear();
            crestMenuItems.AddRange(GetBattleSwapCrests());
            selectedCrestIndex = crestMenuItems.FindIndex(crest => crest && crest.name == battleCrestBeforeMenu);
            if (selectedCrestIndex < 0)
                selectedCrestIndex = 0;
            visualStateChangedAt = Time.unscaledTime;
            state = BattleState.CrestMenu;
            runHeroUpdate = false;
            Time.timeScale = 0f;
            Debug.Log("Silk Thread opened its crest selection overlay; available crests=" + crestMenuItems.Count + ".");
        }

        private void UpdateCrestMenu()
        {
            if (WasPressed(HeroActionButton.ATTACK) || WasPressed(HeroActionButton.DASH))
            {
                CloseCrestMenu();
                return;
            }

            if (crestMenuItems.Count == 0)
                return;
            if (WasPressed(HeroActionButton.UP) || WasPressed(HeroActionButton.LEFT))
                selectedCrestIndex = (selectedCrestIndex + crestMenuItems.Count - 1) % crestMenuItems.Count;
            else if (WasPressed(HeroActionButton.DOWN) || WasPressed(HeroActionButton.RIGHT))
                selectedCrestIndex = (selectedCrestIndex + 1) % crestMenuItems.Count;
            if (WasPressed(HeroActionButton.JUMP))
                ApplySelectedBattleCrest();
        }

        private void ApplySelectedBattleCrest()
        {
            if (selectedCrestIndex < 0 || selectedCrestIndex >= crestMenuItems.Count)
                return;
            ToolCrest selected = crestMenuItems[selectedCrestIndex];
            if (!selected)
                return;
            string currentId = PlayerData.instance.CurrentCrestID;
            if (string.Equals(selected.name, currentId, StringComparison.Ordinal))
            {
                CloseCrestMenu();
                return;
            }
            if (!CanSwapCrest())
            {
                battleMessage = crestSwappedThisTurn
                    ? "Você já trocou de brasão neste turno."
                    : "Limite de três trocas de brasão atingido nesta batalha.";
                return;
            }

            try
            {
                ToolItemManager.SetEquippedCrest(selected.name);
                ToolItemManager.SendEquippedChangedEvent(force: true);
                battleCrestBeforeMenu = PlayerData.instance.CurrentCrestID;
                crestSwapsUsedThisBattle = Mathf.Min(MaxCrestSwapsPerBattle, crestSwapsUsedThisBattle + 1);
                crestSwappedThisTurn = true;
                BuildMoves();
                selectedMove = Mathf.Clamp(selectedMove, 0, moves.Count - 1);
                battleMessage = "Brasão equipado: " + selected.DisplayName.ToString(false) + ".";
                CrestLocalization.SetBattleCrestSwapLabel(crestSwapsUsedThisBattle, crestSwappedThisTurn, CanSwapCrest());
                Debug.Log("Silk Thread battle crest changed to '" + selected.name + "'; swaps=" +
                    crestSwapsUsedThisBattle + "/" + MaxCrestSwapsPerBattle + ".");
                CloseCrestMenu();
            }
            catch (Exception ex)
            {
                battleMessage = "Não foi possível equipar esse brasão.";
                Debug.LogWarning("Silk Thread could not equip battle crest '" + selected.name + "': " + ex.GetBaseException().Message);
            }
        }

        private void CloseCrestMenu()
        {
            crestMenuItems.Clear();
            state = BattleState.PlayerTurn;
            Time.timeScale = originalTimeScale > 0f ? originalTimeScale : 1f;
            PlaceDuelists();
            SetHornetIdlePose();
            UpdateSelectedActionPrompt();
            ShowBattleReminders();
        }

        private void OpenSummonSelectionMenu()
        {
            if (!EnemySummonRegistry.HasAny)
            {
                battleMessage = "Nenhuma invocação foi aprendida ainda.";
                return;
            }
            int summonCost = GetCurrentSummonCost();
            if (PlayerData.instance.silk < summonCost)
            {
                battleMessage = "Seda insuficiente para invocar. Custo: " + summonCost + ".";
                return;
            }

            HideBattleReminders();
            summonMenuItems.Clear();
            summonMenuItems.AddRange(EnemySummonRegistry.GetAllAttacks(DefaultSummonSortMode));
            selectedSummonAttack = Mathf.Clamp(selectedSummonAttack, 0, Mathf.Max(0, summonMenuItems.Count - 1));
            visualStateChangedAt = Time.unscaledTime;
            state = BattleState.SummonMenu;
            runHeroUpdate = false;
            Time.timeScale = 0f;
            Debug.Log("Silk Thread opened summon selection overlay; available attacks=" + summonMenuItems.Count + ".");
        }

        private void UpdateSummonMenu()
        {
            if (WasPressed(HeroActionButton.ATTACK) || WasPressed(HeroActionButton.DASH))
            {
                CloseSummonSelectionMenu();
                return;
            }
            if (summonMenuItems.Count == 0)
                return;
            if (WasPressed(HeroActionButton.UP) || WasPressed(HeroActionButton.LEFT))
                selectedSummonAttack = (selectedSummonAttack + summonMenuItems.Count - 1) % summonMenuItems.Count;
            else if (WasPressed(HeroActionButton.DOWN) || WasPressed(HeroActionButton.RIGHT))
                selectedSummonAttack = (selectedSummonAttack + 1) % summonMenuItems.Count;
            if (WasPressed(HeroActionButton.JUMP))
                BeginSummonAttack(selectedSummonAttack);
        }

        private void BeginSummonAttack(int index)
        {
            if (index < 0 || index >= summonMenuItems.Count || !enemy || enemy.isDead || enemy.hp <= 0)
                return;
            int summonCost = GetCurrentSummonCost();
            if (PlayerData.instance.silk < summonCost)
            {
                battleMessage = "Seda insuficiente para invocar. Custo: " + summonCost + ".";
                CloseSummonSelectionMenu();
                return;
            }

            SummonAttack attack = summonMenuItems[index];
            if (attack == null || attack.Owner == null)
                return;
            summonAttackInProgress = attack;
            currentMove = new BattleMove
            {
                Name = "Invocar — " + attack.DisplayLabel,
                Damage = Mathf.Max(1, attack.Damage),
                SilkCost = summonCost,
                Kind = MoveKind.Summon,
                Skill = null
            };
            hero.TakeSilk(summonCost);
            HideBattleReminders();
            enemyHpBeforePlayerAction = enemy.hp;
            battleMessage = "A invocação de " + attack.Owner.DisplayName + " usa " + attack.Name + ".";
            PlaceDuelists();
            state = BattleState.PlayerAction;
            stateEndTime = Time.unscaledTime + SummonActionDuration;
            runHeroUpdate = false;
            Time.timeScale = originalTimeScale;
            summonVisual = EnemySummonRegistry.CreateVisual(attack.Owner);
            if (summonVisual)
            {
                Vector3 summonPosition = heroDuelPosition;
                float direction = enemyDuelPosition.x >= heroDuelPosition.x ? 1f : -1f;
                summonPosition.x += direction * 0.35f;
                summonVisual.transform.position = summonPosition;
                Vector3 scale = summonVisual.transform.localScale;
                float magnitude = Mathf.Abs(scale.x);
                if (magnitude < 0.001f)
                    magnitude = 1f;
                scale.x = direction >= 0f ? -magnitude : magnitude;
                summonVisual.transform.localScale = scale;
                EnemySummonRegistry.PlayAttack(summonVisual, attack);
            }
            else
            {
                Debug.LogWarning("Silk Thread summon visual was unavailable; the learned attack will still resolve as a battle action.");
            }
            CloseSummonMenuForAction();
        }

        private void CloseSummonMenuForAction()
        {
            summonMenuItems.Clear();
        }

        private void CloseSummonSelectionMenu()
        {
            summonMenuItems.Clear();
            state = BattleState.PlayerTurn;
            Time.timeScale = originalTimeScale > 0f ? originalTimeScale : 1f;
            PlaceDuelists();
            SetHornetIdlePose();
            UpdateSelectedActionPrompt();
            ShowBattleReminders();
        }

        private bool WasPressed(HeroActionButton button)
        {
            try
            {
                InputHandler handler = ManagerSingleton<InputHandler>.Instance;
                if (!handler)
                    return false;
                object action = handler.ActionButtonToPlayerAction(button);
                if (action == null)
                    return false;
                if (wasPressedProperty == null || wasPressedProperty.DeclaringType != action.GetType())
                    wasPressedProperty = AccessTools.Property(action.GetType(), "WasPressed");
                return wasPressedProperty != null && (bool)wasPressedProperty.GetValue(action, null);
            }
            catch
            {
                return false;
            }
        }

        private void RefreshBindingLabels()
        {
            upBindingLabel = GetBindingLabel(HeroActionButton.UP, "↑");
            downBindingLabel = GetBindingLabel(HeroActionButton.DOWN, "↓");
            jumpBindingLabel = GetBindingLabel(HeroActionButton.JUMP, "PULO");
            dashBindingLabel = GetBindingLabel(HeroActionButton.DASH, "DASH");
            attackBindingLabel = GetBindingLabel(HeroActionButton.ATTACK, "ATAQUE");
        }

        private static string GetBindingLabel(HeroActionButton button, string fallback)
        {
            try
            {
                InputHandler handler = ManagerSingleton<InputHandler>.Instance;
                if (!handler)
                    return fallback;
                object action = handler.ActionButtonToPlayerAction(button);
                if (action == null)
                    return fallback;

                object activeSource = LastActiveControllerField == null
                    ? null
                    : LastActiveControllerField.GetValue(handler);
                bool gamepadActive = activeSource != null &&
                    activeSource.ToString().IndexOf("Device", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!gamepadActive)
                {
                    object keyBinding = handler.GetKeyBindingForAction((InControl.PlayerAction)action);
                    string keyText = keyBinding == null ? "" : keyBinding.ToString();
                    if (!string.IsNullOrWhiteSpace(keyText) && !keyText.Equals("None", StringComparison.OrdinalIgnoreCase))
                        return keyText.ToUpperInvariant();
                    return fallback;
                }

                object buttonBinding = handler.GetButtonBindingForAction((InControl.PlayerAction)action);
                string control = buttonBinding == null ? "" : buttonBinding.ToString();
                if (string.IsNullOrWhiteSpace(control) || control.Equals("None", StringComparison.OrdinalIgnoreCase))
                    return fallback;
                return FormatGamepadBinding(control, handler.ActiveGamepadAlias.ToString());
            }
            catch
            {
                return fallback;
            }
        }

        private static string FormatGamepadBinding(string control, string gamepadAlias)
        {
            string alias = gamepadAlias ?? "";
            bool xbox = alias.IndexOf("XBOX", StringComparison.OrdinalIgnoreCase) >= 0;
            bool playStation = alias.IndexOf("PS", StringComparison.OrdinalIgnoreCase) >= 0;
            bool nintendo = alias.IndexOf("SWITCH", StringComparison.OrdinalIgnoreCase) >= 0 ||
                alias.IndexOf("WII", StringComparison.OrdinalIgnoreCase) >= 0;

            switch (control)
            {
                case "Action1":
                case "ButtonSouth":
                    return xbox ? "A" : playStation ? "×" : nintendo ? "B" : "SUL";
                case "Action2":
                case "ButtonEast":
                    return xbox ? "B" : playStation ? "○" : nintendo ? "A" : "ESTE";
                case "Action3":
                case "ButtonWest":
                    return xbox ? "X" : playStation ? "□" : nintendo ? "Y" : "OESTE";
                case "Action4":
                case "ButtonNorth":
                    return xbox ? "Y" : playStation ? "△" : nintendo ? "X" : "NORTE";
                case "LeftBumper": return "LB";
                case "RightBumper": return "RB";
                case "LeftTrigger": return "LT";
                case "RightTrigger": return "RT";
                case "DPadUp": return "↑";
                case "DPadDown": return "↓";
                case "DPadLeft": return "←";
                case "DPadRight": return "→";
                case "Start": return "MENU";
                case "Select": return "VIEW";
                default: return control.ToUpperInvariant();
            }
        }

        private void BeginPlayerMove(int index)
        {
            if (!enemy || index < 0 || index >= moves.Count)
                return;
            BattleMove move = moves[index];
            if (move.SilkCost > PlayerData.instance.silk)
            {
                battleMessage = "Seda insuficiente para " + move.Name + ".";
                return;
            }

            if (move.IsTool && (move.Skill == null || move.Skill.IsEmpty))
            {
                battleMessage = "Essa ferramenta não tem usos fabricados restantes.";
                BuildMoves();
                selectedMove = Mathf.Clamp(selectedMove, 0, Mathf.Max(0, moves.Count - 1));
                return;
            }

            if (move.Kind == MoveKind.Heal && PlayerData.instance.health >= PlayerData.instance.CurrentMaxHealth)
            {
                battleMessage = "Hornet já está com a vida cheia.";
                return;
            }

            if (move.Kind == MoveKind.Summon)
            {
                OpenSummonSelectionMenu();
                return;
            }

            if (move.Kind == MoveKind.Defend)
            {
                currentMove = move;
                defendPending = true;
                defendRewardGranted = false;
                battleMessage = "Hornet reduz o próximo golpe em 50% e recebe +" + GetCurrentDefendReward() +
                    " seda se sofrer dano.";
                StartEnemyAction();
                return;
            }

            currentMove = move;
            HideBattleReminders();
            enemyHpBeforePlayerAction = enemy.hp;
            silkBeforePlayerAction = PlayerData.instance.silk;
            battleMessage = "Hornet usa " + move.Name + ".";
            PlaceDuelists();
            FaceEnemyAndTarget();
            state = BattleState.PlayerAction;
            stateEndTime = Time.unscaledTime + (move.Kind == MoveKind.Heal ? 0.35f : PlayerActionDuration);
            runHeroUpdate = move.Kind != MoveKind.Heal;
            Time.timeScale = originalTimeScale;
            if (move.Kind == MoveKind.Heal)
            {
                hero.TakeSilk(move.SilkCost);
                hero.AddHealth(HealAmount);
                battleMessage = "Hornet recupera até 3 pontos de vida.";
                SetHornetIdlePose();
                return;
            }

            bool usedNativeTool = (move.IsSkill || move.IsTool) && TryStartNativeSkill(move.Skill);
            if (!usedNativeTool)
            {
                if (move.SilkCost > 0)
                    hero.TakeSilk(move.SilkCost);
                try
                {
                    if (NativeAttackMethod != null)
                        NativeAttackMethod.Invoke(hero, new object[] { AttackDirection.normal });
                    else
                        Debug.LogWarning("Silk Thread could not resolve HeroController.Attack; using the damage fallback.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Silk Thread native Hornet attack animation failed: " + ex.GetBaseException().Message);
                }
            }
        }

        private bool TryStartNativeSkill(ToolItem skill)
        {
            if (!hero || !skill || NativeThrowToolMethod == null || WillThrowToolField == null)
                return false;
            try
            {
                MethodInfo bindingMethod = AccessTools.Method(typeof(ToolItemManager), "GetAttackToolBinding", new[] { typeof(ToolItem) });
                if (bindingMethod == null || bindingMethod.Invoke(null, new object[] { skill }) == null)
                    return false;

                hero.AcceptInput();
                WillThrowToolField.SetValue(hero, skill);
                NativeThrowToolMethod.Invoke(hero, new object[] { false });
                hero.IgnoreInputWithoutReset();
                return true;
            }
            catch (Exception ex)
            {
                hero.IgnoreInputWithoutReset();
                Debug.LogWarning("Silk Thread native skill action failed; using the crest slash animation: " + ex.GetBaseException().Message);
                return false;
            }
        }

        private void FinishPlayerAction()
        {
            if (summonAttackInProgress != null)
            {
                FinishSummonAction();
                return;
            }
            Time.timeScale = originalTimeScale;
            runHeroUpdate = false;
            int damageDone = enemy ? Mathf.Max(0, enemyHpBeforePlayerAction - enemy.hp) : 0;
            if (currentMove != null && currentMove.IsSkill && PlayerData.HasInstance)
            {
                int silkSpent = Mathf.Max(0, silkBeforePlayerAction - PlayerData.instance.silk);
                int remainingCost = Mathf.Max(0, currentMove.SilkCost - silkSpent);
                if (remainingCost > 0)
                    hero.TakeSilk(remainingCost);
            }
            bool isOffensiveMove = currentMove != null &&
                (currentMove.Kind == MoveKind.Attack || currentMove.Kind == MoveKind.Skill ||
                 currentMove.Kind == MoveKind.Tool);
            bool fallbackSuppressed = false;
            if (enemy && !enemy.isDead && enemy.hp > 0 && damageDone <= 0 && isOffensiveMove)
            {
                if (IsTargetInvulnerableForFallback(enemy))
                {
                    fallbackSuppressed = true;
                    Debug.Log("Silk Thread skipped the player damage fallback because the target is invulnerable.");
                }
                else
                {
                    try
                    {
                        enemy.Hit(CreatePlayerHit(currentMove));
                        damageDone = Mathf.Max(0, enemyHpBeforePlayerAction - (enemy ? enemy.hp : 0));
                        Debug.Log("Silk Thread used the native-damage fallback because the slash collider did not register a hit.");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("Silk Thread fallback attack failed: " + ex.GetBaseException().Message);
                    }
                }
            }

            if (currentMove != null && currentMove.Kind == MoveKind.Attack && damageDone > 0 && hero)
            {
                int synergySilk = CrestSynergy.GetNormalAttackSilkBonus();
                if (synergySilk > 0)
                {
                    hero.AddSilk(synergySilk, heroEffect: false);
                    Debug.Log("Silk Thread crest/tool synergy granted +" + synergySilk + " silk on a successful crest strike.");
                }
            }

            bool defeatedByPlayerAction = !enemy || enemy.isDead || enemy.hp <= 0;
            battleMessage = currentMove != null && currentMove.Kind == MoveKind.Heal
                ? "Hornet recuperou até 3 pontos de vida."
                : defeatedByPlayerAction
                    ? "Golpe final de Hornet — " + (currentMove != null ? currentMove.Name : "o ataque") + "."
                : damageDone > 0
                    ? "Hornet usou " + (currentMove != null ? currentMove.Name : "o ataque") + " e causou " + damageDone + " de dano."
                    : fallbackSuppressed
                        ? "O alvo está invulnerável; o fallback de dano foi bloqueado."
                    : "O ataque de Hornet não causou dano.";
            currentMove = null;

            if (!enemy || enemy.isDead || enemy.hp <= 0)
            {
                BeginResult("Vitória — inimigo derrotado.", victory: true);
                return;
            }

            PlaceDuelists();
            StartEnemyAction();
        }

        private static bool IsTargetInvulnerableForFallback(HealthManager target)
        {
            if (!target || target.isDead)
                return true;
            try
            {
                return target.IsInvincible || target.CheckInvincible();
            }
            catch
            {
                // A failed state query must never turn a rejected native hit into phantom damage.
                return true;
            }
        }

        private void FinishSummonAction()
        {
            Time.timeScale = originalTimeScale;
            runHeroUpdate = false;
            SummonAttack attack = summonAttackInProgress;
            int damageBefore = enemy ? enemy.hp : 0;
            int damageDone = 0;
            if (enemy && !enemy.isDead && enemy.hp > 0 && attack != null)
            {
                try
                {
                    enemy.Hit(CreateSummonHit(attack));
                    damageDone = Mathf.Max(0, damageBefore - (enemy ? enemy.hp : 0));
                    if (enemy && !enemy.isDead && enemy.hp > 0 && damageDone <= 0)
                    {
                        if (IsTargetInvulnerableForFallback(enemy))
                        {
                            Debug.Log("Silk Thread skipped the summon ApplyExtraDamage fallback because the target is invulnerable.");
                        }
                        else
                        {
                            enemy.ApplyExtraDamage(CreateSummonHit(attack));
                            damageDone = Mathf.Max(0, damageBefore - (enemy ? enemy.hp : 0));
                            Debug.Log("Silk Thread used ApplyExtraDamage fallback for summon '" + attack.DisplayLabel + "'; damage=" + damageDone + ".");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Silk Thread summon attack failed: " + ex.GetBaseException().Message);
                }
            }
            DestroySummonVisual();
            battleMessage = attack == null
                ? "A invocação terminou."
                : damageDone > 0
                    ? "A invocação de " + attack.Owner.DisplayName + " causou " + damageDone + " de dano."
                    : "A invocação de " + attack.Owner.DisplayName + " não causou dano.";
            summonAttackInProgress = null;
            currentMove = null;

            if (!enemy || enemy.isDead || enemy.hp <= 0)
            {
                BeginResult("Vitória — inimigo derrotado.", victory: true);
                return;
            }
            PlaceDuelists();
            StartEnemyAction();
        }

        private HitInstance CreatePlayerHit(BattleMove move)
        {
            return new HitInstance
            {
                Source = hero.gameObject,
                IsFirstHit = true,
                AttackType = AttackTypes.Nail,
                DamageDealt = move.Damage,
                RepresentingTool = move.Skill,
                ToolDamageFlags = move.Skill ? move.Skill.DamageFlags : default(ToolDamageFlags),
                CircleDirection = true,
                MagnitudeMultiplier = 1f,
                SilkGeneration = HitSilkGeneration.Full
            };
        }

        private HitInstance CreateSummonHit(SummonAttack attack)
        {
            return new HitInstance
            {
                Source = hero.gameObject,
                IsFirstHit = true,
                AttackType = AttackTypes.Nail,
                DamageDealt = Mathf.Max(1, attack.Damage),
                RepresentingTool = null,
                ToolDamageFlags = default(ToolDamageFlags),
                CircleDirection = true,
                MagnitudeMultiplier = 1f,
                SilkGeneration = default(HitSilkGeneration)
            };
        }

        private void ResetLimitedToolDamageForHornetTurn()
        {
            limitedToolDamageThisTurn.Clear();
        }

        private bool ShouldSuppressLimitedToolDamage(HealthManager target, ref HitInstance hit)
        {
            if (!target || target != enemy || state == BattleState.Idle || hit.RepresentingTool == null)
                return false;

            ToolItem tool = hit.RepresentingTool;
            if (!IsLimitedDamageTool(tool))
                return false;

            string key = tool.name;
            if (string.IsNullOrEmpty(key))
                key = tool.GetInstanceID().ToString();
            if (limitedToolDamageThisTurn.Add(key))
            {
                Debug.Log("Silk Thread allowed one damage activation for tool/passive '" + key +
                    "' this Hornet turn.");
                return false;
            }

            Debug.Log("Silk Thread suppressed a repeated damage activation from tool/passive '" + key +
                "' during the same Hornet turn.");
            return true;
        }

        private static bool IsLimitedDamageTool(ToolItem tool)
        {
            if (!tool)
                return false;
            try
            {
                // Blue/yellow tools represent passive effects. Red tools with active slots
                // leave an object in the scene and must not multi-hit across the same turn.
                if (tool.Type == ToolItemType.Blue || tool.Type == ToolItemType.Yellow)
                    return true;
                ToolItem.UsageOptions usage = tool.Usage;
                return usage.MaxActive > 0 || usage.MaxActiveAlt > 0;
            }
            catch
            {
                return false;
            }
        }

        internal static void OverrideActiveBattleDamage(HealthManager target, ref HitInstance hit)
        {
            TurnBattleSystem system = activeSystem;
            if (!system || system.enemy != target || system.state == BattleState.Idle)
                return;
            if (system.ShouldSuppressLimitedToolDamage(target, ref hit))
            {
                hit.MagnitudeMultiplier = 0f;
                return;
            }
            hit.MagnitudeMultiplier = 0f;
            if (system.state != BattleState.PlayerAction)
                return;
            system.enemyReactionUntil = Time.unscaledTime + EnemyHitReactionDuration;
            system.enemyIdlePoseForced = false;
            system.ResumeEnemyAnimators();
            if (system.currentMove == null || system.currentMove.Skill)
                return;
            if (system.currentMove.Kind == MoveKind.Summon && system.summonAttackInProgress != null)
                hit.DamageDealt = system.summonAttackInProgress.Damage;
            else
                hit.DamageDealt = system.currentMove.Damage;
        }

        internal static bool ShouldRunTargetFsm(Component fsm)
        {
            TurnBattleSystem system = activeSystem;
            if (!system || system.state == BattleState.Idle || !fsm || !system.enemy)
                return true;

            bool belongsToTarget = fsm.GetComponentInParent<HealthManager>() == system.enemy ||
                fsm.transform == system.enemy.transform || fsm.transform.IsChildOf(system.enemy.transform);
            Transform enemyRoot = system.enemy.transform.root;
            if (!belongsToTarget && enemyRoot &&
                (GlobalEnums.PhysLayers)enemyRoot.gameObject.layer == GlobalEnums.PhysLayers.ENEMIES)
                belongsToTarget = fsm.transform.root == enemyRoot;
            if (!belongsToTarget)
                return true;

            if (system.state == BattleState.EnemyAction && !system.enemyTurnHitResolved)
                return true;
            if (system.state == BattleState.PlayerAction && Time.unscaledTime <= system.enemyReactionUntil)
                return true;
            return system.state == BattleState.Result && system.victoryResult;
        }

        internal static bool ShouldAllowDamageFrom(GameObject source)
        {
            TurnBattleSystem system = activeSystem;
            if (!system || system.state == BattleState.Idle)
                return true;
            if (system.state != BattleState.EnemyAction || system.enemyTurnHitResolved || !system.enemy || !source)
                return false;
            Transform sourceRoot = source.transform.root;
            Transform enemyRoot = system.enemy.transform.root;
            return source.transform == system.enemy.transform || source.transform.IsChildOf(system.enemy.transform) ||
                   sourceRoot == enemyRoot;
        }

        internal static void RecordEnemyTurnHit(GameObject source)
        {
            TurnBattleSystem system = activeSystem;
            if (!system || system.state != BattleState.EnemyAction || system.enemyTurnHitResolved || !system.enemy || !source)
                return;
            Transform sourceRoot = source.transform.root;
            Transform enemyRoot = system.enemy.transform.root;
            bool fromTarget = source.transform == system.enemy.transform || source.transform.IsChildOf(system.enemy.transform) ||
                sourceRoot == enemyRoot;
            if (fromTarget && PlayerHealthTotalStatic() < system.heroHealthBeforeEnemyAction)
            {
                system.enemyTurnHitResolved = true;
                system.enemyReactionUntil = Time.unscaledTime + EnemyHitReactionDuration;
                system.GrantDefendSilkReward();
            }
        }

        internal static void ReduceEnemyDamageIfDefending(GameObject source, ref int damageAmount)
        {
            TurnBattleSystem system = activeSystem;
            if (!system || system.state != BattleState.EnemyAction || !system.defendPending ||
                system.enemyTurnHitResolved || !system.enemy || !source || damageAmount <= 0)
                return;

            Transform sourceRoot = source.transform.root;
            Transform enemyRoot = system.enemy.transform.root;
            bool fromTarget = source.transform == system.enemy.transform || source.transform.IsChildOf(system.enemy.transform) ||
                sourceRoot == enemyRoot;
            if (!fromTarget)
                return;

            int originalDamage = damageAmount;
            damageAmount = Mathf.Max(1, Mathf.CeilToInt(damageAmount * 0.5f));
            Debug.Log("Silk Thread defend reduced enemy damage from " + originalDamage + " to " + damageAmount + ".");
        }

        private void GrantDefendSilkReward()
        {
            if (!defendPending || defendRewardGranted || !hero)
                return;
            int reward = GetCurrentDefendReward();
            hero.AddSilk(reward, heroEffect: false);
            defendRewardGranted = true;
            battleMessage = "Hornet sofreu metade do dano e recebeu +" + reward + " seda.";
            Debug.Log("Silk Thread defend action rewarded " + reward + " silk after a real enemy hit.");
        }

        private static int PlayerHealthTotalStatic()
        {
            if (!PlayerData.HasInstance)
                return 0;
            return PlayerData.instance.health + PlayerData.instance.healthBlue;
        }

        private void StartEnemyAction()
        {
            if (!enemy || enemy.isDead || enemy.hp <= 0)
            {
                BeginResult("Vitória — inimigo derrotado.", victory: true);
                return;
            }
            PlaceDuelists();
            HideBattleReminders();
            FaceEnemyAndTarget();
            ResumeEnemyAnimators();
            heroHealthBeforeEnemyAction = PlayerHealthTotal();
            enemyTurnHitResolved = false;
            battleMessage = EnemyDisplayName() + " prepara o ataque básico…";
            state = BattleState.EnemyAction;
            stateEndTime = Time.unscaledTime + EnemyActionDuration;
            runHeroUpdate = true;
            Time.timeScale = originalTimeScale;
        }

        private void FinishEnemyAction()
        {
            if (!enemyTurnHitResolved && hero && PlayerHealthTotal() >= heroHealthBeforeEnemyAction && !hero.cState.dead)
            {
                ApplyEnemyFallbackHit();
                enemyTurnHitResolved = true;
                stateEndTime = Time.unscaledTime + EnemyHitReactionDuration;
                enemyReactionUntil = stateEndTime;
                battleMessage = EnemyDisplayName() + " acertou o ataque básico.";
                return;
            }

            Time.timeScale = originalTimeScale;
            runHeroUpdate = false;
            PlaceDuelists();
            if (PlayerData.instance.health <= 0 || hero.cState.dead)
            {
                BeginResult("Derrota — Hornet foi derrotada.");
                return;
            }
            if (!enemy || enemy.isDead || enemy.hp <= 0)
            {
                BeginResult("Vitória — inimigo derrotado.", victory: true);
                return;
            }
            state = BattleState.PlayerTurn;
            battleMessage = EnemyDisplayName() + " terminou o turno.";
            defendPending = false;
            defendRewardGranted = false;
            currentMove = null;
            crestSwappedThisTurn = false;
            ResetLimitedToolDamageForHornetTurn();
            BuildMoves();
            selectedMove = Mathf.Clamp(selectedMove, 0, moves.Count - 1);
            StabilizeHornetForNextTurn();
            UpdateSelectedActionPrompt();
            CrestLocalization.SetBattleCrestSwapLabel(crestSwapsUsedThisBattle, crestSwappedThisTurn, CanSwapCrest());
            ShowBattleReminders();
        }

        private void ApplyEnemyFallbackHit()
        {
            if (!hero || !enemy)
                return;
            DamageHero source = enemy.GetComponentsInChildren<DamageHero>(includeInactive: true)
                .FirstOrDefault(candidate => candidate && candidate.hazardType == GlobalEnums.HazardType.ENEMY);
            GameObject sourceObject = source ? source.gameObject : enemy.gameObject;
            CollisionSide side = enemy.transform.position.x < hero.transform.position.x ? CollisionSide.left : CollisionSide.right;
            hero.TakeDamage(sourceObject, side, enemyBasicDamage, GlobalEnums.HazardType.ENEMY, GlobalEnums.DamagePropertyFlags.None);
        }

        private int PlayerHealthTotal()
        {
            if (!PlayerData.HasInstance)
                return 0;
            return PlayerData.instance.health + PlayerData.instance.healthBlue;
        }

        private void BeginResult(string message, bool victory = false)
        {
            HideBattleReminders();
            resultMessage = message;
            victoryResult = victory;
            if (victory)
            {
                bool wasLearned = (battleEnemyKnownSummon != null && !summonUnlockedThisBattle) ||
                    (enemy && EnemySummonRegistry.IsLearned(enemy));
                LearnedEnemySummon learned = summonUnlockedThisBattle
                    ? battleEnemyKnownSummon
                    : pendingDefeatedSummon != null
                        ? EnemySummonRegistry.Commit(pendingDefeatedSummon)
                        : battleEnemyKnownSummon ?? EnemySummonRegistry.Register(enemy);
                pendingDefeatedSummon = null;
                battleEnemyKnownSummon = null;
                if (learned != null && (!wasLearned || summonUnlockedThisBattle))
                    resultMessage += "   ·   INVOCAÇÃO DESBLOQUEADA: " + learned.DisplayName + ".";
                if (learned != null)
                    battleMessage = resultMessage;
                else
                {
                    resultMessage += "   ·   A invocação deste inimigo não pôde ser capturada.";
                    battleMessage = resultMessage;
                    Debug.LogWarning("Silk Thread victory had no summon snapshot to commit; enemy reference may have been destroyed before capture.");
                }
            }
            state = BattleState.Result;
            stateEndTime = Time.unscaledTime + (victory ? VictoryDuration : OtherResultDuration);
            runHeroUpdate = victory || (hero && hero.cState.dead);
            if (victory)
            {
                lootOrigin = enemy ? (Vector2)enemy.transform.position : enemyDuelPosition;
                if (enemyBody)
                {
                    enemyBody.gravityScale = enemyHomeGravity;
                    enemyBody.linearVelocity = Vector2.zero;
                    enemyBody.constraints = enemyHomeConstraints;
                }
                ResumeEnemyAnimators();
                Time.timeScale = originalTimeScale;
                runHeroUpdate = true;
                Debug.Log("Silk Thread victory: leaving native death/drop sequence active and collecting new drops near " + lootOrigin + ".");
            }
            else if (hero && hero.cState.dead)
            {
                Time.timeScale = originalTimeScale;
            }
            else
            {
                Time.timeScale = originalTimeScale;
            }
        }

        private void CollectNewDrops()
        {
            foreach (CurrencyObjectBase drop in FindObjectsByType<CurrencyObjectBase>(FindObjectsSortMode.None))
            {
                if (!drop || !drop.gameObject.activeInHierarchy || lootAlreadyPresent.Contains(drop.GetInstanceID()))
                    continue;
                if (Vector2.Distance(drop.transform.position, lootOrigin) > DropSearchRadius)
                    continue;
                try
                {
                    drop.DoCollect();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Silk Thread could not collect a currency drop: " + ex.GetBaseException().Message);
                }
            }

            foreach (CollectableItemPickup pickup in FindObjectsByType<CollectableItemPickup>(FindObjectsSortMode.None))
            {
                if (!pickup || !pickup.gameObject.activeInHierarchy || lootAlreadyPresent.Contains(pickup.GetInstanceID()) ||
                    pickupAttempts.Contains(pickup.GetInstanceID()))
                    continue;
                if (Vector2.Distance(pickup.transform.position, lootOrigin) > DropSearchRadius)
                    continue;
                try
                {
                    bool activated = PickupActivatedReadField != null && (bool)PickupActivatedReadField.GetValue(pickup);
                    if (activated)
                    {
                        pickupAttempts.Add(pickup.GetInstanceID());
                        continue;
                    }
                    double canPickupTime = PickupCanPickupTimeField != null
                        ? (double)PickupCanPickupTimeField.GetValue(pickup)
                        : 0.0;
                    if (Time.timeAsDouble < canPickupTime)
                        continue;
                    SavedItem item = PickupItemField != null ? PickupItemField.GetValue(pickup) as SavedItem : null;
                    if (item && !item.CanGetMore())
                    {
                        pickupAttempts.Add(pickup.GetInstanceID());
                        continue;
                    }
                    if (PickupInstantMethod != null)
                        PickupInstantMethod.Invoke(pickup, null);
                    pickupAttempts.Add(pickup.GetInstanceID());
                }
                catch (Exception ex)
                {
                    pickupAttempts.Add(pickup.GetInstanceID());
                    Debug.LogWarning("Silk Thread could not collect an item drop: " + ex.GetBaseException().Message);
                }
            }
        }

        private void CalculateDuelPositionsAgain()
        {
            CalculateDuelPositions();
            PlaceDuelists();
        }

        private static void LockBody(Rigidbody2D body, RigidbodyConstraints2D originalConstraints)
        {
            if (!body)
                return;
            body.constraints = originalConstraints | RigidbodyConstraints2D.FreezePositionX |
                RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;
            body.gravityScale = 0f;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }

        private void LockHeroAtDuelPosition()
        {
            if (!hero || hero.cState.dead)
                return;
            if (heroBody)
            {
                LockBody(heroBody, heroHomeConstraints);
                heroBody.position = heroDuelPosition;
            }
            hero.transform.position = heroDuelPosition;
        }

        private void LockEnemyAtDuelPosition()
        {
            if (!enemy || enemy.isDead)
                return;
            if (!enemyBody)
                enemyBody = FindBody(enemy);
            if (enemyBody)
            {
                LockBody(enemyBody, enemyHomeConstraints);
                enemyBody.position = enemyDuelPosition;
            }
            enemy.transform.position = enemyDuelPosition;
        }

        private void SetHornetIdlePose()
        {
            if (!hero || hero.cState.dead)
                return;
            try
            {
                hero.CancelDamageRecoil();
                hero.CancelRecoilHorizontal();
                hero.isHeroInPosition = true;
                if (SetActorStateMethod != null)
                    SetActorStateMethod.Invoke(hero, new object[] { ActorStates.idle });
                hero.StartAnimationControlToIdleForcePlay();
                hero.IgnoreInput();
                Component animator = hero.GetComponent<HeroAnimationController>();
                animator = animator ? animator.GetComponent("tk2dSpriteAnimator") : null;
                if (animator)
                    PlayAnimation(animator, "Idle", pause: false);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Silk Thread could not set Hornet's battle idle pose: " + ex.GetBaseException().Message);
            }
        }

        private void ForceEnemyIdlePose()
        {
            if (!enemy || enemy.isDead)
                return;
            bool foundAnimator = false;
            foreach (Behaviour animator in enemy.GetComponentsInChildren<Behaviour>(includeInactive: true))
            {
                if (!animator || animator.GetType().Name != "tk2dSpriteAnimator")
                    continue;
                foundAnimator = true;
                string clipName = FindIdleClipName(animator);
                bool playedIdle = !string.IsNullOrEmpty(clipName) && PlayAnimation(animator, clipName, pause: false);
                if (!playedIdle)
                    Debug.LogWarning("Silk Thread could not find an idle clip on " + enemy.name + "; letting its current animation finish while its AI remains gated.");
            }
            enemyIdlePoseForced = true;
            if (!foundAnimator)
                Debug.LogWarning("Silk Thread found no 2D animator on the battle target; its position remains locked, but idle animation cannot be forced.");
        }

        private static string FindIdleClipName(Component animator)
        {
            Type animatorType = animator.GetType();
            PropertyInfo libraryProperty = AccessTools.Property(animatorType, "Library");
            object library = libraryProperty == null ? null : libraryProperty.GetValue(animator, null);
            if (library == null)
                return null;
            FieldInfo clipsField = AccessTools.Field(library.GetType(), "clips");
            Array clips = clipsField == null ? null : clipsField.GetValue(library) as Array;
            if (clips == null)
                return null;

            string firstIdle = null;
            foreach (object clip in clips)
            {
                if (clip == null)
                    continue;
                FieldInfo nameField = AccessTools.Field(clip.GetType(), "name");
                string name = nameField == null ? null : nameField.GetValue(clip) as string;
                if (string.IsNullOrWhiteSpace(name) || name.IndexOf("idle", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (string.Equals(name, "Idle", StringComparison.OrdinalIgnoreCase))
                    return name;
                if (firstIdle == null)
                    firstIdle = name;
            }
            return firstIdle;
        }

        private void ResumeEnemyAnimators()
        {
            if (!enemy)
                return;
            foreach (Behaviour animator in enemy.GetComponentsInChildren<Behaviour>(includeInactive: true))
            {
                if (!animator || animator.GetType().Name != "tk2dSpriteAnimator")
                    continue;
                MethodInfo resume = AccessTools.Method(animator.GetType(), "Resume");
                if (resume != null)
                    resume.Invoke(animator, null);
            }
            enemyIdlePoseForced = false;
        }

        private static bool PlayAnimation(Component animator, string clipName, bool pause)
        {
            if (!animator)
                return false;
            Type type = animator.GetType();
            MethodInfo getClip = AccessTools.Method(type, "GetClipByName", new[] { typeof(string) });
            MethodInfo play = AccessTools.Method(type, "Play", new[] { typeof(string) });
            object clip = getClip == null ? null : getClip.Invoke(animator, new object[] { clipName });
            if (clip == null || play == null)
                return false;
            play.Invoke(animator, new object[] { clipName });
            if (pause)
            {
                MethodInfo pauseMethod = AccessTools.Method(type, "Pause");
                if (pauseMethod != null)
                    pauseMethod.Invoke(animator, null);
            }
            return true;
        }

        private void PlaceDuelists()
        {
            if (hero)
            {
                if (heroBody)
                {
                    heroBody.position = heroDuelPosition;
                    heroBody.linearVelocity = Vector2.zero;
                    heroBody.gravityScale = 0f;
                }
                hero.transform.position = heroDuelPosition;
            }
            if (enemy && !enemy.isDead)
            {
                if (!enemyBody)
                    enemyBody = FindBody(enemy);
                if (enemyBody)
                {
                    enemyBody.position = enemyDuelPosition;
                    enemyBody.linearVelocity = Vector2.zero;
                    enemyBody.gravityScale = 0f;
                }
                enemy.transform.position = enemyDuelPosition;
            }
            FaceEnemyAndTarget();
        }

        private void FaceEnemyAndTarget()
        {
            if (!hero || !enemy || enemy.isDead)
                return;
            bool heroFacesRight = enemyDuelPosition.x >= heroDuelPosition.x;
            if (heroFacesRight)
                hero.FaceRight();
            else
                hero.FaceLeft();

            Vector3 scale = enemy.transform.localScale;
            float magnitude = Mathf.Abs(scale.x);
            if (magnitude < 0.001f)
                magnitude = 1f;
            scale.x = heroFacesRight ? -magnitude : magnitude;
            enemy.transform.localScale = scale;
        }

        private void RestoreHomePositions()
        {
            if (hero)
            {
                if (heroBody)
                {
                    heroBody.position = heroHomePosition;
                    heroBody.linearVelocity = heroHomeVelocity;
                    heroBody.gravityScale = heroHomeGravity;
                }
                hero.transform.position = heroHomePosition;
                if (!hero.cState.dead)
                {
                    if (homeFacingRight)
                        hero.FaceRight();
                    else
                        hero.FaceLeft();
                }
            }
            if (enemy && !enemy.isDead)
            {
                if (enemyBody)
                {
                    enemyBody.position = enemyHomePosition;
                    enemyBody.linearVelocity = enemyHomeVelocity;
                    enemyBody.gravityScale = enemyHomeGravity;
                }
                enemy.transform.position = enemyHomePosition;
                enemy.transform.localScale = enemyHomeScale;
            }
        }

        private void StabilizeHornetForNextTurn()
        {
            if (!hero || hero.cState.dead)
                return;
            try
            {
                hero.CancelDamageRecoil();
            hero.CancelRecoilHorizontal();
            hero.isHeroInPosition = true;
            if (SetActorStateMethod != null)
                SetActorStateMethod.Invoke(hero, new object[] { ActorStates.idle });
            hero.StartAnimationControlToIdleForcePlay();
            hero.IgnoreInput();
            if (heroBody)
            {
                LockBody(heroBody, heroHomeConstraints);
                heroBody.position = heroDuelPosition;
            }
                hero.transform.position = heroDuelPosition;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("Silk Thread could not normalize Hornet between turns: " + ex.GetBaseException().Message);
            }
        }

        private void EndBattle(bool restorePositions)
        {
            HideBattleReminders();
            HideProximityPrompt();

            if (PlayerData.HasInstance && !string.IsNullOrEmpty(battleHomeCrestId) &&
                !string.Equals(PlayerData.instance.CurrentCrestID, battleHomeCrestId, StringComparison.Ordinal))
            {
                try
                {
                    ToolItemManager.SetEquippedCrest(battleHomeCrestId);
                    ToolItemManager.SendEquippedChangedEvent(force: true);
                    Debug.Log("Silk Thread restored battle-start crest '" + battleHomeCrestId + "'.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning("Silk Thread could not restore the battle-start crest: " + ex.GetBaseException().Message);
                }
            }

            state = BattleState.Idle;
            runHeroUpdate = false;
            Time.timeScale = originalTimeScale > 0f ? originalTimeScale : 1f;
            if (restorePositions)
                RestoreHomePositions();
            if (hero)
            {
                bool safeToResume = !hero.cState.dead && !hero.cState.transitioning &&
                    !hero.cState.hazardRespawning && !hero.cState.hazardDeath &&
                    GameManager.instance != null && GameManager.instance.GameState == GameState.PLAYING;
                if (safeToResume)
                {
                    try
                    {
                        hero.CancelDamageRecoil();
                        hero.CancelRecoilHorizontal();
                        hero.isHeroInPosition = true;
                        hero.RegainControl();
                        hero.AcceptInput();
                        hero.ResetInputQueues();
                        if (SetActorStateMethod != null)
                            SetActorStateMethod.Invoke(hero, new object[] { ActorStates.idle });
                        hero.StartAnimationControlToIdleForcePlay();
                        if (AcceptingInputField != null)
                            AcceptingInputField.SetValue(hero, true);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning("Silk Thread could not fully restore Hornet input: " + ex.GetBaseException().Message);
                    }
                }
                if (heroBody)
                {
                    heroBody.position = heroHomePosition;
                    heroBody.gravityScale = heroHomeGravity;
                    heroBody.linearVelocity = heroHomeVelocity;
                    heroBody.constraints = heroHomeConstraints;
                }
                hero.transform.position = heroHomePosition;
            }
            if (enemy && !enemy.isDead)
            {
                if (enemyBody)
                {
                    enemyBody.gravityScale = enemyHomeGravity;
                    enemyBody.constraints = enemyHomeConstraints;
                }
                ResumeEnemyAnimators();
            }

            battleMessage = "";
            resultMessage = "";
            selectedMove = 0;
            currentMove = null;
            battleHomeCrestId = "";
            battleCrestBeforeMenu = "";
            crestSwapsUsedThisBattle = 0;
            crestSwappedThisTurn = false;
            defendPending = false;
            defendRewardGranted = false;
            enemy = null;
            enemyMaxHp = 1;
            DestroySummonVisual();
            summonAttackInProgress = null;
            if (pendingDefeatedSummon != null)
                EnemySummonRegistry.Discard(pendingDefeatedSummon);
            pendingDefeatedSummon = null;
            battleEnemyKnownSummon = null;
            heroBody = null;
            enemyBody = null;
            victoryResult = false;
            summonUnlockedThisBattle = false;
            lootAlreadyPresent.Clear();
            pickupAttempts.Clear();
            nextSearchTime = Time.unscaledTime + 0.35f;
            Debug.Log("Silk Thread in-world battle ended; area positions and game time restored.");
        }

        private static Rigidbody2D FindBody(HealthManager target)
        {
            if (!target)
                return null;
            Rigidbody2D body = target.GetComponent<Rigidbody2D>();
            return body ? body : target.GetComponentInParent<Rigidbody2D>();
        }

        internal static void ApplyHitDamageOverride(HealthManager target, ref HitInstance hit)
        {
            OverrideActiveBattleDamage(target, ref hit);
        }

        internal static void ObserveEnemyDeath(HealthManager target)
        {
            TurnBattleSystem system = activeSystem;
            if (!system || !target || system.enemy != target || system.pendingDefeatedSummon == null)
                return;
            if (!target.isDead && target.hp > 0)
                return;

            LearnedEnemySummon learned = EnemySummonRegistry.Commit(system.pendingDefeatedSummon);
            system.pendingDefeatedSummon = null;
            if (learned == null)
                return;
            system.battleEnemyKnownSummon = learned;
            system.summonUnlockedThisBattle = true;
            system.battleMessage = "INVOCAÇÃO DESBLOQUEADA: " + learned.DisplayName + ".";
            Debug.Log("Silk Thread confirmed summon unlock immediately when " + learned.DisplayName + " was defeated.");
        }

        private void DestroySummonVisual()
        {
            if (summonVisual)
                EnemySummonRegistry.DestroyVisual(summonVisual);
            summonVisual = null;
        }

        private void OnGUI()
        {
            EnsureUiStyles();
            if (state != lastVisualState)
            {
                if (state == BattleState.Idle && lastVisualState != BattleState.Idle)
                    visualExitStartedAt = Time.unscaledTime;
                else if (lastVisualState == BattleState.Idle && state != BattleState.Idle)
                    battleIntroStartedAt = Time.unscaledTime;
                lastVisualState = state;
            }

            bool battleUiVisible = state != BattleState.Idle;
            if (state == BattleState.CrestMenu)
                DrawCrestSelectionMenu();
            else if (state == BattleState.SummonMenu)
                DrawSummonSelectionMenu();
            else if (battleUiVisible)
            {
                DrawTurnOrderHud();
                DrawStatusHud();
                DrawEnemyHealthBar();
                DrawBattleActionHud();
            }
            else if (enemy && hero && !reminderShown)
                DrawFallbackPrompt();

            if (battleUiVisible)
            {
                float transitionElapsed = Time.unscaledTime - battleIntroStartedAt;
                float transitionProgress = Mathf.Clamp01(transitionElapsed / 0.34f);
                if (transitionElapsed >= 0f && transitionProgress < 1f)
                    DrawTransitionWipe(transitionProgress, reverse: false);
            }
            else
            {
                float exitProgress = (Time.unscaledTime - visualExitStartedAt) / 0.3f;
                if (exitProgress >= 0f && exitProgress < 1f)
                    DrawTransitionWipe(exitProgress, reverse: true);
            }
        }

        private void DrawCrestSelectionMenu()
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            float menuProgress = Mathf.Clamp01((Time.unscaledTime - visualStateChangedAt) / 0.28f);
            float eased = 1f - Mathf.Pow(1f - menuProgress, 3f);
            GUI.color = new Color(0.015f, 0.012f, 0.02f, 0.78f + 0.18f * eased);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float width = Mathf.Min(760f, Mathf.Max(1f, Screen.width - 32f));
            float height = Mathf.Min(540f, Mathf.Max(1f, Screen.height - 32f));
            float x = (Screen.width - width) * 0.5f + (1f - eased) * Screen.width * 0.16f;
            float y = (Screen.height - height) * 0.5f;
            Rect panel = new Rect(x, y, width, height);
            DrawUiRect(panel, new Color(0.035f, 0.027f, 0.038f, 0.98f));
            DrawUiRect(new Rect(x, y, width, 7f), new Color(0.87f, 0.035f, 0.09f, 1f));
            DrawUiRect(new Rect(x + 12f, y + 15f, 4f, height - 30f), new Color(0.94f, 0.05f, 0.12f, 1f));
            DrawUiSlash(new Rect(x + width - 94f, y + 7f, 100f, 7f), new Color(1f, 1f, 1f, 0.92f), -12f);

            uiSmallStyle.fontSize = Mathf.Clamp(Screen.width / 115, 9, 13);
            uiTitleStyle.fontSize = Mathf.Clamp(Screen.width / 65, 18, 30);
            uiMenuStyle.fontSize = Mathf.Clamp(Screen.width / 105, 12, 18);
            uiBodyStyle.fontSize = Mathf.Clamp(Screen.width / 125, 10, 14);
            GUI.Label(new Rect(x + 28f, y + 18f, width - 56f, 22f), "SILK THREAD   /   LOADOUT", uiSmallStyle);
            GUI.Label(new Rect(x + 28f, y + 38f, width - 56f, 38f), "SELECIONAR BRASÃO", uiTitleStyle);

            string swapInfo = "Trocas nesta batalha: " + crestSwapsUsedThisBattle + "/" + MaxCrestSwapsPerBattle +
                (crestSwappedThisTurn ? "  ·  troca do turno já usada" : "  ·  uma troca por turno");
            GUI.Label(new Rect(x + 28f, y + 78f, width - 56f, 24f), swapInfo, uiBodyStyle);

            float rowHeight = Mathf.Clamp(height / 10f, 30f, 44f);
            float rowsTop = y + 116f;
            float footerHeight = 78f;
            int visibleRows = Mathf.Max(1, Mathf.Min(8, (int)((height - 116f - footerHeight) / rowHeight)));
            int firstVisible = Mathf.Clamp(selectedCrestIndex - visibleRows / 2, 0, Mathf.Max(0, crestMenuItems.Count - visibleRows));
            int lastVisible = Mathf.Min(crestMenuItems.Count, firstVisible + visibleRows);

            if (crestMenuItems.Count == 0)
            {
                GUI.Label(new Rect(x + 30f, rowsTop, width - 60f, rowHeight), "Nenhum brasão desbloqueado disponível.", uiMenuStyle);
            }
            else
            {
                for (int index = firstVisible; index < lastVisible; index++)
                {
                    float rowY = rowsTop + (index - firstVisible) * rowHeight;
                    Rect row = new Rect(x + 24f, rowY, width - 48f, rowHeight - 3f);
                    if (index == selectedCrestIndex)
                    {
                        float pulse = 0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * 7f);
                        DrawUiRect(row, new Color(0.82f, 0.025f, 0.085f, pulse));
                        DrawUiSlash(new Rect(row.x + row.width - 54f, row.y, 62f, row.height), new Color(1f, 1f, 1f, 0.8f), -10f);
                    }
                    else
                        DrawUiRect(row, new Color(1f, 1f, 1f, 0.055f));
                    ToolCrest crest = crestMenuItems[index];
                    string name = crest ? crest.DisplayName.ToString(false) : "Brasão indisponível";
                    if (crest && crest.name == PlayerData.instance.CurrentCrestID)
                        name += "   /   EQUIPADO";
                    GUI.Label(new Rect(row.x + 16f, row.y, row.width - 32f, row.height),
                        (index == selectedCrestIndex ? "◆  " : "   ") + name, uiMenuStyle);
                }
            }

            string navigationLabel = upBindingLabel + "/" + downBindingLabel;
            string limitMessage = CanSwapCrest()
                ? navigationLabel + "=NAVEGAR     ·     " + jumpBindingLabel + "=EQUIPAR     ·     " + attackBindingLabel + "/" + dashBindingLabel + "=FECHAR"
                : (crestSwappedThisTurn
                    ? "TROCA DO TURNO UTILIZADA   ·   " + navigationLabel + "=NAVEGAR   ·   " + attackBindingLabel + "/" + dashBindingLabel + "=FECHAR"
                    : "LIMITE DE TROCAS ATINGIDO   ·   " + navigationLabel + "=NAVEGAR   ·   " + attackBindingLabel + "/" + dashBindingLabel + "=FECHAR");
            float footerY = y + height - footerHeight;
            DrawUiRect(new Rect(x + 22f, footerY, width - 44f, 2f), new Color(1f, 1f, 1f, 0.3f));
            uiSmallStyle.fontSize = Mathf.Clamp(Screen.width / 150, 8, 12);
            GUI.Label(new Rect(x + 26f, footerY + 10f, width - 52f, 25f), limitMessage, uiSmallStyle);
            GUI.Label(new Rect(x + 26f, footerY + 38f, width - 52f, 23f), "A seleção não consome o turno. O limite continua a ser uma troca por turno e três por batalha.", uiBodyStyle);
            GUI.matrix = previousMatrix;
        }

        private void DrawSummonSelectionMenu()
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            float menuProgress = Mathf.Clamp01((Time.unscaledTime - visualStateChangedAt) / 0.28f);
            float eased = 1f - Mathf.Pow(1f - menuProgress, 3f);
            GUI.color = new Color(0.015f, 0.012f, 0.02f, 0.78f + 0.18f * eased);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            float width = Mathf.Min(760f, Mathf.Max(1f, Screen.width - 32f));
            float height = Mathf.Min(540f, Mathf.Max(1f, Screen.height - 32f));
            float x = (Screen.width - width) * 0.5f + (1f - eased) * Screen.width * 0.16f;
            float y = (Screen.height - height) * 0.5f;
            Rect panel = new Rect(x, y, width, height);
            DrawUiRect(panel, new Color(0.035f, 0.027f, 0.038f, 0.98f));
            DrawUiRect(new Rect(x, y, width, 7f), new Color(0.87f, 0.035f, 0.09f, 1f));
            DrawUiRect(new Rect(x + 12f, y + 15f, 4f, height - 30f), new Color(0.94f, 0.05f, 0.12f, 1f));
            DrawUiSlash(new Rect(x + width - 94f, y + 7f, 100f, 7f), new Color(1f, 1f, 1f, 0.92f), -12f);

            uiSmallStyle.fontSize = Mathf.Clamp(Screen.width / 115, 9, 13);
            uiTitleStyle.fontSize = Mathf.Clamp(Screen.width / 65, 18, 30);
            uiMenuStyle.fontSize = Mathf.Clamp(Screen.width / 105, 12, 18);
            uiBodyStyle.fontSize = Mathf.Clamp(Screen.width / 125, 10, 14);
            GUI.Label(new Rect(x + 28f, y + 18f, width - 56f, 22f), "SILK THREAD   /   INVOCAR", uiSmallStyle);
            GUI.Label(new Rect(x + 28f, y + 38f, width - 56f, 38f), "ESCOLHER GOLPE", uiTitleStyle);
            GUI.Label(new Rect(x + 28f, y + 78f, width - 56f, 24f),
                "Custo: " + GetCurrentSummonCost() + " seda   ·   a invocação consome o turno de Hornet", uiBodyStyle);

            float rowHeight = Mathf.Clamp(height / 10f, 30f, 44f);
            float rowsTop = y + 116f;
            float footerHeight = 78f;
            int visibleRows = Mathf.Max(1, Mathf.Min(8, (int)((height - 116f - footerHeight) / rowHeight)));
            int firstVisible = Mathf.Clamp(selectedSummonAttack - visibleRows / 2, 0,
                Mathf.Max(0, summonMenuItems.Count - visibleRows));
            int lastVisible = Mathf.Min(summonMenuItems.Count, firstVisible + visibleRows);
            for (int index = firstVisible; index < lastVisible; index++)
            {
                float rowY = rowsTop + (index - firstVisible) * rowHeight;
                Rect row = new Rect(x + 24f, rowY, width - 48f, rowHeight - 3f);
                if (index == selectedSummonAttack)
                {
                    float pulse = 0.88f + 0.12f * Mathf.Sin(Time.unscaledTime * 7f);
                    DrawUiRect(row, new Color(0.82f, 0.025f, 0.085f, pulse));
                    DrawUiSlash(new Rect(row.x + row.width - 54f, row.y, 62f, row.height),
                        new Color(1f, 1f, 1f, 0.8f), -10f);
                }
                else
                    DrawUiRect(row, new Color(1f, 1f, 1f, 0.055f));
                SummonAttack attack = summonMenuItems[index];
                string attackLabel = attack == null
                    ? "Golpe indisponível"
                    : attack.DisplayLabel + "  ·  DANO " + Mathf.Max(1, attack.Damage);
                GUI.Label(new Rect(row.x + 16f, row.y, row.width - 32f, row.height),
                    (index == selectedSummonAttack ? "◆  " : "   ") + attackLabel, uiMenuStyle);
            }

            string navigationLabel = upBindingLabel + "/" + downBindingLabel;
            float footerY = y + height - footerHeight;
            DrawUiRect(new Rect(x + 22f, footerY, width - 44f, 2f), new Color(1f, 1f, 1f, 0.3f));
            uiSmallStyle.fontSize = Mathf.Clamp(Screen.width / 150, 8, 12);
            GUI.Label(new Rect(x + 26f, footerY + 10f, width - 52f, 25f),
                navigationLabel + "=NAVEGAR     ·     " + jumpBindingLabel + "=INVOCAR     ·     " +
                attackBindingLabel + "/" + dashBindingLabel + "=FECHAR", uiSmallStyle);
            GUI.Label(new Rect(x + 26f, footerY + 38f, width - 52f, 23f),
                "Ordenado por dano decrescente. Escolha um golpe aprendido do inimigo derrotado.", uiBodyStyle);
            GUI.matrix = previousMatrix;
        }

        private void DrawBattleActionHud()
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            float margin = Mathf.Min(28f, Screen.width * 0.035f);
            float width = Mathf.Min(1050f, Mathf.Max(1f, Screen.width - margin * 2f));
            float height = Mathf.Clamp(Screen.height * 0.19f, 112f, 148f);
            float x = Mathf.Clamp((Screen.width - width) * 0.5f, margin, Mathf.Max(margin, Screen.width - width - margin));
            float y = Mathf.Max(0f, Screen.height - height - Mathf.Min(24f, Screen.height * 0.03f));
            Rect panel = new Rect(x, y, width, height);
            DrawUiRect(panel, new Color(0.018f, 0.015f, 0.023f, 0.94f));
            DrawUiRect(new Rect(x, y, width, 4f), new Color(0.91f, 0.025f, 0.1f, 1f));
            DrawUiRect(new Rect(x, y + height - 3f, width, 3f), new Color(1f, 1f, 1f, 0.75f));
            DrawUiRect(new Rect(x + 13f, y + 10f, 3f, height - 20f), new Color(0.91f, 0.025f, 0.1f, 1f));
            float tagWidth = Mathf.Clamp(width * 0.19f, 120f, 210f);
            DrawUiRect(new Rect(x + 20f, y + 14f, tagWidth, 21f), new Color(0.86f, 0.025f, 0.09f, 1f));

            string headline = battleMessage;
            if (state == BattleState.Result && !string.IsNullOrWhiteSpace(resultMessage))
                headline = resultMessage;
            if (state == BattleState.PlayerTurn && moves.Count > 0)
            {
                BattleMove move = moves[Mathf.Clamp(selectedMove, 0, moves.Count - 1)];
                headline = move.Name;
            }
            if (string.IsNullOrWhiteSpace(headline))
                headline = state == BattleState.Preparing ? "A preparar o confronto…" : "Combate por turnos";
            if (headline.Length > 72)
                headline = headline.Substring(0, 69) + "…";

            string controls = state == BattleState.PlayerTurn
                ? "↑/↓=MUDAR AÇÃO     ·     " + jumpBindingLabel + "=EXECUTAR     ·     " +
                    dashBindingLabel + "=RECUAR     ·     " + attackBindingLabel + "=BRASÕES"
                : (state == BattleState.Result ? "" : "AGUARDA A AÇÃO DO TURNO…");
            GUI.Label(new Rect(x + 27f, y + 12f, tagWidth - 8f, 22f),
                state == BattleState.PlayerTurn ? "COMANDO  " + (selectedMove + 1).ToString("00") + "/" + moves.Count.ToString("00") : "SILK THREAD  /  BATALHA", uiSmallStyle);

            uiTitleStyle.fontSize = Mathf.Clamp(Screen.width / 82, 14, 24);
            GUI.Label(new Rect(x + 27f, y + 39f, width - 54f, 34f), headline, uiTitleStyle);
            Rect messageBox = new Rect(x + 27f, y + 73f, width - 54f, 27f);
            DrawUiRect(messageBox, new Color(0.91f, 0.9f, 0.88f, 0.96f));
            DrawUiRect(new Rect(messageBox.x, messageBox.y, 5f, messageBox.height), new Color(0.88f, 0.025f, 0.09f, 1f));
            string dialogue = battleMessage;
            if (string.IsNullOrWhiteSpace(dialogue))
                dialogue = "A tensão mantém o confronto em curso.";
            uiDialogueStyle.fontSize = Mathf.Clamp(Screen.width / 140, 9, 13);
            uiDialogueStyle.normal.textColor = new Color(0.08f, 0.055f, 0.065f, 1f);
            GUI.Label(new Rect(messageBox.x + 12f, messageBox.y + 1f, messageBox.width - 20f, messageBox.height - 2f), dialogue, uiDialogueStyle);
            uiBodyStyle.fontSize = Mathf.Clamp(Screen.width / 150, 8, 12);
            if (state == BattleState.PlayerTurn)
            {
                float arrowY = y + 101f;
                DrawUiRect(new Rect(x + 28f, arrowY + 1f, 19f, 18f), new Color(0.87f, 0.025f, 0.09f, 1f));
                DrawUiRect(new Rect(x + 50f, arrowY + 1f, 19f, 18f), new Color(0.87f, 0.025f, 0.09f, 1f));
                uiBodyStyle.alignment = TextAnchor.MiddleCenter;
                uiBodyStyle.fontStyle = FontStyle.Bold;
                uiBodyStyle.normal.textColor = Color.white;
                GUI.Label(new Rect(x + 28f, arrowY, 19f, 20f), "↑", uiBodyStyle);
                GUI.Label(new Rect(x + 50f, arrowY, 19f, 20f), "↓", uiBodyStyle);
                uiBodyStyle.alignment = TextAnchor.MiddleLeft;
                uiBodyStyle.fontStyle = FontStyle.Normal;
                uiBodyStyle.normal.textColor = new Color(0.91f, 0.89f, 0.9f, 1f);
                GUI.Label(new Rect(x + 78f, arrowY, width - 106f, 20f), controls, uiBodyStyle);
            }
            else
                GUI.Label(new Rect(x + 29f, y + 101f, width - 58f, 20f), controls, uiBodyStyle);
            float pulseWidth = 64f + 20f * Mathf.Sin(Time.unscaledTime * 5f);
            DrawUiRect(new Rect(x + width - pulseWidth - 18f, y + 5f, pulseWidth, 3f), new Color(1f, 1f, 1f, 0.55f));
            GUI.matrix = previousMatrix;
        }

        private void DrawTurnOrderHud()
        {
            Matrix4x4 previousMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            float lineWidth = Mathf.Min(700f, Mathf.Max(220f, Screen.width * 0.56f));
            float lineX = (Screen.width - lineWidth) * 0.5f;
            float lineCenter = Screen.width * 0.5f;
            float lineY = Mathf.Max(28f, Screen.height * 0.075f);
            float titleWidth = Mathf.Min(520f, lineWidth - 80f);
            float titleX = lineCenter - titleWidth * 0.5f;
            float gap = titleWidth * 0.5f + 18f;
            float segmentWidth = Mathf.Max(0f, lineWidth * 0.5f - gap);
            bool enemyActs = state == BattleState.EnemyAction;
            bool showHornet = !enemyActs || (state == BattleState.Result && victoryResult);
            string turnLabel = showHornet ? "TURNO DE HORNET" : "TURNO DO INIMIGO";
            Color red = new Color(0.9f, 0.025f, 0.1f, 1f);
            DrawUiRect(new Rect(lineX, lineY + 27f, segmentWidth, 4f), red);
            DrawUiRect(new Rect(lineCenter + gap, lineY + 27f, segmentWidth, 4f), red);
            DrawUiRect(new Rect(titleX - 12f, lineY + 25f, titleWidth + 24f, 8f), new Color(0.018f, 0.015f, 0.023f, 0.72f));

            uiTitleStyle.fontSize = Mathf.Clamp(Screen.width / 65, 18, 32);
            uiTitleStyle.fontStyle = FontStyle.Bold;
            uiTitleStyle.alignment = TextAnchor.MiddleCenter;
            uiTitleStyle.normal.textColor = new Color(0.04f, 0.02f, 0.025f, 0.95f);
            GUI.Label(new Rect(titleX + 3f, lineY + 3f, titleWidth, 38f), turnLabel, uiTitleStyle);
            uiTitleStyle.normal.textColor = Color.white;
            GUI.Label(new Rect(titleX, lineY, titleWidth, 38f), turnLabel, uiTitleStyle);
            uiTitleStyle.alignment = TextAnchor.MiddleLeft;
            uiTitleStyle.fontStyle = FontStyle.Bold;
            GUI.matrix = previousMatrix;
        }

        private void DrawStatusHud()
        {
            if (!PlayerData.HasInstance)
                return;
            float margin = Mathf.Min(28f, Screen.width * 0.035f);
            float width = Mathf.Min(340f, Mathf.Max(190f, Screen.width * 0.43f));
            float height = 88f;
            float x = Mathf.Max(margin, Screen.width - width - margin);
            float y = Mathf.Max(18f, Screen.height * 0.035f);
            Rect panel = new Rect(x, y, width, height);
            DrawUiRect(panel, new Color(0.018f, 0.015f, 0.023f, 0.91f));
            DrawUiRect(new Rect(x + width - 5f, y, 5f, height), new Color(0.9f, 0.025f, 0.1f, 1f));

            int health = Mathf.Max(0, PlayerData.instance.health);
            int maxHealth = Mathf.Max(1, PlayerData.instance.CurrentMaxHealth);
            int silk = Mathf.Max(0, PlayerData.instance.silk);
            uiSmallStyle.fontSize = Mathf.Clamp(Screen.width / 155, 8, 11);
            string heroLine = "HORNET   ·   HP " + health + "/" + maxHealth + "   ·   SEDA " + silk;
            GUI.Label(new Rect(x + 12f, y + 5f, width - 24f, 22f), heroLine, uiSmallStyle);

            string equippedCrestName = PlayerData.instance.CurrentCrestID;
            ToolCrest equippedCrest = ToolItemManager.GetCrestByName(equippedCrestName);
            if (equippedCrest)
            {
                string localizedName = equippedCrest.DisplayName.ToString(false);
                if (!string.IsNullOrWhiteSpace(localizedName) && !localizedName.StartsWith("!!", StringComparison.Ordinal))
                    equippedCrestName = localizedName;
            }
            if (string.IsNullOrWhiteSpace(equippedCrestName))
                equippedCrestName = "Nenhum";
            if (equippedCrestName.Length > 22)
                equippedCrestName = equippedCrestName.Substring(0, 21) + "…";
            Rect crestTile = new Rect(x + 10f, y + 27f, 34f, 34f);
            DrawUiRect(crestTile, new Color(1f, 1f, 1f, 0.12f));
            DrawUiRect(new Rect(crestTile.x, crestTile.y, 3f, crestTile.height), new Color(0.9f, 0.025f, 0.1f, 1f));
            if (equippedCrest && equippedCrest.CrestSprite)
            {
                UiIcon crestIcon = CreateSpriteIcon(equippedCrest.CrestSprite);
                if (crestIcon != null)
                    DrawUiIcon(new Rect(crestTile.x + 4f, crestTile.y + 4f, 26f, 26f), crestIcon, Color.white);
            }
            GUI.Label(new Rect(x + 52f, y + 27f, width - 64f, 19f), "BRASÃO EQUIPADO", uiSmallStyle);
            GUI.Label(new Rect(x + 52f, y + 44f, width - 64f, 18f), equippedCrestName, uiBodyStyle);

            Rect healthBar = new Rect(x + 12f, y + 74f, width - 24f, 8f);
            DrawUiRect(healthBar, new Color(1f, 1f, 1f, 0.16f));
            DrawUiRect(new Rect(healthBar.x, healthBar.y, healthBar.width * Mathf.Clamp01((float)health / maxHealth), healthBar.height),
                new Color(0.9f, 0.025f, 0.1f, 1f));
        }

        private void DrawEnemyHealthBar()
        {
            if (!enemy || !enemy.transform)
                return;
            Camera camera = Camera.main;
            if (!camera)
                return;

            float worldTop = enemy.transform.position.y + 1f;
            Collider2D collider = enemy.GetComponentInChildren<Collider2D>(includeInactive: true);
            Renderer renderer = enemy.GetComponentInChildren<Renderer>(includeInactive: true);
            if (collider)
                worldTop = collider.bounds.max.y + 0.22f;
            else if (renderer)
                worldTop = renderer.bounds.max.y + 0.22f;

            Vector3 screenPoint = camera.WorldToScreenPoint(new Vector3(enemy.transform.position.x, worldTop, enemy.transform.position.z));
            if (screenPoint.z <= 0f)
                return;
            float screenY = Screen.height - screenPoint.y;
            if (screenY < -30f || screenY > Screen.height + 20f)
                return;

            float width = Mathf.Clamp(Screen.width * 0.14f, 96f, 190f);
            float height = Mathf.Clamp(Screen.height * 0.012f, 7f, 11f);
            float x = Mathf.Clamp(screenPoint.x - width * 0.5f, 8f, Screen.width - width - 8f);
            float y = Mathf.Clamp(screenY - height, 8f, Screen.height - height - 8f);
            Rect bar = new Rect(x, y, width, height);
            DrawUiRect(new Rect(x - 4f, y - 4f, width + 8f, height + 8f), new Color(0.018f, 0.015f, 0.023f, 0.92f));
            DrawUiRect(bar, new Color(1f, 1f, 1f, 0.22f));
            float ratio = Mathf.Clamp01((float)Mathf.Max(0, enemy.hp) / Mathf.Max(1, enemyMaxHp));
            DrawUiRect(new Rect(x, y, width * ratio, height), new Color(0.9f, 0.025f, 0.1f, 1f));
            DrawUiRect(new Rect(x, y, width, 2f), new Color(1f, 0.78f, 0.78f, 0.9f));
        }

        private static UiIcon CreateSpriteIcon(Sprite sprite)
        {
            if (!sprite || !sprite.texture || sprite.texture.width <= 0 || sprite.texture.height <= 0)
                return null;
            Rect source = sprite.textureRect;
            return new UiIcon
            {
                Texture = sprite.texture,
                TextureCoordinates = new Rect(
                    source.x / sprite.texture.width,
                    source.y / sprite.texture.height,
                    source.width / sprite.texture.width,
                    source.height / sprite.texture.height)
            };
        }

        private static void DrawUiIcon(Rect rect, UiIcon icon, Color color)
        {
            if (icon == null || !icon.Texture)
                return;
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTextureWithTexCoords(rect, icon.Texture, icon.TextureCoordinates, true);
            GUI.color = oldColor;
        }

        private void DrawFallbackPrompt()
        {
            float width = Mathf.Max(1f, Mathf.Min(520f, Screen.width - 24f));
            float height = Mathf.Max(1f, Mathf.Min(62f, Screen.height - 20f));
            float x = (Screen.width - width) * 0.5f;
            float y = Mathf.Max(10f, Screen.height - height - 20f);
            DrawUiRect(new Rect(x, y, width, height), new Color(0.02f, 0.016f, 0.024f, 0.92f));
            DrawUiRect(new Rect(x, y, 5f, height), new Color(0.9f, 0.025f, 0.1f, 1f));
            uiTitleStyle.fontSize = Mathf.Clamp(Screen.width / 100, 12, 20);
            GUI.Label(new Rect(x + 18f, y + 5f, width - 36f, height - 10f), "INICIAR BATALHA   /   V   ·   CLIQUE NO ANALÓGICO DIREITO", uiTitleStyle);
        }

        private void EnsureUiStyles()
        {
            if (uiTitleStyle != null)
                return;
            uiTitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
                wordWrap = false,
                normal = { textColor = Color.white }
            };
            uiBodyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                wordWrap = false,
                normal = { textColor = new Color(0.91f, 0.89f, 0.9f, 1f) }
            };
            uiSmallStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                clipping = TextClipping.Clip,
                wordWrap = false,
                normal = { textColor = new Color(1f, 0.83f, 0.83f, 1f) }
            };
            uiMenuStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                clipping = TextClipping.Clip,
                wordWrap = false,
                normal = { textColor = Color.white }
            };
            uiDialogueStyle = new GUIStyle(uiBodyStyle)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
        }

        private static void DrawUiRect(Rect rect, Color color)
        {
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private static void DrawUiSlash(Rect rect, Color color, float angle)
        {
            Matrix4x4 oldMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, new Vector2(rect.center.x, rect.center.y));
            DrawUiRect(rect, color);
            GUI.matrix = oldMatrix;
        }

        private void DrawTransitionWipe(float progress, bool reverse)
        {
            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            float eased = progress * progress * (3f - 2f * progress);
            float width = Screen.width * 0.72f;
            float x = reverse
                ? Mathf.Lerp(Screen.width * 1.35f, -width * 0.6f, eased)
                : Mathf.Lerp(-width * 0.8f, Screen.width * 1.25f, eased);
            float alpha = Mathf.Sin(progress * Mathf.PI) * 0.72f;
            GUIUtility.RotateAroundPivot(-12f, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            GUI.color = new Color(0.86f, 0.018f, 0.085f, alpha);
            GUI.DrawTexture(new Rect(x, -Screen.height * 0.3f, width, Screen.height * 1.6f), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, alpha * 0.82f);
            GUI.DrawTexture(new Rect(x + width * 0.13f, -Screen.height * 0.3f, Mathf.Max(4f, width * 0.012f), Screen.height * 1.6f), Texture2D.whiteTexture);
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }

        private string EnemyDisplayName()
        {
            if (!enemy)
                return "inimigo";
            string name = enemy.gameObject.name;
            return name.Replace("(Clone)", "").Trim();
        }
    }

    [HarmonyPatch(typeof(HeroController), "Update")]
    internal static class TurnBattleHeroUpdatePatch
    {
        private static bool Prefix()
        {
            return !TurnBattleSystem.IsBattleActive || TurnBattleSystem.ShouldRunHeroUpdate;
        }
    }

    [HarmonyPatch(typeof(GameManager), "PauseGameToggle")]
    internal static class TurnBattlePausePatch
    {
        private static bool Prefix()
        {
            return !TurnBattleSystem.IsBattleActive;
        }
    }

    [HarmonyPatch(typeof(InventoryItemToolManager), "CanChangeEquips", new Type[0])]
    internal static class TurnBattleAllowCrestChangePatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!TurnBattleSystem.IsNativeCrestMenuOpen)
                return true;
            __result = TurnBattleSystem.CanChooseCrestInNativeMenu;
            return false;
        }
    }

    [HarmonyPatch(typeof(InventoryItemToolManager), "CanChangeEquips", new Type[]
    {
        typeof(ToolItemType), typeof(InventoryItemToolManager.CanChangeEquipsTypes)
    })]
    internal static class TurnBattleBlockToolEquipPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!TurnBattleSystem.IsNativeCrestMenuOpen)
                return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(InventoryItemToolManager), "BeginSwitchingCrest")]
    internal static class TurnBattleLimitCrestSwitchPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!TurnBattleSystem.IsNativeCrestMenuOpen || TurnBattleSystem.CanChooseCrestInNativeMenu)
                return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(InventoryToolCrestSlot), "Submit")]
    internal static class TurnBattleBlockCrestSlotSubmitPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!TurnBattleSystem.IsNativeCrestMenuOpen)
                return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(InventoryToolCrestSlot), "DoPress")]
    internal static class TurnBattleBlockCrestSlotPressPatch
    {
        private static bool Prefix(ref bool __result)
        {
            if (!TurnBattleSystem.IsNativeCrestMenuOpen)
                return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(HealthManager), "Hit", new Type[] { typeof(HitInstance) })]
    internal static class TurnBattleDamageScalePatch
    {
        private static void Prefix(HealthManager __instance, ref HitInstance __0)
        {
            TurnBattleSystem.ApplyHitDamageOverride(__instance, ref __0);
        }

        private static void Postfix(HealthManager __instance)
        {
            TurnBattleSystem.ObserveEnemyDeath(__instance);
        }
    }

    [HarmonyPatch(typeof(HeroController), "TakeDamage")]
    internal static class TurnBattleDamageGatePatch
    {
        private static bool Prefix(GameObject __0, ref int __2)
        {
            bool allowed = TurnBattleSystem.ShouldAllowDamageFrom(__0);
            if (allowed)
                TurnBattleSystem.ReduceEnemyDamageIfDefending(__0, ref __2);
            return allowed;
        }

        private static void Postfix(GameObject __0)
        {
            TurnBattleSystem.RecordEnemyTurnHit(__0);
        }
    }

    [HarmonyPatch]
    internal static class TurnBattleEnemyFsmPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type[] types =
            {
                AccessTools.TypeByName("PlayMakerFSM"),
                AccessTools.TypeByName("PlayMakerFixedUpdate"),
                AccessTools.TypeByName("PlayMakerLateUpdate")
            };
            string[] methods = { "Update", "FixedUpdate", "LateUpdate" };
            for (int i = 0; i < types.Length; i++)
            {
                MethodBase method = types[i] == null ? null : AccessTools.Method(types[i], methods[i]);
                if (method != null)
                    yield return method;
            }
        }

        private static bool Prefix(Component __instance)
        {
            return TurnBattleSystem.ShouldRunTargetFsm(__instance);
        }
    }
}
