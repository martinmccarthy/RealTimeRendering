using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class ComputerController : MonoBehaviour
{
    Renderer m_Renderer;
    
    [SerializeField] GameObject interactText;
    [SerializeField] Transform lookAt;
    [SerializeField] PlayerMovement playerMovement;

    bool insideRegion = false;
    InputActionReference buttonPress;


    private void OnEnable()
    {
        buttonPress.action.Enable();
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
                if(!interactText.activeSelf) interactText.SetActive(true);

                if(buttonPress.action.WasPerformedThisFrame())
                {
                    InspectComputer();
                }
            }
        }
    }

    private void InspectComputer()
    {
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
