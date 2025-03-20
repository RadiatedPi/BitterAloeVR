//CREDIT: Silk from the AutoHand discord server for the original script

using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class HandColliderEditor : EditorWindow {
    private GameObject selectedHand;
    private PhysicsMaterial replacementMaterial;

    [MenuItem("/Window/Autohand/Hand Collider Editor")]
    public static void ShowWindow() => GetWindow<HandColliderEditor>("Hand Collider Editor");

    void OnGUI() {
        GUI.color = Color.white;
        selectedHand = (GameObject)EditorGUILayout.ObjectField("Hand Root", selectedHand, typeof(GameObject), true);
        if(selectedHand == null) {
            GUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox("Please select a valid hand root object, this must be the root object of your hand prefab, not the root bone transform.", MessageType.None);
            GUILayout.EndHorizontal();
        }
        GUILayout.Space(2.5f);
        GUILayout.Box("", GUILayout.Height(10), GUILayout.ExpandWidth(true));
        GUILayout.Space(2.5f);
        GUILayout.BeginHorizontal();
        if(GUILayout.Button("Generate Colliders") && selectedHand != null) GenerateColliders(selectedHand);
        if(GUILayout.Button("Destroy Colliders") && selectedHand != null) DestroyColliders(selectedHand);
        GUILayout.EndHorizontal();
        GUILayout.Space(2.5f);
        GUILayout.Box("", GUILayout.Height(10), GUILayout.ExpandWidth(true));
    }

    void DestroyColliders(GameObject obj) {
        foreach(Collider collider in obj.GetComponentsInChildren<Collider>())
            DestroyImmediate(collider.gameObject, true);
    }

    void GenerateColliders(GameObject handRoot) {
        SkinnedMeshRenderer skinnedMesh = handRoot.GetComponentInChildren<SkinnedMeshRenderer>();
        if(skinnedMesh == null) {
            Debug.LogError("No SkinnedMeshRenderer found in the hand object.");
            return;
        }

        Mesh bakedMesh = new Mesh();
        skinnedMesh.BakeMesh(bakedMesh);
        Transform[] bones = skinnedMesh.bones;
        Dictionary<Transform, List<Vector3>> boneVertexMap = new Dictionary<Transform, List<Vector3>>();

        for(int i = 0; i < bakedMesh.vertexCount; i++) {
            Vector3 worldVertex = skinnedMesh.transform.TransformPoint(bakedMesh.vertices[i]);
            BoneWeight weight = skinnedMesh.sharedMesh.boneWeights[i];
            Transform bone = bones[weight.boneIndex0];
            if(!boneVertexMap.ContainsKey(bone)) boneVertexMap[bone] = new List<Vector3>();
            boneVertexMap[bone].Add(worldVertex);
        }

        foreach(var pair in boneVertexMap) {
            Transform bone = pair.Key;
            List<Vector3> vertices = pair.Value;
            if(vertices.Count == 0) continue;
            Vector3 center = Vector3.zero;

            foreach(var v in vertices) 
                center += v;

            center /= vertices.Count;
            Vector3 min = center, max = center;

            foreach(var v in vertices) {
                min = Vector3.Min(min, v);
                max = Vector3.Max(max, v);
            }

            Vector3 size = max - min;
            GameObject colliderHolder = new GameObject("ColliderHolder");
            colliderHolder.transform.SetParent(bone, false);
            colliderHolder.transform.localPosition = Vector3.zero;
            colliderHolder.transform.rotation = bone.rotation;

            var localRotation = colliderHolder.transform.localRotation;
            localRotation.z = bone.localRotation.z;
            colliderHolder.transform.localScale = Vector3.one;

            Vector3 localCenter = colliderHolder.transform.InverseTransformPoint(center);
            localCenter.x = 0;
            localCenter.z = 0;

            if(size.x > size.y || size.x > 0.05f) {
                Vector3 appendedSize = new Vector3(size.x * 1.2f, size.y * 1.3f, size.z);
                BoxCollider box = colliderHolder.AddComponent<BoxCollider>();
                box.gameObject.transform.rotation = Quaternion.identity;
                box.center = localCenter;
                box.size = appendedSize / 1.5f;
            }
            else {
                CapsuleCollider capsule = colliderHolder.AddComponent<CapsuleCollider>();
                capsule.center = localCenter;
                capsule.height = size.y * 1.15f;
                capsule.radius = Mathf.Max(size.x, size.z) / 2.5f;
                capsule.direction = 1;
            }
        }
    }
}

