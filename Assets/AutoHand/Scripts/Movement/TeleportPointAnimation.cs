using Autohand;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Autohand.Demo {
    [RequireComponent(typeof(TeleportPoint))]
    public class TeleportPointAnimation : InteractionAnimations {
        TeleportPoint teleportPoint;

        protected override void OnEnable() {
            base.OnEnable();
            teleportPoint = GetComponent<TeleportPoint>();
            teleportPoint.StartHighlight.AddListener(StartHighlight);
            teleportPoint.StopHighlight.AddListener(StopHighlight);
        }

        protected override void OnDisable() {
            base.OnDisable();
            teleportPoint.StartHighlight.RemoveListener(StartHighlight);
            teleportPoint.StopHighlight.RemoveListener(StopHighlight);
        }

        void StartHighlight(TeleportPoint teleportPoint, Teleporter teleporter) {
            highlightStartTime = Time.time;
            highlighting = true;
        }

        void StopHighlight(TeleportPoint teleportPoint, Teleporter teleporter) {
            highlightStopTime = Time.time;
            highlighting = false;
        }

    }
}
