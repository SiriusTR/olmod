using HarmonyLib;
using Overload;
using System;
using UnityEngine;
using System.Linq;

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

        public static void DrawDamageScaleLabel(Vector2 pos, MpTeam team, float w, UIElement instance)
        {
            var damageScale = CalculateDamageScale(team, GameManager.m_local_player.m_mp_team);
            if (damageScale != 1.0f)
            {
                Color color = damageScale > 1.0f ? UIManager.m_col_red : UIManager.m_col_green;
                string damageLabel = string.Format("{0:+0%;-0%} DAMAGE", damageScale - 1.0f);
                instance.DrawStringSmall(damageLabel, pos - Vector2.right * (w - 100f), 0.4f, StringOffset.LEFT, color, 1f);
            }
        }
    }

    [HarmonyPatch(typeof(PlayerShip), "ApplyDamage")]
    class CTFDamageHandicap
    {
        private static void Prefix(ref DamageInfo di, PlayerShip __instance)
        {
            if (NetworkManager.IsServer() && !NetworkMatch.m_postgame &&
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

    // Originally this patched DrawTeamScore but that now fights with olmod, so we have to do this instead
    // Note this only works with two teams currently, but most of the olmod machinery to deal with more is
    // inaccessible and it's not worth the effort to duplicate it
    [HarmonyPatch(typeof(UIElement), "DrawMpScoreboardRaw")]
    class CTFTeamScore
    {
        private static void Postfix(Vector2 pos, UIElement __instance)
        {
            if (NetworkMatch.GetMode() == CTF.MatchModeCTF)
            {
                if (NetworkMatch.GetTeamScore(MpTeam.TEAM1) > NetworkMatch.GetTeamScore(MpTeam.TEAM0))
                {
                    CTFHandicap.DrawDamageScaleLabel(pos, MpTeam.TEAM1, 350f, __instance);
                    int numTeamPlayers = NetworkManager.m_PlayersForScoreboard
                        .Where(player => player.m_mp_team == MpTeam.TEAM1 && !player.m_spectator).Count();
                    pos.y += 120f + (numTeamPlayers * 25f);
                    CTFHandicap.DrawDamageScaleLabel(pos, MpTeam.TEAM0, 350f, __instance);
                }
                else
                {
                    CTFHandicap.DrawDamageScaleLabel(pos, MpTeam.TEAM0, 350f, __instance);
                    int numTeamPlayers = NetworkManager.m_PlayersForScoreboard
                        .Where(player => player.m_mp_team == MpTeam.TEAM0 && !player.m_spectator).Count();
                    pos.y += 120f + (numTeamPlayers * 25f);
                    CTFHandicap.DrawDamageScaleLabel(pos, MpTeam.TEAM1, 350f, __instance);
                }
            }
        }
    }
}