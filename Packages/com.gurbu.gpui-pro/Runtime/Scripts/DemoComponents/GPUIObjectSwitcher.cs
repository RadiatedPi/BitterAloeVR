// GPU Instancer Pro
// Copyright (c) GurBu Technologies

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GPUInstancerPro
{
    public class GPUIObjectSwitcher : GPUIInputHandler
    {
        public List<GameObject> gameObjects;
        public Text activeGONameText;

        private GameObject _currentActiveGO;

        private void OnEnable()
        {
            gameObjects ??= new List<GameObject>();
            if (gameObjects.Count > 0 )
            {
                _currentActiveGO = gameObjects[0];
                if ( _currentActiveGO != null )
                {
                    _currentActiveGO.SetActive(true);
                    if (activeGONameText != null) activeGONameText.text = _currentActiveGO.name;
                }
            }
            for (int i = 1; i < gameObjects.Count; i++)
            {
                GameObject go = gameObjects[i];
                if (go == null) continue;
                go.SetActive(false);
            }
        }

        private void Update()
        {
            int count = gameObjects.Count;
            int index = -1;
            if (count > 0 && GetKey(KeyCode.Alpha1))
                index = 0;
            else if (count > 1 && GetKey(KeyCode.Alpha2))
                index = 1;
            else if (count > 2 && GetKey(KeyCode.Alpha3))
                index = 2;
            else if (count > 3 && GetKey(KeyCode.Alpha4))
                index = 3;
            else if (count > 4 && GetKey(KeyCode.Alpha5))
                index = 4;
            else if (count > 5 && GetKey(KeyCode.Alpha6))
                index = 5;
            else if (count > 6 && GetKey(KeyCode.Alpha7))
                index = 6;
            else if (count > 7 && GetKey(KeyCode.Alpha8))
                index = 7;
            else if (count > 8 && GetKey(KeyCode.Alpha9))
                index = 8;

            if (index >= 0)
            {
                if (_currentActiveGO != null)
                    _currentActiveGO.SetActive(false);
                _currentActiveGO = gameObjects[index];
                if (_currentActiveGO != null)
                {
                    _currentActiveGO.SetActive(true);
                    if (activeGONameText != null) activeGONameText.text = _currentActiveGO.name;
                }
            }
        }
    }
}
