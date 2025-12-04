using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NavKeypad
{
    public class KeypadInteractionFPV : MonoBehaviour
    {
        private Camera cam;

        private void Awake()
        {
            cam = Camera.main;
        }

        private void Update()
        {
            // Ray from center of the screen (crosshair)
            var ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            if (Input.GetMouseButtonDown(0))
            {
                // Get ALL hits along the ray, not just the first
                var hits = Physics.RaycastAll(ray, 10f); // 10f = max distance, tweak if needed

                if (hits.Length == 0)
                {
                    // Debug.Log("No hits");
                    return;
                }

                // Sort hits by distance so we can process nearest first
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

                foreach (var hit in hits)
                {
                    // Debug.Log("Hit: " + hit.collider.name);

                    // If this collider has a KeypadButton, use it and ignore everything else
                    if (hit.collider.TryGetComponent(out KeypadButton keypadButton))
                    {
                        Debug.Log("HIT BUTTON: " + hit.collider.name);
                        keypadButton.PressButton();
                        break; // stop after first valid keypad button
                    }

                    // Otherwise, keep looking through the rest of the hits
                }
            }
        }
    }
}
