using UnityEngine;
using TMPro;
using System.Collections;
public class ComputerPasswordManager : MonoBehaviour
{
    public string password = "1234";
    public string userInput = string.Empty;

    public TMP_Text onScreenPassword;
    bool isFlickering = false;

    private void Update()
    {
        if(userInput == string.Empty && !isFlickering)
        {
            StartCoroutine(nameof(FlickerComputer));
        }
    }

    private IEnumerator FlickerComputer()
    {
        onScreenPassword.text = "_ _ _";
        isFlickering = true;
        yield return new WaitForSeconds(1f);
        onScreenPassword.text = "_ _ _ _";
        yield return new WaitForSeconds(1f);
        isFlickering = false;
    }

    public bool Validate()
    {
        return password == userInput;
    }
}
