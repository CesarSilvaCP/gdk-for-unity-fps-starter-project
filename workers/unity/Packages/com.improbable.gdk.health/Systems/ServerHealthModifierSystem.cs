using Improbable.Gdk.Core;
using Improbable.Gdk.Guns;
using Improbable.Gdk.Health;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Improbable.Gdk.Health
{
    [UpdateInGroup(typeof(SpatialOSUpdateGroup))]
    public class ServerHealthModifierSystem : ComponentSystem
    {
        private struct EntitiesWithModifiedHealth
        {
            public readonly int Length;
            [ReadOnly] public EntityArray Entity;
            public ComponentDataArray<HealthComponent.Component> Health;
            [ReadOnly] public ComponentDataArray<HealthComponent.CommandRequests.ModifyHealth> ModifyHealthRequests;
            public ComponentDataArray<HealthComponent.EventSender.HealthModified> HealthModifiedEventSenders;
            public ComponentDataArray<ScoreComponent.CommandSenders.AddScore> ScoreSender;
            public ComponentDataArray<ScoreComponent.Component> ScoreComponent;
            [ReadOnly] public ComponentDataArray<Authoritative<ScoreComponent.Component>> DenotesAuthority;
        }

        [Inject] private EntitiesWithModifiedHealth entitiesWithModifiedHealth;

        protected override void OnUpdate()
        {
            for (var i = 0; i < entitiesWithModifiedHealth.Length; i++)
            {
                var health = entitiesWithModifiedHealth.Health[i];
                var healthModifiedEventSender = entitiesWithModifiedHealth.HealthModifiedEventSenders[i];

                if (health.Health <= 0)
                {
                    continue;
                }

                foreach (var request in entitiesWithModifiedHealth.ModifyHealthRequests[i].Requests)
                {
                    var modifier = request.Payload;

                    var healthModifiedInfo = new HealthModifiedInfo
                    {
                        Modifier = modifier,
                        HealthBefore = health.Health
                    };

                    health.Health = Mathf.Clamp(health.Health + modifier.Amount, 0, health.MaxHealth);
                    healthModifiedInfo.HealthAfter = health.Health;

                    if (health.Health <= 0)
                    {
                        healthModifiedInfo.Died = true;
                        healthModifiedEventSender.Events.Add(healthModifiedInfo);

                        var scoreComponent = entitiesWithModifiedHealth.ScoreComponent[i];
                        scoreComponent.Deaths++;
                        entitiesWithModifiedHealth.ScoreComponent[i] = scoreComponent;

                        if (modifier.Shooter.HasValue)
                        {
                            entitiesWithModifiedHealth.ScoreSender[i].RequestsToSend.Add(
                                ScoreComponent.AddScore.CreateRequest(
                                    modifier.Shooter.Value,
                                    new AddScoreRequest()));
                        }

                        break;
                    }

                    healthModifiedEventSender.Events.Add(healthModifiedInfo);
                }

                entitiesWithModifiedHealth.Health[i] = health;
            }
        }
    }
}
