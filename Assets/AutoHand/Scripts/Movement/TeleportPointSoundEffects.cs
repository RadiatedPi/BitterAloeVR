using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Autohand.Demo {
    public class TeleportPointSoundEffects : MonoBehaviour {
        public TeleportPoint teleportPoint;
        public float randomPitchRange = 0.1f;
        public AudioSource audioSource;
        public AudioClip startHighlightSound;
        public AudioClip stopHighlightSound;
        public AudioClip teleportSound;

        float startPitch;

        private void OnEnable() {
            if(audioSource == null)
                audioSource = GetComponent<AudioSource>();
            if(teleportPoint == null)
                teleportPoint = GetComponent<TeleportPoint>();

            teleportPoint.StartHighlight.AddListener(OnStartHighlight);
            teleportPoint.StopHighlight.AddListener(OnStopHighlight);
            teleportPoint.OnTeleport.AddListener(OnTeleport);
            startPitch = audioSource.pitch;
        }

        private void OnDisable() {
            teleportPoint.StartHighlight.RemoveListener(OnStartHighlight);
            teleportPoint.StopHighlight.RemoveListener(OnStopHighlight);
            teleportPoint.OnTeleport.RemoveListener(OnTeleport);
        }

        void OnStartHighlight(TeleportPoint point, Teleporter raycaster) {
            if(audioSource != null && startHighlightSound != null) {
                audioSource.pitch = startPitch + Random.Range(-randomPitchRange, randomPitchRange);
                audioSource.PlayOneShot(startHighlightSound);
            }
        }

        void OnStopHighlight(TeleportPoint point, Teleporter raycaster) {
            if(audioSource != null && stopHighlightSound != null) {
                audioSource.pitch = startPitch + Random.Range(-randomPitchRange, randomPitchRange);
                audioSource.PlayOneShot(stopHighlightSound);
            }
        }

        void OnTeleport(TeleportPoint point, Teleporter raycaster) {
            if(audioSource != null && teleportSound != null) {
                audioSource.pitch = startPitch + Random.Range(-randomPitchRange, randomPitchRange);
                audioSource.PlayOneShot(teleportSound);
            }
        }


    }
}