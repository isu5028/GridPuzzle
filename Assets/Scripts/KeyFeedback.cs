using UnityEngine;
using TMPro;
using Cysharp.Threading.Tasks;
using System.Threading;
using System;

public class KeyFeedback : MonoBehaviour
{
    [SerializeField] private SpriteRenderer[] keyTexts;
    [SerializeField] private KeyCode[] keyCodes;

    void Start()
    {

    }

    void Update()
    {
        for (int i = 0; i < keyTexts.Length; i++)
        {
            if (Input.GetKeyDown(keyCodes[i]))
            {
                Show(keyCodes[i]);
            }
            else if (Input.GetKeyUp(keyCodes[i]))
            {
                Hide(keyCodes[i]);
            }
        }
    }

    public void Show(KeyCode keycode)
    {
        int index = Array.IndexOf(keyCodes, keycode);
        if (index == -1) return;
        keyTexts[index].color = Utility.mainColors[1];
    }

    public void Hide(KeyCode keycode)
    {
        int index = Array.IndexOf(keyCodes, keycode);
        if (index == -1) return;
        keyTexts[index].color = Utility.mainColors[0];
    }
}
