using UnityEngine;
using TMPro;
using DG.Tweening;
using Cysharp.Threading.Tasks;
using UnityEngine.Localization.Components;

public class TitleView : MonoBehaviour
{
    [SerializeField] private TextMeshPro startText; // "NEW GAME" or "CONTINUE"
    [SerializeField] private TextMeshPro maskedStartText;
    [SerializeField] private Transform[] itemPositions;
    [SerializeField] private Transform cursor;
    [SerializeField] private float slideDuration = 0.3f;
    [SerializeField] private Transform menuContainer;

    private Vector3 visiblePos;
    private Vector3 hiddenPos;

    private bool isInitialized = false;

    public void Initialize()
    {
        if (isInitialized) return;

         if (menuContainer != null)
         {
             visiblePos = menuContainer.localPosition;
             // OptionLoadView comes from bottom. Title could hide somewhere.
             // Let's assume hiding means moving out of screen.
             // If Load View comes up, Title might fade or move Up.
             hiddenPos = visiblePos + new Vector3(0, Screen.height, 0);
         }

#if UNITY_WEBGL
        if (itemPositions != null && itemPositions.Length > 2)
        {
            // Disable Quit button visual
            if(itemPositions[2] != null) itemPositions[2].gameObject.SetActive(false);
        }
#endif

         isInitialized = true;
    }

    public void UpdateStartText(bool hasSaveData)
    {
        string key = hasSaveData ? "TITLE_CONTINUE" : "TITLE_NEW_GAME";
        if (startText != null)
            SetLocalizedText(startText, "StringTable", key);
        if (maskedStartText != null)
            SetLocalizedText(maskedStartText, "StringTable", key);
    }

    public void UpdateCursor(int index)
    {
         if (index >= 0 && index < itemPositions.Length && cursor != null)
         {
             cursor.position = itemPositions[index].position;
         }
    }

    public async UniTask ShowAsync()
    {
         Initialize();
         if (menuContainer != null)
         {
            //menuContainer.DOLocalMove(visiblePos, slideDuration).SetEase(Ease.OutCubic);
         }
         //await UniTask.Delay(System.TimeSpan.FromSeconds(slideDuration));
    }

    public async UniTask HideAsync()
    {
         Initialize();
         if (menuContainer != null)
         {
            //menuContainer.DOLocalMove(hiddenPos, slideDuration).SetEase(Ease.InCubic);
         }
         //await UniTask.Delay(System.TimeSpan.FromSeconds(slideDuration));
    }
    private void SetLocalizedText(TextMeshPro textComp, string table, string key)
    {
        if (textComp.TryGetComponent<LocalizeStringEvent>(out var locEvent))
        {
            locEvent.StringReference.SetReference(table, key);
        }
        else
        {
            var text = UnityEngine.Localization.Settings.LocalizationSettings.StringDatabase.GetLocalizedString(table, key);
            textComp.text = text;
        }
    }
}
