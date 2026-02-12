using System.Collections.Generic;
using UnityEngine;

public interface ISaveSystem
{
    void SaveGame(GameProgressManager.SaveType saveType);
    void ContinueGame(GameProgressManager.SaveType saveType);
    void ResetAndStartNewGame();
    void LoadStageWithCustomStart(StageSO stage, Vector2Int startPos, Direction dir, bool resetEnemy = false);
    void DeleteSaveFile(GameProgressManager.SaveType saveType);
}
