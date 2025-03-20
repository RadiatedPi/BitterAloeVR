using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
// Include the demo namespace to access TeleportPoint.
using Autohand.Demo;

namespace Autohand {
    [DefaultExecutionOrder(10000)]
    public class Teleporter : MonoBehaviour {
        [Header("Teleport")]
        public GameObject teleportObject;
        public Transform[] additionalTeleports;

        [Header("Aim Settings")]
        public bool onlyUseTeleportPoints;
        public bool preventCapsuleOverlap = true;
        public Transform aimer;
        public float aimerSmoothingSpeed = 5f;
        public LayerMask layer;
        public float maxSurfaceAngle = 45f;
        [Min(0)]
        public float distanceMultiplyer = 1f;
        [Min(0)]
        public float curveStrength = 1f;

        [Header("Line Settings")]
        public LineRenderer line;
        public int lineSegments = 50;
        public Gradient canTeleportColor;
        public Gradient cantTeleportColor;

        [Tooltip("Shown at the aim-hit point for non-TeleportPoint surfaces")]
        public GameObject indicator;

        [Header("Unity Events")]
        public UnityEvent OnStartTeleport;
        public UnityEvent OnStopTeleport;
        public UnityEvent OnTeleport;

        // ---------------- Internal state ----------------
        private Vector3[] lineArr;
        private bool aiming;
        private bool hitting;   // True if we found a valid teleport target this frame
        private RaycastHit aimHit;
        private AutoHandPlayer playerBody;
        private RaycastHit[] hitNonAlloc;
        private TeleportPoint currentTeleportPoint;

        private Vector3 currentTeleportSmoothForward;
        private Vector3 currentTeleportForward;
        private Vector3 currentTeleportPosition;

        private TeleportPoint[] teleportPoints;
        private const float arcTimeDivisor = 60f;

        private void Awake() {
            line.enabled = false;
            hitNonAlloc = new RaycastHit[10];
        }

        private void Start() {
            playerBody = AutoHandExtensions.CanFindObjectOfType<AutoHandPlayer>();

            // If the "teleportObject" is actually the player's root, null it to avoid double-moving
            if(playerBody != null && playerBody.transform.gameObject == teleportObject) {
                teleportObject = null;
            }

            lineArr = new Vector3[lineSegments];

            // Grab all TeleportPoints in scene, disable them initially
            teleportPoints = AutoHandExtensions.CanFindObjectsOfType<TeleportPoint>();
            ToggleTeleportPoints(false);
        }

        void ToggleTeleportPoints(bool enabled) {
            if(teleportPoints == null)
                return;

            foreach(var tp in teleportPoints) {
                // If TeleportPoint has "alwaysShow" = true, only disable if enabling == false
                if(!tp.alwaysShow || enabled) {
                    tp.gameObject.SetActive(enabled);
                }
            }
        }

        private void LateUpdate() {
            SmoothTargetValues();

            if(aiming) {
                CalculateTeleportPoint();
            }
            else {
                // Turn off line if not aiming
                line.positionCount = 0;
            }

            DrawIndicator();
        }

        void SmoothTargetValues() {
            currentTeleportForward = aimer.forward;
            currentTeleportPosition = aimer.position;
            currentTeleportSmoothForward = Vector3.Lerp(
                currentTeleportSmoothForward,
                currentTeleportForward,
                Time.deltaTime * aimerSmoothingSpeed
            );
        }

        /// <summary>
        /// Core arc logic + collisions checking for a valid teleport point.
        /// </summary>
        void CalculateTeleportPoint() {
            hitting = false;
            line.colorGradient = cantTeleportColor;



            var lineList = new List<Vector3>();
            TeleportPoint foundTP = null;
            bool foundTeleport = false;

            for(int i = 0; i < lineSegments; i++) {
                float time = i / arcTimeDivisor;

                // Parabolic arc
                lineArr[i] = currentTeleportPosition
                    + (currentTeleportSmoothForward * time * distanceMultiplyer * 15f)
                    + Vector3.up * (curveStrength * (time - Mathf.Pow(9.8f * 0.5f * time, 2)));

                lineList.Add(lineArr[i]);

                // Skip first segment, no line to cast
                if(i == 0)
                    continue;

                Vector3 segmentStart = lineArr[i - 1];
                Vector3 segmentDir = lineArr[i] - segmentStart;
                float segmentDist = segmentDir.magnitude;

                int hitCount = Physics.RaycastNonAlloc(
                    segmentStart,
                    segmentDir.normalized,
                    hitNonAlloc,
                    segmentDist,
                    layer,
                    QueryTriggerInteraction.Collide
                );

                if(hitCount > 0) {
                    bool teleporterHit = false;
                    bool solidHitFound = false;
                    RaycastHit bestSolidHit = default;

                    // Examine each hit in the segment
                    for(int h = 0; h < hitCount; h++) {
                        var tempHit = hitNonAlloc[h];

                        // If it's a TeleportPoint (trigger or not)
                        if(tempHit.collider.TryGetComponent<TeleportPoint>(out TeleportPoint tp)) {
                            foundTP = tp;
                            aimHit = tempHit;
                            teleporterHit = true;
                            // We choose the earliest TeleportPoint in the path, so break
                            break;
                        }
                        else if(!tempHit.collider.isTrigger) {
                            // This is an actual physical collider.
                            float angle = Vector3.Angle(tempHit.normal, Vector3.up);
                            if(angle <= maxSurfaceAngle) {
                                // The first valid flat surface we see on this segment
                                if(!solidHitFound) {
                                    solidHitFound = true;
                                    bestSolidHit = tempHit;
                                }
                            }
                            else {
                                // It's too steep => discard and break out
                                break;
                            }
                        }
                    }

                    // If we got either a teleporter or a valid solid, do overlap checks if needed
                    bool capsuleOverlapInvalid = false;
                    if(playerBody != null && preventCapsuleOverlap) {
                        bool arcValid = (teleporterHit || solidHitFound);

                        // If the user only wants TeleportPoints, disallow solids
                        if(!teleporterHit && onlyUseTeleportPoints)
                            arcValid = false;

                        if(arcValid) {
                            // The actual point we aim to stand on:
                            Vector3 checkPoint;
                            if(teleporterHit) {
                                checkPoint = foundTP.teleportPoint.position;
                            }
                            else {
                                checkPoint = bestSolidHit.point;
                            }

                            CapsuleCollider playerCapsule = playerBody.bodyCollider;
                            Vector3 capsuleBottom = checkPoint + Vector3.up * (playerCapsule.radius) + Vector3.up * 0.15f;
                            Vector3 capsuleTop = checkPoint + Vector3.up * (playerCapsule.height - playerCapsule.radius);

                            Collider[] overlaps = Physics.OverlapCapsule(
                                capsuleBottom,
                                capsuleTop,
                                playerCapsule.radius,
                                playerBody.handPlayerMask,
                                QueryTriggerInteraction.Ignore
                            );

                            foreach(Collider col in overlaps) {
                                if(col.gameObject == playerBody.gameObject)
                                    continue;

                                capsuleOverlapInvalid = true;
                                Debug.Log("Capsule overlap detected on: " + col.name);
                                break;
                            }

                            // Debug line to visualize overlap check
                            Debug.DrawLine(capsuleBottom, capsuleTop, capsuleOverlapInvalid ? Color.red : Color.green);
                        }
                    }

                    // If overlap is invalid, we end the arc here as "no"
                    if(capsuleOverlapInvalid) {
                        hitting = false;
                        line.colorGradient = cantTeleportColor;

                        // End line here for clarity
                        lineList[lineList.Count - 1] = lineArr[i];

                        // If we had a highlighted TeleportPoint from last frame, clear it now
                        if(currentTeleportPoint != null) {
                            currentTeleportPoint.StopHighlighting(this);
                            currentTeleportPoint = null;
                        }

                        break;
                    }
                    else {
                        // Overlap is fine => use teleporter or solid
                        if(teleporterHit) {
                            hitting = true;
                            if(currentTeleportPoint == null || currentTeleportPoint != foundTP)
                                foundTP.StartHighlighting(this);

                            currentTeleportPoint = foundTP;

                            // End line right at the TeleportPoint's collision point
                            lineList[lineList.Count - 1] = aimHit.point;
                            line.colorGradient = canTeleportColor;
                            break;
                        }
                        else if(solidHitFound) {
                            // Non-teleport surface, but valid
                            hitting = true;
                            aimHit = bestSolidHit;

                            // If we had an old teleport point, stop highlighting
                            if(currentTeleportPoint != null) {
                                currentTeleportPoint.StopHighlighting(this);
                                currentTeleportPoint = null;
                            }

                            lineList[lineList.Count - 1] = aimHit.point;
                            line.colorGradient = canTeleportColor;
                            foundTeleport = true;
                            break;
                        }
                        else {
                            foundTeleport = true;
                            break;
                        }
                    }
                }
            }

            // Final update to line renderer
            line.enabled = true;
            line.positionCount = lineList.Count;
            line.SetPositions(lineList.ToArray());

            // If we never found anything valid, be sure to clear any leftover highlight
            if(!hitting && currentTeleportPoint != null) {
                currentTeleportPoint.StopHighlighting(this);
                currentTeleportPoint = null;
            }
        }

        /// <summary>
        /// Places the indicator where we hit (if hitting) and if not a TeleportPoint.
        /// </summary>
        void DrawIndicator() {
            if(indicator == null)
                return;

            if(hitting) {
                if(currentTeleportPoint == null && onlyUseTeleportPoints) {
                    indicator.SetActive(false);
                    line.colorGradient = cantTeleportColor;
                }
                else if(currentTeleportPoint != null) {
                    indicator.SetActive(false);
                }
                else {
                    // Show our generic ground indicator
                    indicator.SetActive(true);
                    indicator.transform.position = aimHit.point;
                    indicator.transform.up = aimHit.normal;
                }
            }
            else {
                indicator.SetActive(false);
            }
        }

        public void StartTeleport() {
            aiming = true;
            ToggleTeleportPoints(true);
            line.enabled = true;
            OnStartTeleport?.Invoke();
        }

        public void CancelTeleport() {
            aiming = false;
            hitting = false;
            line.positionCount = 0;
            line.enabled = false;
            ToggleTeleportPoints(false);

            if(currentTeleportPoint != null) {
                currentTeleportPoint.StopHighlighting(this);
                currentTeleportPoint = null;
            }
            indicator?.SetActive(false);

            OnStopTeleport?.Invoke();
        }

        /// <summary>
        /// Actually perform the teleport, either to a TeleportPoint or a generic location.
        /// </summary>
        public void Teleport() {
            // If we are on a TeleportPoint:
            if(currentTeleportPoint != null) {
                Vector3 finalPos = currentTeleportPoint.matchPoint
                    ? currentTeleportPoint.teleportPoint.position
                    : aimHit.point;

                Quaternion finalRot = currentTeleportPoint.matchDirection
                    ? currentTeleportPoint.teleportPoint.rotation
                    : playerBody ? playerBody.headCamera.transform.rotation : Quaternion.identity;

                // Move the designated object
                if(teleportObject != null) {
                    Vector3 offset = finalPos - teleportObject.transform.position;
                    teleportObject.transform.position = finalPos;
                    foreach(var extra in additionalTeleports) {
                        extra.position += offset;
                    }
                }
                // Move the player body
                if(playerBody != null) {
                    playerBody.SetPosition(finalPos, finalRot);
                }

                // Invoke the TeleportPoint’s own teleport logic
                currentTeleportPoint.Teleport(this);
                OnTeleport?.Invoke();
            }
            else if(!onlyUseTeleportPoints && hitting) {
                // Solid ground (non‐teleport) is allowed, and we found a valid aimHit
                Vector3 finalPos = aimHit.point;

                if(teleportObject != null) {
                    Vector3 offset = finalPos - teleportObject.transform.position;
                    teleportObject.transform.position = finalPos;
                    foreach(var extra in additionalTeleports) {
                        extra.position += offset;
                    }
                }
                if(playerBody != null) {
                    // Just rotate so player faces the same direction, or up to you
                    playerBody.SetPosition(finalPos);
                }
                OnTeleport?.Invoke();
            }

            CancelTeleport();
        }
    }
}
