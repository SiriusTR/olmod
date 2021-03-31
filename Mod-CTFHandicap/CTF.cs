using Harmony;
using Overload;
using System;
using UnityEngine;

namespace GameMod
{
    public static class CTFHandicap
    {
        public static float CalculateDamageScale(MpTeam team, MpTeam myTeam)
        {
            const int handicapScoreGapThreshold = 3;
            const int uncertaintyWeightingFactor = 10;

            var theirScore = NetworkMatch.GetTeamScore(team);
            var myScore = NetworkMatch.GetTeamScore(myTeam);
            var scoreGap = theirScore - myScore;
            if (Math.Abs(scoreGap) >= handicapScoreGapThreshold)
            {
                return (myScore + uncertaintyWeightingFactor) / ((float)(theirScore + uncertaintyWeightingFactor));
            }
            else
            {
                // No scaling
                return 1.0f;
            }
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "ApplyDamage")]
    class CTFDamageHandicap
    {
        private static void Prefix(ref DamageInfo di, PlayerShip __instance)
        {
            if (Overload.NetworkManager.IsServer() && !NetworkMatch.m_postgame &&
                GameplayManager.IsMultiplayer && NetworkMatch.GetMode() == CTF.MatchModeCTF)
            {
                var ownerPlayer = di.owner?.GetComponent<Player>();
                if (ownerPlayer && ownerPlayer.m_mp_team != __instance.c_player.m_mp_team)
                {
                    var damageScale = CTFHandicap.CalculateDamageScale(ownerPlayer.m_mp_team, __instance.c_player.m_mp_team);
                    Debug.Log($"Damage scale (team {ownerPlayer.m_mp_team} vs team {__instance.c_player.m_mp_team}): {damageScale}");
                    if (damageScale != 1.0f)
                    {
                        var preDamage = di.damage;
                        // Damage scale is meant to mean "how much more damage they do than you".
                        // Need to take the square root here to make that accurate.
                        di.damage *= (float)Math.Sqrt(damageScale);
                        Debug.Log($"Player \"{ownerPlayer.m_mp_name}\" damage changed from {preDamage} to {di.damage}");
                    }
                }
            }
        }
    }

    [HarmonyPatch(typeof(UIElement), "DrawTeamScore")]
    class CTFTeamScore
    {
        private static void Postfix(Vector2 pos, MpTeam team, int score, float w, bool my_team, UIElement __instance)
        {
            if (NetworkMatch.GetMode() == CTF.MatchModeCTF)
            {
                var damageScale = CTFHandicap.CalculateDamageScale(team, GameManager.m_local_player.m_mp_team);
                if (damageScale != 1.0f)
                {
                    Color color = damageScale > 1.0f ? UIManager.m_col_red : UIManager.m_col_green;
                    string damageLabel = string.Format("{0:+0%;-0%} DAMAGE", damageScale - 1.0f);
                    __instance.DrawStringSmall(damageLabel, pos - Vector2.right * (w - 100f), 0.4f, StringOffset.LEFT, color, 1f);
                }
            }
        }
    }
}