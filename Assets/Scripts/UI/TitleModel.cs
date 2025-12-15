using UnityEngine;

public class TitleModel
{
    public enum TitleItem { Start, Option, Quit }
    public TitleItem CurrentItem { get; private set; } = TitleItem.Start;

    public bool HasSaveData()
    {
        return GameProgressManager.Instance.GetSaveData(GameProgressManager.SaveType.Auto) != null ||
               GameProgressManager.Instance.GetSaveData(GameProgressManager.SaveType.Manual) != null;
    }

    public void MoveSelection(int direction)
    {
        int current = (int)CurrentItem;
        int count = System.Enum.GetNames(typeof(TitleItem)).Length;

#if UNITY_WEBGL
        count--; // Quit is the last item, so reduce count to ignore it
#endif

        current = (current + direction + count) % count;
        CurrentItem = (TitleItem)current;
    }
}
