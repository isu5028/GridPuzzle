using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;

public class EnemyAIRush : EnemyAI
{
    public enum RushState
    {
        Idle,
        Charging, // Found player, preparing to rush (1 turn wait)
    }

    private RushState currentState = RushState.Idle;
    private Direction chargeDirection;
    private int rushDamage = 1;

    // AI State Persistence
    // Pack State (2 bits) and Direction (2 bits, shifted by 2)
    public override int GetState()
    {
        return (int)currentState | ((int)chargeDirection << 2);
    }

    public override void SetState(int state)
    {
        currentState = (RushState)(state & 0x3); // First 2 bits
        chargeDirection = (Direction)((state >> 2) & 0x3); // Next 2 bits

        // Visual Sync: Ensure enemy faces the charge direction if Charging
        if (currentState == RushState.Charging)
        {
            var enemy = GetComponent<Enemy>();
            if (enemy != null)
            {
                enemy.SetDirection(chargeDirection);
            }
        }
    }

    public override async UniTask ExecuteAI(Enemy enemy)
    {
        if (currentState == RushState.Charging)
        {
            // Execute Rush
            await ExecuteRush(enemy, chargeDirection);

            // Use Command for State Change to support Undo
            commandManager.ExecuteCommand(new SetRushStateCommand(this, (int)RushState.Idle));
            return;
        }

        // Idle: Look for player to charge
        Unit player = gridManager.PlayerScript;
        if (player == null) return;

        Vector2Int diff = player.CurrentPos - enemy.CurrentPos;
        bool isAligned = (diff.x == 0 || diff.y == 0);

        if (isAligned)
        {
            Direction dir = GetDirectionTo(enemy.CurrentPos, player.CurrentPos);
            // Face target
            if (enemy.CurrentDirection != dir)
            {
                enemy.SetDirection(dir);
            }
            // Enter Charge State using Command
            chargeDirection = dir;

            // Pack State and Direction for Command
            int targetState = (int)RushState.Charging | ((int)dir << 2);
            commandManager.ExecuteCommand(new SetRushStateCommand(this, targetState));

            // Visual feedback
            enemy.OrderSpriteAnimation.PlayAnimation();

            // Wait time for Charge initiation
            await UniTask.Delay(200);
        }
        else
        {
            // Just idle or random turn?
            // "Rotate if needed? or just stay?" - usually random patrol or static.
            // Let's keep it static for now as requested.
            if(currentState != RushState.Idle)
            {
                 commandManager.ExecuteCommand(new SetRushStateCommand(this, (int)RushState.Idle));
            }
        }
    }

    // Recursive rush logic (simulates continuous movement)
    private async UniTask ExecuteRush(Enemy enemy, Direction dir)
    {
        bool isMoving = true;

        if (enemy.gameObject.activeInHierarchy)
        {
             // Small Shake at start of rush
             CameraManager.Instance.ShakeCamera(0.5f, 2.0f, 60f, Utility.GetVectorFromDirection(dir));
        }

        while (isMoving)
        {
            Vector2Int currentPos = enemy.CurrentPos;
            Vector2Int nextPos = currentPos + Utility.GetVectorFromDirection(dir);
            StageSO currentStage = gridManager.CurrentStageSO; // Note: if enemy moves off-screen, it's removed from unitManager, but "enemy" obj still exists in memory?

            // We need a helper to check collision on ANY stage.
            StageSO enemyStage = GetEnemyStage(enemy);
            if (enemyStage == null) break; // Should not happen

            // Check what is at nextPos

            // 1. Unit Check
            Unit targetUnit = GetUnitAt(enemyStage, nextPos);
            if (targetUnit != null)
            {
                // Hit Unit
                bool bothActive = enemy.gameObject.activeInHierarchy && targetUnit.gameObject.activeInHierarchy;

                if (bothActive)
                {
                   if (targetUnit.IsBusy) await UniTask.WaitUntil(() => !targetUnit.IsBusy);

                   commandManager.ExecuteCommand(new RushAttackCommand(enemy, targetUnit.CurrentPos, gridManager));
                   CameraManager.Instance.ShakeCamera(0.3f, 15.0f, 120f, Utility.GetVectorFromDirection(dir));

                   await UniTask.WaitUntil(() => !enemy.IsMoving && (targetUnit == null || !targetUnit.IsBusy));
                   await UniTask.Delay(100);
                }
                else
                {
                    commandManager.ExecuteCommand(new CrossStageAttackCommand(enemy, targetUnit, rushDamage));
                }

                isMoving = false;
                break;
            }

            // 2. Walkable Check (Floor or Broken Wall)
            // gridManager.IsWalkableAt handles checking DestroyedWalls for In-Bounds AND OOB coordinates
            if (gridManager.IsWalkableAt(enemyStage, nextPos))
            {
                MoveCommand moveCmd = new MoveCommand(enemy, currentPos, nextPos, dir, dir, false);
                commandManager.ExecuteCommand(moveCmd);

                if (enemy.gameObject.activeInHierarchy)
                {
                    if(!commandManager.IsUndoing) AudioManager.Instance.PlaySE(AudioManager.SEType.rush);
                    await UniTask.Delay(50);
                }
                else
                {
                    await UniTask.Yield();
                }

                // 階段遷移チェックは行わない（突進中は階段を無視して通過する）
                /*
                if (enemy.gameObject.activeInHierarchy)
                {
                    var transition = gridManager.GetEnemyStairDestination(enemy, nextPos, dir);
                    // ...
                }
                */

                // 境界遷移チェック（Rush継続のため）
                if (gridManager.CheckAndPerformEnemyBoundaryTransfer(enemy))
                {
                    // 遷移先の着地位置が壁マスなら壁を破壊せずに即死
                    StageSO landedStage = GetEnemyStage(enemy);
                    if (landedStage != null)
                    {
                        CellType landedCell = gridManager.GetCellTypeAt(landedStage, enemy.CurrentPos);
                        if (landedCell == CellType.Wall)
                        {
                            Debug.Log($"[EnemyAIRush] Landed on wall at {enemy.CurrentPos} after boundary transfer. Dying without breaking wall.");
                            commandManager.ExecuteCommand(new InstantDeathCommand(enemy, 999, gridManager.ErrorEffectPrefab));
                            if (enemy.gameObject.activeInHierarchy)
                            {
                                if (!commandManager.IsUndoing) AudioManager.Instance.PlaySE(AudioManager.SEType.attack);
                                enemy.OrderSpriteAnimation.PlayAnimation();
                                CameraManager.Instance.ShakeCamera(0.3f, 8.0f, 80f, Utility.GetVectorFromDirection(dir));
                            }
                            isMoving = false;
                            break;
                        }
                    }

                    // 遷移したらループ継続（次のステージでRushを続ける）
                    continue;
                }

                continue; // Continue rushing
            }

            // 3. Destructible Check (Wall or Virtual Wall)
            bool canBreak = false;
            // Check bounds against the stage the enemy is currently in (which might not be the active stage)
            if (nextPos.x >= 0 && nextPos.x < enemyStage.Width && nextPos.y >= 0 && nextPos.y < enemyStage.Height)
            {
                CellType cell = gridManager.GetCellTypeAt(enemyStage, nextPos);
                if (cell == CellType.Wall) canBreak = true;
            }
            else
            {
                // OOB. Is it a breakable boundary?
                if (gridManager.IsBoundaryCoordinate(enemyStage, nextPos))
                {
                    canBreak = true;
                }
            }

            if (canBreak)
            {
                // Break Wall
                BreakWallCommand breakCmd = new BreakWallCommand(gridManager, enemyStage, nextPos, enemy, rushDamage);
                commandManager.ExecuteCommand(breakCmd);

                if (enemy.gameObject.activeInHierarchy)
                {
                    if(!commandManager.IsUndoing) AudioManager.Instance.PlaySE(AudioManager.SEType.attack);
                    enemy.OrderSpriteAnimation.PlayAnimation();
                    CameraManager.Instance.ShakeCamera(0.3f, 8.0f, 80f, Utility.GetVectorFromDirection(dir));
                }

                isMoving = false; // Stop after breaking
                break;
            }

            // 4. Hard Stop (Unbreakable Boundary / Map Edge)
            commandManager.ExecuteCommand(new RushAttackCommand(enemy, gridManager));

            if (enemy.gameObject.activeInHierarchy)
            {
                if(!commandManager.IsUndoing) AudioManager.Instance.PlaySE(AudioManager.SEType.attack);

                enemy.OrderSpriteAnimation.PlayAnimation();
                CameraManager.Instance.ShakeCamera(0.3f, 15.0f, 60f, Utility.GetVectorFromDirection(dir));

                await UniTask.WaitUntil(() => !enemy.IsMoving);
            }

            isMoving = false;
            break;
        }


        // 突進終了後に階段チェック
        if (!isMoving && enemy != null && !enemy.IsDead) // activeInHierarchy check removed to allow off-screen transition
        {
            var transition = gridManager.GetEnemyStairDestination(enemy, enemy.CurrentPos, dir);
            if (transition != null)
            {
               // Change state to Idle before moving so saved data reflects Idle state
               commandManager.ExecuteCommand(new SetRushStateCommand(this, (int)RushState.Idle));

               if (GameSettings.GameSpeed == 0) await UniTask.Delay(200);
               await gridManager.MoveEnemyToStage(enemy, transition.Value.targetStage, transition.Value.targetPos, dir);
            }
        }
    }

    private StageSO GetEnemyStage(Enemy enemy)
    {
       // Inefficient search, but reliable
       // GridManager doesn't expose "GetStageOfEnemy" efficiently yet.
       // But we know if it is active, it's CurrentStage.
       if (enemy.gameObject.activeInHierarchy) return gridManager.CurrentStageSO;

       // Otherwise search logic (we can't easily access private stageEnemiesMap from here without getter).
       // GridManager should probably expose a method `GetStageOfEnemy(Enemy)`
       return gridManager.GetStageOfEnemy(enemy);
    }

    private Unit GetUnitAt(StageSO stage, Vector2Int pos)
    {
        // 1. If within bounds, check normally
        if (pos.x >= 0 && pos.x < stage.Width && pos.y >= 0 && pos.y < stage.Height)
        {
            if (stage == gridManager.CurrentStageSO)
            {
                return gridManager.GetUnitAtPosition(pos);
            }
            else
            {
                // Check Enemies
                List<Enemy> enemies = gridManager.GetEnemiesOnStage(stage);
                if (enemies != null)
                {
                    foreach (var e in enemies) if (e.CurrentPos == pos && !e.IsDead) return e;
                }

                // Ensure Crates are loaded before checking
                gridManager.EnsureStageCratesLoaded(stage);

                // Check Crates
                if (gridManager.StageCratesMap.ContainsKey(stage))
                {
                    foreach (var crate in gridManager.StageCratesMap[stage])
                    {
                        if (crate != null && crate.CurrentPos == pos && !crate.IsDead) return crate;
                    }
                }
            }
        }
        else
        {
            // 2. If out of bounds, check the adjacent stage
            if (gridManager.IsBoundaryCoordinate(stage, pos))
            {
                StageSO nextStage = gridManager.GetAdjacentStageFromBoundary(stage, pos);
                if (nextStage != null)
                {
                    // Direction the unit is traveling to enter the next stage
                    Direction moveDir;
                    if (pos.x == -1) moveDir = Direction.Left;
                    else if (pos.x == stage.Width) moveDir = Direction.Right;
                    else if (pos.y == -1) moveDir = Direction.Down;
                    else moveDir = Direction.Up;

                    Vector2Int entryPos = GridCalculation.CalculateEntryPosition(nextStage, moveDir, pos);
                    return GetUnitAt(nextStage, entryPos); // Recursive call
                }
            }
        }
        return null;
    }

    private Direction GetDirectionTo(Vector2Int from, Vector2Int to)
    {
        Vector2Int diff = to - from;
        if (Mathf.Abs(diff.x) > Mathf.Abs(diff.y)) return diff.x > 0 ? Direction.Right : Direction.Left;
        return diff.y > 0 ? Direction.Up : Direction.Down;
    }
}
