using System;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine.Localization.Components;

public class OptionLoadView : MonoBehaviour
{
    [SerializeField] private Transform[] itemPositions = null!;
    [SerializeField] private Transform cursorObj = null!;
    [SerializeField] private TextMeshPro autoSaveText;
    [SerializeField] private TextMeshPro manualSaveText;
    [SerializeField] private TextMeshPro maskedAutoSaveText;
    [SerializeField] private TextMeshPro maskedManualSaveText;

    [SerializeField] private Transform optionLoadPanel = null!;
    [SerializeField] private float slideDuration = 0.3f;

    private Vector3 hiddenPosition;
    private Vector3 visiblePosition;
    private bool isAnimating = false;
    public bool IsAnimating => isAnimating;

    public void SetSaveData(string autoSaveDate, string manualSaveDate)
    {
        autoSaveText.text = autoSaveDate;
        manualSaveText.text = manualSaveDate;
        maskedAutoSaveText.text = autoSaveDate;
        maskedManualSaveText.text = manualSaveDate;
    }

    public void UpdateCursorPosition(int index)
    {
        Debug.Log("Index " + index);
        if (index >= 0 && index < itemPositions.Length)
        {
            cursorObj.position = itemPositions[index].position;
        }
    }

    private Vector3 hiddenUpPosition;

    void Start()
    {
        if (optionLoadPanel != null)
        {
            visiblePosition = new Vector3(0, 0, optionLoadPanel.localPosition.z);
            hiddenPosition = new Vector3(visiblePosition.x, visiblePosition.y - Screen.height, visiblePosition.z);
            hiddenUpPosition = new Vector3(visiblePosition.x, visiblePosition.y + Screen.height, visiblePosition.z);
            optionLoadPanel.localPosition = hiddenPosition;
        }
    }

    public async UniTask ShowAsync()
    {
        if (isAnimating) return;

        isAnimating = true;
        optionLoadPanel.DOLocalMove(visiblePosition, slideDuration).SetUpdate(true).SetEase(Ease.OutCubic);
        await UniTask.Delay(TimeSpan.FromSeconds(slideDuration));
        isAnimating = false;
    }

    public async UniTask HideAsync()
    {
        if (isAnimating) return;

        isAnimating = true;
        optionLoadPanel.DOLocalMove(hiddenPosition, slideDuration).SetUpdate(true).SetEase(Ease.InCubic);
        await UniTask.Delay(TimeSpan.FromSeconds(slideDuration));
        isAnimating = false;
    }

    public async UniTask HideUpAsync()
    {
        if (isAnimating) return;

        isAnimating = true;
        optionLoadPanel.DOLocalMove(hiddenUpPosition, slideDuration).SetUpdate(true).SetEase(Ease.InCubic);
        await UniTask.Delay(TimeSpan.FromSeconds(slideDuration));
        isAnimating = false;
    }
}
