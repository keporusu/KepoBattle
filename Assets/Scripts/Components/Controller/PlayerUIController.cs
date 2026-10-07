using System;
using TMPro;
using UnityEngine;

public class PlayerUIController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI debugText;

    public void ShowUpdateDebugText(string text)
    {
        debugText.text = text;
    }
}
