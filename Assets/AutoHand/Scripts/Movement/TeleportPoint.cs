using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Autohand.Demo {

    public class TeleportPoint : MonoBehaviour {
        public Transform teleportPoint;
        public bool alwaysShow = false;
        public bool matchPoint = true;
        public bool matchDirection = true;

        public UnityEvent<TeleportPoint, Teleporter> StartHighlight;
        public UnityEvent<TeleportPoint, Teleporter> StopHighlight;
        public UnityEvent<TeleportPoint, Teleporter> OnTeleport;


        public void Awake() {
            if(teleportPoint == null)
                teleportPoint = transform;
        }


        public virtual void StartHighlighting(Teleporter raycaster) {
            StartHighlight.Invoke(this, raycaster);
        }

        public virtual void StopHighlighting(Teleporter raycaster) {
            StopHighlight.Invoke(this, raycaster);
        }

        public virtual void Teleport(Teleporter raycaster) {
            OnTeleport.Invoke(this, raycaster);
        }
    }
}
