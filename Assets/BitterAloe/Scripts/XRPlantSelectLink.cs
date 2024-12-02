using Autohand.Demo;
using Autohand;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class XRPlantSelectLink : MonoBehaviour
{
    public PlantRaycastSearch hand;
    public XRNode role;
    public CommonButton button;

    bool selecting = false;
    InputDevice device;
    List<InputDevice> devices;

    void Start()
    {
        devices = new List<InputDevice>();
    }

    void FixedUpdate()
    {
        InputDevices.GetDevicesAtXRNode(role, devices);
        if (devices.Count > 0)
            device = devices[0];

        if (device != null && device.isValid)
        {
            //Sets hand fingers wrap
            if (device.TryGetFeatureValue(XRHandControllerLink.GetCommonButton(button), out bool teleportButton))
            {
                if (selecting && !teleportButton)
                {
                    hand.Select();
                    selecting = false;
                }
                else if (!selecting && teleportButton)
                {
                    hand.StartSelect();
                    selecting = true;
                }
            }
        }
    }
}
