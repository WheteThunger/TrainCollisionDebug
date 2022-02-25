using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using static TrainEngine;

namespace Oxide.Plugins
{
    [Info("Train Collision Debug", "WhiteThunder", "0.2.0")]
    [Description("Debugs workcart collision issues.")]
    internal class TrainCollisionDebug : CovalencePlugin
    {
        #region Fields

        private const float DebugDrawDuration = 60;
        private const float SpeedLimitMultiplier = 1.5f;

        [PluginReference]
        private Plugin CargoTrainEvent;

        #endregion

        #region Helper Methods

        private bool IsCargoTrain(TrainEngine workcart)
        {
            var result = CargoTrainEvent?.Call("IsTrainSpecial", workcart.net.ID);
            return result is bool && (bool)result;
        }

        private void DestroyWorkcart(TrainEngine workcart)
        {
            if (workcart.IsDestroyed)
                return;

            var hitInfo = new HitInfo(null, workcart, Rust.DamageType.Explosion, float.MaxValue, workcart.transform.position);
            hitInfo.UseProtection = false;
            workcart.Die(hitInfo);
        }

        private void CheckWorkcartSpeed(TrainEngine workcart)
        {
            if (workcart == null || workcart.IsDestroyed)
                return;

            var position = workcart.transform.position;
            var currentSpeed = workcart.TrackSpeed;

            if (currentSpeed > workcart.maxSpeed * SpeedLimitMultiplier)
            {
                workcart.TrackSpeed = workcart.maxSpeed;

                LogWarning($"Workcart emergency slowed from {currentSpeed:f2} to {workcart.TrackSpeed} because it was moving too fast after collision. Location: {position}");

                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player.IsAdmin)
                    {
                        player.SendConsoleCommand("ddraw.text", DebugDrawDuration, new Color(1, 0.5f, 0), position + Vector3.up, $"Workcart emergency slowed:\n{currentSpeed:f2} -> {workcart.TrackSpeed}");
                        player.SendConsoleCommand("ddraw.sphere", DebugDrawDuration, new Color(1, 0.5f, 0), position, 1);
                    }
                }
            }
        }

        private bool AreWorkcartsOverlapping(BaseTrain workcart, BaseTrain otherWorkcart, float proximityTolerance, out float distance)
        {
            distance = Vector3.Distance(workcart.transform.position, otherWorkcart.transform.position);
            return distance < proximityTolerance;
        }

        private List<BasePlayer> GetWorkcartPassengers(TrainEngine workcart)
        {
            if (!workcart.platformParentTrigger.HasAnyEntityContents)
                return null;

            var playerList = new List<BasePlayer>();

            foreach (var entity in workcart.platformParentTrigger.entityContents)
            {
                var player = entity.ToPlayer();
                if (player != null && !player.IsNpc && player.userID.IsSteamId())
                {
                    playerList.Add(player);
                }
            }

            return playerList;
        }

        private void CheckWorkcartTrigger(TrainEngine workcart, TriggerTrainCollisions trigger)
        {
            if (!trigger.HasAnyEntityContents)
                return;

            foreach (var content in trigger.entityContents)
            {
                var otherWorkcart = content as TrainEngine;
                if (otherWorkcart == null)
                    continue;

                float distance;
                if (AreWorkcartsOverlapping(workcart, otherWorkcart, 4, out distance))
                {
                    var workcartPosition = workcart.transform.position;

                    var sb = new StringBuilder();
                    sb.AppendLine($"Workcart emergency destroyed due to extreme proximity ({distance:f2}m) at {workcartPosition}.");

                    if (IsCargoTrain(workcart))
                    {
                        sb.AppendLine("Workcart A was controlled by the Cargo Train Event plugin");
                    }

                    if (IsCargoTrain(otherWorkcart))
                    {
                        sb.AppendLine("Workcart B was controlled by the Cargo Train Event plugin");
                    }

                    var playerA = workcart.GetMounted();
                    if (playerA != null && !playerA.IsNpc && playerA.userID.IsSteamId())
                    {
                        sb.AppendLine($"Workcart A driver: {playerA.displayName ?? "Unknown Name"} ({playerA.UserIDString})");
                    }

                    var playerB = otherWorkcart.GetMounted();
                    if (playerB != null && !playerB.IsNpc && playerB.userID.IsSteamId())
                    {
                        sb.AppendLine($"Workcart B driver: {playerB.displayName ?? "Unknown Name"} ({playerB.UserIDString})");
                    }

                    var passengersA = GetWorkcartPassengers(workcart);
                    if (passengersA != null)
                    {
                        foreach (var player in passengersA)
                        {
                            sb.AppendLine($"Workcart A passenger: {player.displayName ?? "Unknown Name"} ({player.UserIDString})");
                        }
                    }

                    var passengersB = GetWorkcartPassengers(otherWorkcart);
                    if (passengersB != null)
                    {
                        foreach (var player in passengersB)
                        {
                            sb.AppendLine($"Workcart B passenger: {player.displayName ?? "Unknown Name"} ({player.UserIDString})");
                        }
                    }

                    LogError(sb.ToString().Trim());

                    workcart.Invoke(() => DestroyWorkcart(workcart), 0);

                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        if (player.IsAdmin)
                        {
                            player.SendConsoleCommand("ddraw.text", DebugDrawDuration, Color.red, workcartPosition + Vector3.up, "Workcart emergency destroyed");
                            player.SendConsoleCommand("ddraw.sphere", DebugDrawDuration, Color.red, workcartPosition, 1);
                        }
                    }
                }
            }
        }

        private void CheckWorkcartTriggers(TrainEngine workcart)
        {
            if (workcart == null || workcart.IsDestroyed)
                return;

            CheckWorkcartTrigger(workcart, workcart.frontCollisionTrigger);
            CheckWorkcartTrigger(workcart, workcart.rearCollisionTrigger);
        }

        private bool IsOverlappingWorkart(TriggerTrainCollisions trigger, float proximityTolerance, out float distance)
        {
            distance = float.MaxValue;

            if (!trigger.HasAnyEntityContents)
                return false;

            foreach (var content in trigger.entityContents)
            {
                var otherWorkcart = content as TrainEngine;
                if (otherWorkcart == null)
                    continue;

                if (AreWorkcartsOverlapping(trigger.owner, otherWorkcart, proximityTolerance, out distance))
                {
                    return true;
                }
            }

            return false;
        }

        private void InitialProximityCheck(TrainEngine workcart, float maxProximity)
        {
            if (workcart == null || workcart.IsDestroyed)
                return;

            var frontDistance = float.MaxValue;
            var rearDistance = float.MaxValue;

            if (!IsOverlappingWorkart(workcart.frontCollisionTrigger, maxProximity, out frontDistance)
                && !IsOverlappingWorkart(workcart.rearCollisionTrigger, maxProximity, out rearDistance))
                return;

            var distance = Mathf.Min(frontDistance, rearDistance);
            var checkDuration = 10;
            var position = workcart.transform.position;

            LogWarning($"Workcarts unusually close ({distance:f2}m) at {position}. Monitoring for {checkDuration} seconds.");

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.IsAdmin)
                {
                    player.SendConsoleCommand("ddraw.text", DebugDrawDuration, Color.yellow, position + Vector3.up, $"Workcarts unusually close ({distance:f2}m)");
                    player.SendConsoleCommand("ddraw.sphere", DebugDrawDuration, Color.yellow, position, 1);
                }
            }

            timer.Repeat(1, checkDuration, () => CheckWorkcartTriggers(workcart));
        }

        #endregion

        #region Hooks

        private void OnEntityEnter(TriggerTrainCollisions trigger, TrainEngine workcartB)
        {
            if (trigger.entityContents != null && trigger.entityContents.Contains(workcartB))
                return;

            var workcartA = trigger.GetComponentInParent<TrainEngine>();
            if (workcartA == null)
                return;

            NextTick(() =>
            {
                CheckWorkcartSpeed(workcartA);
                CheckWorkcartSpeed(workcartB);
            });

            timer.Once(0.1f, () => InitialProximityCheck(workcartA, 6));
        }

        #endregion
    }
}
