using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;
using System;

public class ComputerController : MonoBehaviour
{
    Renderer m_Renderer;
    
    [SerializeField] GameObject interactText;
    [SerializeField] Transform lookAt;
    [SerializeField] PlayerMovement playerMovement;

    bool insideRegion = false;
    bool inspecting = false;
    [SerializeField] InputActionReference buttonPress;
    [SerializeField] InputActionReference escapeButtonPress;

    private string password = "";

    private void OnEnable()
    {
        buttonPress.action.Enable();
        escapeButtonPress.action.Enable();
    }

    private void Start()
    {
        m_Renderer = GetComponent<Renderer>();
    }

    private void Update()
    {
        if(insideRegion)
        {
            if (m_Renderer.isVisible)
            {
                if(!inspecting && !interactText.activeSelf) interactText.SetActive(true);

                if(!inspecting && buttonPress.action.WasPressedThisFrame())
                {
                    InspectComputer();
                    interactText.SetActive(false);
                }

                if(inspecting)
                {
                    if(escapeButtonPress.action.WasPressedThisFrame())
                    {
                        Debug.Log("i did thuis");

                        playerMovement.Unlock();
                        interactText.SetActive(true);
                    }

                    EnterPassword();
                }
            }
        }
    }

    private void EnterPassword()
    {
        // Number keys 0-9
        for (int i = 0; i <= 9; i++)
        {
            var key = (Key)((int)Key.Digit0 + i);

            if (Keyboard.current[key].wasPressedThisFrame)
            {
                password += i.ToString();
                Debug.Log($"Pressed: {i} | Password: {password}");
            }
        }

        // Numpad keys 0-9
        for (int i = 0; i <= 9; i++)
        {
            var key = (Key)((int)Key.Numpad0 + i);

            if (Keyboard.current[key].wasPressedThisFrame)
            {
                password += i.ToString();
                Debug.Log($"Pressed (Numpad): {i} | Password: {password}");
            }
        }

        // Backspace/Delete
        if (Keyboard.current.backspaceKey.wasPressedThisFrame ||
            Keyboard.current.deleteKey.wasPressedThisFrame)
        {
            if (password.Length > 0)
            {
                password = password.Substring(0, password.Length - 1);
                Debug.Log($"Deleted | Password: {password}");
            }
        }
    }

    private void InspectComputer()
    {
        inspecting = true;
        playerMovement.LookAndLock(lookAt);
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            insideRegion = true;
        }
    }


    private void OnBecameInvisible()
    {
        interactText.SetActive(false);
    }

    private void OnTriggerExit(Collider other)
    {
        interactText.SetActive(false);
        insideRegion = false;
    }
}
