using UnityEngine;

namespace SilkThreadCrest
{
    internal sealed class SilkThreadMove : MonoBehaviour
    {
        private const float PullSpeed = 18f;
        private const float StopDistance = 0.55f;
        private const float MaxPullTime = 0.7f;
        private const float NoHitTimeout = 1.8f;
        private const float SpearSpeed = 24f;
        private const float SpearRadius = 0.12f;
        private const float SpearMaxDistance = 12f;

        private HeroController hero;
        private Downspike attack;
        private HealthManager target;
        private HitInstance firstHit;
        private GameObject threadObject;
        private LineRenderer threadLine;
        private LineRenderer spearLine;
        private Vector2 spearPosition;
        private float moveStartTime;
        private float targetSetTime;
        private float pullStartTime;
        private float spearTravel;
        private float pullInvulnerabilityExpiry;
        private object pullInvulnerabilityToken;
        private bool pulling;
        private bool secondHitApplied;
        private bool firstHitCaptured;
        private bool projectileFinished;

        internal bool ActiveMove { get; private set; }
        internal bool HasTarget => target;
        internal bool HasCustomHitbox => attack && attack.EnemyDamager;

        internal void BeginMove(HeroController owner, Downspike downspike)
        {
            if (!owner)
                return;

            EndMove();
            hero = owner;
            attack = downspike;
            target = null;
            firstHit = default(HitInstance);
            pulling = false;
            secondHitApplied = false;
            firstHitCaptured = false;
            projectileFinished = false;
            ActiveMove = true;
            moveStartTime = Time.time;
            targetSetTime = 0f;
            spearTravel = 0f;
            spearPosition = (Vector2)owner.transform.position + Vector2.down * 0.25f;
            EnsureThreadVisual();
            if (HasCustomHitbox)
                SilkThreadCrestPlugin.LogInfo("Silk Thread spear armed with Downspike damager: " + attack.EnemyDamager.name + "; maximum downward reach=" + SpearMaxDistance.ToString("0") + " units.");
            else
                SilkThreadCrestPlugin.LogWarning("Silk Thread could not find the active Downspike damager; using the game's original enemy hitbox for this attack.");
        }

        internal void SetTarget(HealthManager enemy, HitInstance hitInstance)
        {
            if (!ActiveMove || !enemy || (target && target != enemy))
                return;

            bool isNewTarget = !target;
            target = enemy;
            if (hitInstance.Source || hitInstance.DamageDealt > 0)
            {
                firstHit = hitInstance;
                if (!firstHitCaptured)
                {
                    SilkThreadCrestPlugin.LogInfo("Silk Thread first-hit response captured: " + hitInstance.DamageDealt + " damage to " + enemy.name + ".");
                    firstHitCaptured = true;
                }
            }
            if (isNewTarget)
            {
                targetSetTime = Time.time;
                SilkThreadCrestPlugin.LogInfo("Silk Thread spear hit " + enemy.name + " for " + hitInstance.DamageDealt + "; starting tether pull.");
            }
        }

        private void SetPendingTarget(HealthManager enemy)
        {
            if (!ActiveMove || !enemy || target)
                return;
            target = enemy;
            targetSetTime = Time.time;
        }

        private void ClearPendingTarget(HealthManager enemy)
        {
            if (target == enemy && firstHit.DamageDealt <= 0)
                target = null;
        }

        internal void BeginPull()
        {
            if (!ActiveMove || pulling || !hero || !target)
                return;

            pulling = true;
            pullStartTime = Time.time;
            GrantPullInvulnerability();
            hero.FinishDownspike(false);
            hero.RelinquishControl();
            hero.AffectedByGravity(false);
            hero.Body.linearVelocity = Vector2.zero;
        }

        private void Update()
        {
            UpdatePullInvulnerability();
            if (!ActiveMove)
                return;
            if (!hero)
            {
                EndMove();
                return;
            }

            if (target && !pulling && Time.time - targetSetTime > 0.14f)
                BeginPull();

            UpdateThreadVisual();

            if (!pulling && !target && Time.time - moveStartTime > NoHitTimeout)
                EndMove();
        }

        private void FixedUpdate()
        {
            if (!ActiveMove || !hero)
                return;

            if (!pulling)
            {
                if (!target && attack && attack.IsDamagerActive && attack.CanDamageEnemies && !projectileFinished)
                    AdvanceSpear();
                return;
            }

            if (!target)
            {
                CompletePull();
                return;
            }

            Vector2 destination = (Vector2)target.transform.position + Vector2.up * 0.42f;
            Vector2 current = hero.Body.position;
            Vector2 next = Vector2.MoveTowards(current, destination, PullSpeed * Time.fixedDeltaTime);
            hero.Body.linearVelocity = Vector2.zero;
            hero.Body.MovePosition(next);

            if (Vector2.Distance(next, destination) <= StopDistance || Time.time - pullStartTime >= MaxPullTime)
                CompletePull();
        }

        private void AdvanceSpear()
        {
            float distance = SpearSpeed * Time.fixedDeltaTime;
            RaycastHit2D[] hits = Physics2D.CircleCastAll(spearPosition, SpearRadius, Vector2.down, distance, ~0);
            foreach (RaycastHit2D hit in hits)
            {
                if (!hit.collider)
                    continue;

                GlobalEnums.PhysLayers layer = (GlobalEnums.PhysLayers)hit.collider.gameObject.layer;
                DamageHero hazard = hit.collider.GetComponent<DamageHero>();
                if (!hazard)
                    hazard = hit.collider.GetComponentInParent<DamageHero>();
                bool terrainHit = layer == GlobalEnums.PhysLayers.TERRAIN || layer == GlobalEnums.PhysLayers.SOFT_TERRAIN;
                bool spikeHit = hazard && (hazard.hazardType == GlobalEnums.HazardType.SPIKES || hazard.hazardType == GlobalEnums.HazardType.COAL_SPIKES);
                if (terrainHit || spikeHit)
                {
                    spearTravel += hit.distance;
                    spearPosition = hit.centroid;
                    projectileFinished = true;
                    SilkThreadCrestPlugin.LogInfo("Silk Thread spear reached " + (spikeHit ? "spikes" : "terrain") + " at " + spearTravel.ToString("0.0") + " units; starting a short pogo.");
                    hero.DownspikeBounce(false);
                    return;
                }

                HealthManager enemy = hit.collider.GetComponent<HealthManager>();
                if (!enemy)
                    enemy = hit.collider.GetComponentInParent<HealthManager>();
                if (!enemy || enemy == hero.GetComponent<HealthManager>() || enemy.isDead || enemy.transform.IsChildOf(hero.transform))
                    continue;

                DamageEnemies damager = attack.EnemyDamager;
                if (!damager)
                    break;

                SetPendingTarget(enemy);
                if (damager.TryDoDamage(hit.collider))
                {
                    spearPosition = hit.centroid;
                    return;
                }

                ClearPendingTarget(enemy);
            }

            spearPosition += Vector2.down * distance;
            spearTravel += distance;
            if (spearTravel >= SpearMaxDistance)
                projectileFinished = true;
        }

        private void CompletePull()
        {
            if (!ActiveMove || !hero)
            {
                EndMove();
                return;
            }

            if (!secondHitApplied && target && firstHit.DamageDealt > 0)
            {
                secondHitApplied = true;
                HitInstance secondHit = firstHit;
                secondHit.Source = hero.gameObject;
                secondHit.IsFirstHit = false;
                secondHit.IgnoreInvulnerable = true;
                if (!target.isDead)
                {
                    target.ApplyExtraDamage(secondHit);
                    SilkThreadCrestPlugin.LogInfo("Silk Thread follow-up strike applied " + secondHit.DamageDealt + " extra damage to " + target.name + ".");
                }
            }
            else if (target && !firstHitCaptured)
            {
                SilkThreadCrestPlugin.LogWarning("Silk Thread reached its target without receiving the first-hit HitInstance; the follow-up strike was skipped.");
            }

            hero.DownspikeBounceSlightlyShort(false);
            SilkThreadCrestPlugin.LogInfo("Silk Thread follow-up finished; starting the normal short pogo without extra height.");
            EndMove();
        }

        private void GrantPullInvulnerability()
        {
            if (!hero)
                return;

            if (pullInvulnerabilityToken != null)
                hero.RemoveInvulnerabilitySource(pullInvulnerabilityToken);

            pullInvulnerabilityToken = new object();
            hero.AddInvulnerabilitySource(pullInvulnerabilityToken);
            pullInvulnerabilityExpiry = Time.unscaledTime + 1f;
            SilkThreadCrestPlugin.LogInfo("Silk Thread pull grants 1 second of invulnerability.");
        }

        private void UpdatePullInvulnerability()
        {
            if (pullInvulnerabilityToken == null || Time.unscaledTime < pullInvulnerabilityExpiry)
                return;

            if (hero)
                hero.RemoveInvulnerabilitySource(pullInvulnerabilityToken);
            pullInvulnerabilityToken = null;
            SilkThreadCrestPlugin.LogInfo("Silk Thread pull invulnerability expired.");
        }

        private void EnsureThreadVisual()
        {
            if (threadObject)
                Destroy(threadObject);

            threadObject = new GameObject("SilkThreadCrest_Thread");
            threadObject.transform.SetParent(transform, false);
            threadLine = threadObject.AddComponent<LineRenderer>();
            threadLine.useWorldSpace = true;
            threadLine.positionCount = 2;
            threadLine.startWidth = 0.045f;
            threadLine.endWidth = 0.028f;
            threadLine.startColor = new Color(1f, 0.48f, 0.76f, 0.95f);
            threadLine.endColor = new Color(1f, 0.80f, 0.90f, 0.95f);
            threadLine.numCapVertices = 3;
            Shader shader = Shader.Find("Sprites/Default");
            if (shader)
                threadLine.material = new Material(shader);

            spearLine = threadObject.AddComponent<LineRenderer>();
            spearLine.useWorldSpace = true;
            spearLine.positionCount = 5;
            spearLine.startWidth = 0.075f;
            spearLine.endWidth = 0.035f;
            spearLine.startColor = Color.white;
            spearLine.endColor = Color.white;
            if (shader)
                spearLine.material = new Material(shader);
        }

        private void UpdateThreadVisual()
        {
            if (!threadLine || !hero)
                return;

            Vector3 start = hero.transform.position + Vector3.down * 0.2f;
            Vector3 end = target
                ? target.transform.position
                : new Vector3(spearPosition.x, spearPosition.y, hero.transform.position.z);
            threadLine.SetPosition(0, start);
            threadLine.SetPosition(1, end);

            if (spearLine)
            {
                spearLine.SetPosition(0, end + Vector3.up * 0.20f);
                spearLine.SetPosition(1, end + Vector3.down * 0.11f);
                spearLine.SetPosition(2, end + Vector3.up * 0.015f + Vector3.left * 0.075f);
                spearLine.SetPosition(3, end + Vector3.down * 0.11f);
                spearLine.SetPosition(4, end + Vector3.up * 0.015f + Vector3.right * 0.075f);
            }
        }

        internal void EndMove()
        {
            ActiveMove = false;
            pulling = false;
            target = null;
            attack = null;
            if (threadObject)
            {
                Destroy(threadObject);
                threadObject = null;
                threadLine = null;
                spearLine = null;
            }
        }

        private void OnDestroy()
        {
            if (hero && pullInvulnerabilityToken != null)
                hero.RemoveInvulnerabilitySource(pullInvulnerabilityToken);
            if (threadObject)
                Destroy(threadObject);
        }
    }
}
