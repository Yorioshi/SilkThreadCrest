using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SilkThreadCrest
{
    internal enum SummonSortMode
    {
        DamageDescending,
        AcquisitionOrder,
        Name
    }

    internal sealed class SummonAttack
    {
        internal string Name;
        internal int Damage;
        internal string AnimationClip;
        internal int AttackOrder;
        internal LearnedEnemySummon Owner;

        internal string DisplayLabel => (Owner == null ? "Inimigo" : Owner.DisplayName) + " — " + Name;
    }

    internal sealed class LearnedEnemySummon
    {
        internal string Key;
        internal string DisplayName;
        internal int AcquisitionOrder;
        internal GameObject VisualTemplate;
        internal readonly List<SummonAttack> Attacks = new List<SummonAttack>();
    }

    public sealed class SilkThreadSummonSaveData
    {
        public int Version = 1;
        public int NextAcquisitionOrder = 1;
        public List<SilkThreadSummonSaveRecord> Invocations = new List<SilkThreadSummonSaveRecord>();
    }

    public sealed class SilkThreadSummonSaveRecord
    {
        public string Key;
        public string DisplayName;
        public int AcquisitionOrder;
        public List<SilkThreadSummonAttackSaveRecord> Attacks = new List<SilkThreadSummonAttackSaveRecord>();
    }

    public sealed class SilkThreadSummonAttackSaveRecord
    {
        public string Name;
        public int Damage;
        public string AnimationClip;
        public int AttackOrder;
    }

    internal static class EnemySummonRegistry
    {
        private static readonly List<LearnedEnemySummon> learned = new List<LearnedEnemySummon>();
        private static int nextAcquisitionOrder = 1;

        private static readonly string[] AttackWords =
        {
            "attack", "slash", "strike", "charge", "dash", "shoot", "shot", "spit", "slam", "jump",
            "stab", "bite", "beam", "blast", "throw", "fire", "acid", "roar", "hit", "damage", "cast"
        };

        // These names/tags identify contact damage, body hurtboxes, auras and environmental hazards,
        // rather than deliberate attacks that should become summon choices.
        private static readonly string[] PassiveAttackTokens =
        {
            "bodydamage", "body_damage", "body-damage", "contact", "contactdamage", "contact_damage",
            "contact-damage", "touchdamage", "touch_damage", "touch-damage", "damageaura", "damage_aura",
            "damage-aura", "aura", "proximitydamage", "proximity_damage", "proximity-damage", "passive",
            "constantdamage", "constant_damage", "constant-damage", "environmental", "damagecollider",
            "damage_collider", "damage-collider", "collisiondamage", "collision_damage", "collision-damage",
            "passivehitbox", "passive_hitbox", "passive-hitbox", "hurtbox", "spikes", "spike", "thorn", "thorns"
        };

        private static readonly string[] PassiveAttackTags =
        {
            "hazard", "spike", "spikes", "thorn", "thorns", "contactdamage", "contact_damage",
            "passive", "environmental", "damage"
        };

        internal static IReadOnlyList<LearnedEnemySummon> Learned => learned;
        internal static bool HasAny => learned.Any(entry => entry != null && entry.Attacks.Count > 0);

        internal static bool IsLearned(HealthManager defeated)
        {
            return defeated && defeated.gameObject && learned.Any(entry => entry.Key == MakeKey(defeated.gameObject));
        }

        internal static LearnedEnemySummon Find(HealthManager defeated)
        {
            return defeated && defeated.gameObject
                ? learned.FirstOrDefault(entry => entry.Key == MakeKey(defeated.gameObject))
                : null;
        }

        internal static LearnedEnemySummon Capture(HealthManager source)
        {
            if (!source || !source.gameObject)
                return null;

            string key = MakeKey(source.gameObject);
            if (learned.Any(entry => entry.Key == key))
                return null;

            LearnedEnemySummon entry = new LearnedEnemySummon
            {
                Key = key,
                DisplayName = CleanName(source.gameObject.name),
                AcquisitionOrder = nextAcquisitionOrder++
            };
            CollectAttacks(source.gameObject, entry);
            entry.VisualTemplate = CreateVisualTemplate(source.gameObject, key);
            if (!entry.VisualTemplate)
                Debug.LogWarning("Silk Thread could not archive a visual template for pending summon " + entry.DisplayName + ".");
            Debug.Log("Silk Thread captured pending summon " + entry.DisplayName + " with " + entry.Attacks.Count + " attack(s).");
            return entry;
        }

        internal static LearnedEnemySummon Commit(LearnedEnemySummon entry)
        {
            if (entry == null)
                return null;
            LearnedEnemySummon existing = learned.FirstOrDefault(item => item.Key == entry.Key);
            if (existing != null)
            {
                Discard(entry);
                return existing;
            }

            if (entry.AcquisitionOrder <= 0)
                entry.AcquisitionOrder = nextAcquisitionOrder++;
            else
                nextAcquisitionOrder = Mathf.Max(nextAcquisitionOrder, entry.AcquisitionOrder + 1);
            learned.Add(entry);
            Debug.Log("Silk Thread learned summon " + entry.DisplayName + " with " + entry.Attacks.Count + " attack(s).");
            QueueGameSave();
            return entry;
        }

        internal static void Discard(LearnedEnemySummon entry)
        {
            if (entry == null || learned.Contains(entry))
                return;
            DestroyEntryVisual(entry);
        }

        internal static LearnedEnemySummon Register(HealthManager defeated)
        {
            LearnedEnemySummon existing = defeated && defeated.gameObject
                ? learned.FirstOrDefault(entry => entry.Key == MakeKey(defeated.gameObject))
                : null;
            if (existing != null)
            {
                Debug.Log("Silk Thread summon already learned for " + existing.DisplayName + ".");
                return existing;
            }
            return Commit(Capture(defeated));
        }

        internal static List<SummonAttack> GetAllAttacks(SummonSortMode sortMode = SummonSortMode.DamageDescending)
        {
            IEnumerable<SummonAttack> attacks = learned
                .Where(entry => entry != null)
                .SelectMany(entry => entry.Attacks)
                .Where(attack => attack != null);

            switch (sortMode)
            {
                case SummonSortMode.AcquisitionOrder:
                    return attacks
                        .OrderBy(attack => attack.Owner == null ? int.MaxValue : attack.Owner.AcquisitionOrder)
                        .ThenBy(attack => attack.AttackOrder)
                        .ThenBy(attack => attack.Name, StringComparer.OrdinalIgnoreCase)
                        .ToList();
                case SummonSortMode.Name:
                    return attacks
                        .OrderBy(attack => attack.Owner == null ? "" : attack.Owner.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(attack => attack.Name, StringComparer.OrdinalIgnoreCase)
                        .ThenByDescending(attack => attack.Damage)
                        .ToList();
                default:
                    return attacks
                        .OrderByDescending(attack => attack.Damage)
                        .ThenBy(attack => attack.Owner == null ? "" : attack.Owner.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(attack => attack.AttackOrder)
                        .ToList();
            }
        }

        internal static SilkThreadSummonSaveData ExportSaveData()
        {
            SilkThreadSummonSaveData data = new SilkThreadSummonSaveData
            {
                Version = 1,
                NextAcquisitionOrder = Mathf.Max(1, nextAcquisitionOrder)
            };
            foreach (LearnedEnemySummon entry in learned)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Key))
                    continue;
                SilkThreadSummonSaveRecord record = new SilkThreadSummonSaveRecord
                {
                    Key = entry.Key,
                    DisplayName = entry.DisplayName,
                    AcquisitionOrder = entry.AcquisitionOrder
                };
                foreach (SummonAttack attack in entry.Attacks)
                {
                    if (attack == null)
                        continue;
                    record.Attacks.Add(new SilkThreadSummonAttackSaveRecord
                    {
                        Name = attack.Name,
                        Damage = Mathf.Max(1, attack.Damage),
                        AnimationClip = attack.AnimationClip,
                        AttackOrder = attack.AttackOrder
                    });
                }
                data.Invocations.Add(record);
            }
            return data;
        }

        internal static void RestoreFromSaveData(SilkThreadSummonSaveData data)
        {
            Clear();
            nextAcquisitionOrder = data == null ? 1 : Mathf.Max(1, data.NextAcquisitionOrder);
            if (data == null || data.Invocations == null)
                return;

            foreach (SilkThreadSummonSaveRecord record in data.Invocations)
            {
                if (record == null || string.IsNullOrWhiteSpace(record.Key))
                    continue;
                LearnedEnemySummon entry = new LearnedEnemySummon
                {
                    Key = CleanName(record.Key).ToLowerInvariant(),
                    DisplayName = string.IsNullOrWhiteSpace(record.DisplayName) ? CleanName(record.Key) : CleanName(record.DisplayName),
                    AcquisitionOrder = record.AcquisitionOrder > 0 ? record.AcquisitionOrder : nextAcquisitionOrder++
                };
                if (record.Attacks != null)
                {
                    foreach (SilkThreadSummonAttackSaveRecord attackRecord in record.Attacks)
                    {
                        if (attackRecord == null || string.IsNullOrWhiteSpace(attackRecord.Name))
                            continue;
                        entry.Attacks.Add(new SummonAttack
                        {
                            Owner = entry,
                            Name = CleanName(attackRecord.Name),
                            Damage = Mathf.Max(1, attackRecord.Damage),
                            AnimationClip = attackRecord.AnimationClip,
                            AttackOrder = attackRecord.AttackOrder
                        });
                    }
                }
                if (entry.Attacks.Count > 0)
                    learned.Add(entry);
                nextAcquisitionOrder = Mathf.Max(nextAcquisitionOrder, entry.AcquisitionOrder + 1);
            }
            Debug.Log("Silk Thread restored " + learned.Count + " learned summon(s) from the game save.");
        }

        internal static void Clear()
        {
            foreach (LearnedEnemySummon entry in learned)
                DestroyEntryVisual(entry);
            learned.Clear();
            nextAcquisitionOrder = 1;
        }

        internal static void TryRehydrateVisualTemplates()
        {
            if (!learned.Any(entry => entry != null && !entry.VisualTemplate))
                return;

            HealthManager[] candidates;
            try
            {
                candidates = UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None);
            }
            catch
            {
                return;
            }

            foreach (LearnedEnemySummon entry in learned)
            {
                if (entry == null || entry.VisualTemplate)
                    continue;
                HealthManager source = candidates.FirstOrDefault(candidate => candidate && candidate.gameObject &&
                    MakeKey(candidate.gameObject) == entry.Key);
                if (!source)
                    continue;
                entry.VisualTemplate = CreateVisualTemplate(source.gameObject, entry.Key);
                if (entry.VisualTemplate)
                    Debug.Log("Silk Thread rehydrated the visual template for saved summon " + entry.DisplayName + ".");
            }
        }

        internal static GameObject CreateVisual(LearnedEnemySummon entry)
        {
            if (entry == null || !entry.VisualTemplate)
                return null;
            try
            {
                GameObject visual = UnityEngine.Object.Instantiate(entry.VisualTemplate);
                visual.name = "SilkThread_SummonVisual_" + entry.Key;
                visual.transform.SetParent(null, true);
                visual.SetActive(false);
                ConfigureVisualOnly(visual);
                visual.SetActive(true);
                SetAllLayers(visual, IgnoreRaycastLayer());
                return visual;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Silk Thread could not create summon visual for " + entry.DisplayName + ": " +
                    exception.GetBaseException().Message);
                return null;
            }
        }

        internal static void PlayAttack(GameObject visual, SummonAttack attack)
        {
            if (!visual)
                return;
            bool played = false;
            foreach (tk2dSpriteAnimator animator in visual.GetComponentsInChildren<tk2dSpriteAnimator>(true))
            {
                if (!animator || string.IsNullOrWhiteSpace(attack?.AnimationClip))
                    continue;
                try
                {
                    object clip = animator.GetClipByName(attack.AnimationClip);
                    if (clip == null)
                        continue;
                    animator.Play(attack.AnimationClip);
                    played = true;
                }
                catch
                {
                }
            }

            foreach (Animator animator in visual.GetComponentsInChildren<Animator>(true))
            {
                if (!animator)
                    continue;
                try
                {
                    if (!string.IsNullOrWhiteSpace(attack?.AnimationClip))
                    {
                        animator.Play(attack.AnimationClip, 0, 0f);
                        played = true;
                    }
                }
                catch
                {
                }
            }

            if (!played)
            {
                foreach (tk2dSpriteAnimator animator in visual.GetComponentsInChildren<tk2dSpriteAnimator>(true))
                {
                    if (!animator || animator.DefaultClip == null)
                        continue;
                    try
                    {
                        animator.Play(animator.DefaultClip.name);
                        break;
                    }
                    catch
                    {
                    }
                }
            }
        }

        internal static void DestroyVisual(GameObject visual)
        {
            if (visual)
                UnityEngine.Object.Destroy(visual);
        }

        private static void CollectAttacks(GameObject source, LearnedEnemySummon entry)
        {
            DamageHero[] sources = source.GetComponentsInChildren<DamageHero>(true)
                .Where(IsSafeAttackSource)
                .ToArray();
            int index = 1;
            foreach (DamageHero damager in sources)
            {
                string label = DescribeAttack(damager, index);
                string clip = FindAttackClip(source, label);
                if (entry.Attacks.Any(attack => attack.Name == label && attack.Damage == damager.damageDealt))
                    continue;
                entry.Attacks.Add(new SummonAttack
                {
                    Owner = entry,
                    Name = label,
                    Damage = Mathf.Max(1, damager.damageDealt),
                    AnimationClip = clip,
                    AttackOrder = index
                });
                index++;
            }

            if (entry.Attacks.Count == 0)
            {
                entry.Attacks.Add(new SummonAttack
                {
                    Owner = entry,
                    Name = "Golpe básico",
                    Damage = 1,
                    AnimationClip = FindAttackClip(source, "attack"),
                    AttackOrder = 1
                });
            }
        }

        private static bool IsSafeAttackSource(DamageHero damager)
        {
            if (!damager || damager.damageDealt <= 0)
                return false;
            if (damager.hazardType == GlobalEnums.HazardType.SPIKES ||
                damager.hazardType == GlobalEnums.HazardType.COAL_SPIKES)
                return false;
            if (IsPassiveDamageComponent(damager))
                return false;
            return damager.hazardType == GlobalEnums.HazardType.ENEMY || NameLooksLikeAttack(damager.gameObject.name);
        }

        private static bool IsPassiveDamageComponent(DamageHero damager)
        {
            for (Transform current = damager ? damager.transform : null; current; current = current.parent)
            {
                if (ContainsPassiveToken(current.name))
                    return true;
                string tag = current.gameObject.tag;
                if (!string.IsNullOrWhiteSpace(tag) && !string.Equals(tag, "Untagged", StringComparison.OrdinalIgnoreCase) &&
                    PassiveAttackTags.Any(value => string.Equals(value, tag, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            return false;
        }

        private static bool ContainsPassiveToken(string value)
        {
            string lower = (value ?? "").ToLowerInvariant();
            return PassiveAttackTokens.Any(token => lower.Contains(token));
        }

        private static string DescribeAttack(DamageHero damager, int index)
        {
            string candidate = damager ? damager.gameObject.name : "";
            if (string.IsNullOrWhiteSpace(candidate) || candidate.Equals("DamageHero", StringComparison.OrdinalIgnoreCase))
                candidate = damager && damager.transform.parent ? damager.transform.parent.name : "";
            candidate = CleanName(candidate);
            if (string.IsNullOrWhiteSpace(candidate) || candidate.Equals("DamageHero", StringComparison.OrdinalIgnoreCase) ||
                candidate.Equals("Collider", StringComparison.OrdinalIgnoreCase))
                candidate = "Golpe " + index;
            return candidate;
        }

        private static string FindAttackClip(GameObject source, string attackLabel)
        {
            string[] words = (attackLabel ?? "").Split(new[] { ' ', '_', '-', '/', ':' }, StringSplitOptions.RemoveEmptyEntries);
            string best = null;
            int bestScore = 0;
            foreach (tk2dSpriteAnimator animator in source.GetComponentsInChildren<tk2dSpriteAnimator>(true))
            {
                if (!animator || !animator.Library || animator.Library.clips == null)
                    continue;
                foreach (tk2dSpriteAnimationClip clip in animator.Library.clips)
                {
                    if (clip == null || string.IsNullOrWhiteSpace(clip.name))
                        continue;
                    string value = clip.name.ToLowerInvariant();
                    int score = 0;
                    foreach (string word in words)
                        if (word.Length > 2 && value.Contains(word.ToLowerInvariant())) score += 5;
                    foreach (string word in AttackWords)
                        if (value.Contains(word)) score += 2;
                    if (score > bestScore && !value.Contains("idle"))
                    {
                        bestScore = score;
                        best = clip.name;
                    }
                }
            }
            return best;
        }

        private static GameObject CreateVisualTemplate(GameObject source, string key)
        {
            try
            {
                GameObject template = UnityEngine.Object.Instantiate(source);
                template.name = "SilkThread_SummonArchive_" + key;
                template.transform.SetParent(null, true);
                template.SetActive(false);
                ConfigureVisualOnly(template);
                SetAllLayers(template, IgnoreRaycastLayer());
                UnityEngine.Object.DontDestroyOnLoad(template);
                return template;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Silk Thread failed to archive summon template: " + exception.GetBaseException().Message);
                return null;
            }
        }

        private static void ConfigureVisualOnly(GameObject root)
        {
            foreach (Behaviour behaviour in root.GetComponentsInChildren<Behaviour>(true))
            {
                if (!behaviour)
                    continue;
                string typeName = behaviour.GetType().Name;
                bool visualBehaviour = typeName == "tk2dSpriteAnimator" || typeName == "tk2dSprite" ||
                    typeName == "tk2dBaseSprite" || typeName == "Animator";
                if (visualBehaviour)
                    behaviour.enabled = true;
                else
                    UnityEngine.Object.DestroyImmediate(behaviour);
            }
            foreach (Collider2D collider in root.GetComponentsInChildren<Collider2D>(true))
                if (collider) collider.enabled = false;
            foreach (Rigidbody2D body in root.GetComponentsInChildren<Rigidbody2D>(true))
                if (body) body.simulated = false;
            foreach (Rigidbody body in root.GetComponentsInChildren<Rigidbody>(true))
                if (body) body.isKinematic = true;
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                if (renderer) renderer.enabled = true;
        }

        private static void SetAllLayers(GameObject root, int layer)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }

        private static int IgnoreRaycastLayer()
        {
            int layer = LayerMask.NameToLayer("Ignore Raycast");
            return layer < 0 ? 2 : layer;
        }

        private static bool NameLooksLikeAttack(string value)
        {
            string lower = (value ?? "").ToLowerInvariant();
            return AttackWords.Any(word => lower.Contains(word));
        }

        private static string MakeKey(GameObject source)
        {
            return CleanName(source.name).ToLowerInvariant();
        }

        private static string CleanName(string value)
        {
            string cleaned = (value ?? "").Replace("(Clone)", "").Trim();
            if (cleaned.Length > 42)
                cleaned = cleaned.Substring(0, 42);
            return cleaned;
        }

        private static void DestroyEntryVisual(LearnedEnemySummon entry)
        {
            if (entry == null || !entry.VisualTemplate)
                return;
            UnityEngine.Object.Destroy(entry.VisualTemplate);
            entry.VisualTemplate = null;
        }

        private static void QueueGameSave()
        {
            SilkThreadSaveData.WriteSidecar();
            try
            {
                if (GameManager.instance)
                    GameManager.instance.QueueSaveGame();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Silk Thread could not queue a native save after learning a summon: " + exception.Message);
            }
        }
    }
}
