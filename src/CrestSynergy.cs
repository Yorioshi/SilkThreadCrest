using System;
using System.Linq;

namespace SilkThreadCrest
{
    internal enum CrestSynergyAction
    {
        Skill,
        Heal,
        Summon
    }

    internal static class CrestSynergy
    {
        private const int MinimumFixedActionCost = 1;

        internal static int GetActionCost(CrestSynergyAction action, int baseCost)
        {
            if (baseCost <= 0)
                return 0;

            ToolCrest crest = CurrentCrest();
            int reduction = 0;
            switch (action)
            {
                case CrestSynergyAction.Skill:
                    // Tool-focused crests make equipped skills cheaper in the duel.
                    if (MatchesCrest(crest, "Toolmaster", "Architect"))
                        reduction++;
                    break;
                case CrestSynergyAction.Heal:
                    // Wanderer gives the player a cheaper emergency recovery option.
                    if (MatchesCrest(crest, "Wanderer"))
                        reduction++;
                    break;
                case CrestSynergyAction.Summon:
                    // Bard/Witch-style crests support the new invocation action.
                    if (MatchesCrest(crest, "Bard", "Witch"))
                        reduction++;
                    if (IsToolEquipped("SpoolExtender", "SilkSpool", "Spool"))
                        reduction++;
                    break;
            }
            return Math.Max(MinimumFixedActionCost, baseCost - reduction);
        }

        internal static int GetNormalAttackSilkBonus()
        {
            int bonus = 0;
            ToolCrest crest = CurrentCrest();
            if (MatchesCrest(crest, "Hunter"))
                bonus++;
            if (IsToolEquipped("WhiteRing", "SilkRegen", "SilkRing"))
                bonus++;
            return bonus;
        }

        internal static int GetDefendRewardBonus()
        {
            return MatchesCrest(CurrentCrest(), "Reaper") ? 1 : 0;
        }

        internal static string DescribeCurrentSynergy()
        {
            ToolCrest crest = CurrentCrest();
            string crestName = crest ? crest.name : "nenhum";
            int silkBonus = GetNormalAttackSilkBonus();
            int defendBonus = GetDefendRewardBonus();
            int skillCost = GetActionCost(CrestSynergyAction.Skill, PlayerData.HasInstance ? PlayerData.instance.SilkSkillCost : 0);
            int summonCost = GetActionCost(CrestSynergyAction.Summon, 3);
            return crestName + " | skill=" + skillCost + " | invocação=" + summonCost +
                " | golpe+seda=" + silkBonus + " | defesa+seda=" + defendBonus;
        }

        private static ToolCrest CurrentCrest()
        {
            if (!PlayerData.HasInstance || string.IsNullOrWhiteSpace(PlayerData.instance.CurrentCrestID))
                return null;
            try
            {
                return ToolItemManager.GetCrestByName(PlayerData.instance.CurrentCrestID);
            }
            catch
            {
                return null;
            }
        }

        private static bool MatchesCrest(ToolCrest crest, params string[] aliases)
        {
            if (!crest || aliases == null || aliases.Length == 0)
                return false;
            string displayName = "";
            try
            {
                displayName = crest.DisplayName.ToString(false);
            }
            catch
            {
            }
            return MatchesAny(crest.name, displayName, aliases);
        }

        private static bool IsToolEquipped(params string[] aliases)
        {
            try
            {
                return ToolItemManager.GetAllTools().Any(tool => tool && tool.IsEquipped &&
                    MatchesAny(tool.name, tool.DisplayName.ToString(false), aliases));
            }
            catch
            {
                return false;
            }
        }

        private static bool MatchesAny(string internalName, string displayName, string[] aliases)
        {
            string internalLower = (internalName ?? "").ToLowerInvariant();
            string displayLower = (displayName ?? "").ToLowerInvariant();
            return aliases.Any(alias =>
            {
                string value = (alias ?? "").ToLowerInvariant();
                return value.Length > 0 && (internalLower.Contains(value) || displayLower.Contains(value));
            });
        }
    }
}
