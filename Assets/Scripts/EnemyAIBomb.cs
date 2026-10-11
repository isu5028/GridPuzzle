using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using DG.Tweening;

public class EnemyAIBomb : EnemyAI
{
    private bool isCountdownStarted = false;
    private bool isExploded = false;

    private Enemy ownerEnemy;

    public override int GetState()
    {
        if (isExploded) return 2;
        return isCountdownStarted ? 1 : 0;
    }

    public override void SetState(int state)
    {
        isCountdownStarted = (state == 1 || state == 2);
        isExploded = (state == 2);

        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (ownerEnemy != null)
        {
            ownerEnemy.SetAmmoTextActive(!isExploded);

            // アニメーション・揺れの更新
            if (isCountdownStarted && !isExploded)
            {
                ownerEnemy.UnitAnimator.IsAnimationLocked = true;
                ownerEnemy.UnitAnimator.PlayAnimation(ownerEnemy.UnitAnimator.IgnitionAnimation);

                // 本体を激しく揺らす（爆発寸前の不安定さを表現）
                Transform sprite = ownerEnemy.transform.Find("Sprite");
                if (sprite != null)
                {
                    sprite.DOKill();
                    sprite.DOShakePosition(1f, 0.8f, 50, 90f, false, false).SetLoops(-1);
                }
            }
            else
            {
                ownerEnemy.UnitAnimator.IsAnimationLocked = false;
                // 通常状態に戻す
                if (!isExploded) ownerEnemy.UnitAnimator.SetDirection(ownerEnemy.CurrentDirection);

                // 揺れを止める
                Transform sprite = ownerEnemy.transform.Find("Sprite");
                if (sprite != null) sprite.DOKill();
                if (sprite != null) sprite.localPosition = Vector3.zero;
            }
        }
    }

    public void Awake()
    {
        // Awake時点では初期化されていない可能性がある
    }

    private bool visualsInitialized = false;
    private void EnsureVisuals(Enemy enemy)
    {
        ownerEnemy = enemy;
        if (visualsInitialized) return;

        // StageSOでstartAmmoが設定されていない(-1)場合、デフォルト値を3に設定
        if (enemy.MaxAmmo < 0)
        {
            enemy.SetMaxAmmo(3);
        }

        UpdateVisuals();
        visualsInitialized = true;
    }

    public override async UniTask ExecuteAI(Enemy enemy)
    {
        EnsureVisuals(enemy);

        if (isExploded || enemy.IsDead) return;

        // 高速モードで直前の被弾ノックバックと自ターン爆発が重なると、
        // 見た目と実ダメージの不整合が起きるため、移動/演出中は完了待ちする。
        if (enemy.IsBusy)
        {
            await UniTask.WaitUntil(() => !enemy.IsBusy || enemy.IsDead || isExploded);
            if (isExploded || enemy.IsDead) return;
        }

        if (isCountdownStarted)
        {
            if (enemy.CurrentAmmo.Value > 0)
            {
                commandManager.ExecuteCommand(new ConsumeAmmoCommand(enemy, 1));
                // 減少後の値が0になった瞬間に爆発
                if (enemy.CurrentAmmo.Value <= 0)
                {
                    await Explode(enemy);
                }
            }
            else
            {
                // すでに0以下の場合は即爆発
                await Explode(enemy);
            }
        }
    }

    public override void OnDamageTaken(Enemy enemy, Vector2Int attackerPos)
    {
        ownerEnemy = enemy;
        if (isExploded || enemy.IsDead) return;

        // カウントダウン開始
        if (!isCountdownStarted)
        {
            // StageSOでstartAmmoが設定されていない(-1)場合、デフォルト値を3に設定
            if (enemy.MaxAmmo < 0)
            {
                enemy.SetMaxAmmo(3);
            }
            commandManager.ExecuteCommand(new SetAIStateCommand(this, 1));
        }

        // ノックバック処理
        Knockback(enemy, attackerPos).Forget();
    }

    private async UniTask Knockback(Enemy enemy, Vector2Int attackerPos)
    {
        if (commandManager.IsUndoing || commandManager.IsRedoing) return;

        Vector2Int currentPos = enemy.CurrentPos;
        Vector2Int diff = currentPos - attackerPos;

        // 攻撃方向と逆方向を計算（斜めからの攻撃なら斜め方向にノックバックする）
        Vector2Int dirVec = new Vector2Int(
            diff.x == 0 ? 0 : (diff.x > 0 ? 1 : -1),
            diff.y == 0 ? 0 : (diff.y > 0 ? 1 : -1)
        );

        if (dirVec == Vector2Int.zero) return;

        Direction knockbackDir = Utility.GetDirectionFromVector(dirVec);

        Vector2Int targetPos = currentPos + dirVec;
        StageSO stage = gridManager.GetStageOfEnemy(enemy);

        // 移動可能かチェック（地形が歩ける 且つ ユニットがいない）
        Unit targetOccupant = gridManager.GetUnitAtPosition(targetPos);
        bool isWalkable = gridManager.IsWalkableAt(stage, targetPos);
        bool isOccupied = targetOccupant != null;

        // ノックバック先が押し出し対象（Crate/Bomb）なら、連鎖押し出しを試みる
        if (IsPushableByBombKnockback(targetOccupant))
        {
            List<Unit> unitsToPush = new List<Unit>();
            Vector2Int checkPos = targetPos;
            while (true)
            {
                Unit nextUnit = gridManager.GetUnitAtPosition(checkPos);
                if (!IsPushableByBombKnockback(nextUnit)) break;

                unitsToPush.Add(nextUnit);
                checkPos += dirVec;
            }

            bool canPush = false;
            if (gridManager.IsCellWalkable(checkPos))
            {
                canPush = true;
            }
            else if (!gridManager.IsPositionInBounds(checkPos))
            {
                // OOBへの押し出しは、境界壁が壊れており、遷移先も受け入れ可能な場合のみ
                bool isCardinal = (dirVec.x == 0) ^ (dirVec.y == 0);
                if (isCardinal && gridManager.IsWalkableAt(stage, checkPos))
                {
                    StageSO neighbor = gridManager.GetAdjacentStageFromBoundary(stage, checkPos);
                    if (neighbor != null)
                    {
                        Vector2Int neighborEntry = GridCalculation.CalculateEntryPosition(neighbor, knockbackDir, checkPos);
                        if (gridManager.IsWalkableAt(neighbor, neighborEntry) && gridManager.GetUnitAtPosition(neighborEntry) == null)
                        {
                            canPush = true;
                        }
                    }
                }
            }

            if (canPush)
            {
                for (int i = unitsToPush.Count - 1; i >= 0; i--)
                {
                    Unit pushUnit = unitsToPush[i];
                    Vector2Int unitCurrentPos = targetPos + dirVec * i;
                    Vector2Int unitTargetPos = unitCurrentPos + dirVec;

                    Direction pushToDirection = pushUnit is Crate ? knockbackDir : pushUnit.CurrentDirection;
                    MoveCommand pushCommand = new MoveCommand(pushUnit, unitCurrentPos, unitTargetPos, pushUnit.CurrentDirection, pushToDirection, false);
                    ExecuteKnockbackCommand(pushCommand);
                }

                MoveCommand moveCommand = new MoveCommand(enemy, currentPos, targetPos, enemy.CurrentDirection, enemy.CurrentDirection, false);
                ExecuteKnockbackCommand(moveCommand);
                await UniTask.WaitUntil(() => !enemy.IsMoving);

                bool transitioned = await TryTransitionAfterKnockback(enemy, knockbackDir, targetPos);
                if (!transitioned)
                {
                    UpdateVisuals();
                }
                return;
            }
        }

        if (isWalkable && !isOccupied)
        {
            // MoveCommandを使用してUndoに対応させる
            MoveCommand moveCommand = new MoveCommand(enemy, currentPos, targetPos, enemy.CurrentDirection, enemy.CurrentDirection, false);
            ExecuteKnockbackCommand(moveCommand);
            await UniTask.WaitUntil(() => !enemy.IsMoving);

            bool transitioned = await TryTransitionAfterKnockback(enemy, knockbackDir, targetPos);
            if (!transitioned)
            {
                // 移動後に念のためアニメーションを維持（SetDirectionによって上書きされるのを防ぐ）
                UpdateVisuals();
            }
        }
    }

    private void ExecuteKnockbackCommand(ICommand command)
    {
        if (command == null) return;
        if (commandManager.IsUndoing || commandManager.IsRedoing) return;

        commandManager.ExecuteCommandWithTurnFallback(command);
    }

    private async UniTask<bool> TryTransitionAfterKnockback(Enemy enemy, Direction moveDirection, Vector2Int expectedPos)
    {
        if (enemy == null || enemy.IsDead) return false;
        if (commandManager.IsUndoing || commandManager.IsRedoing) return false;
        if (enemy.CurrentPos != expectedPos) return false;

        var transition = gridManager.GetEnemyStairDestination(enemy, expectedPos, moveDirection);
        if (transition == null) return false;

        if (GameSettings.GameSpeed == 0) await UniTask.Delay(200);

        if (enemy == null || enemy.IsDead) return false;
        if (commandManager.IsUndoing || commandManager.IsRedoing) return false;
        if (enemy.CurrentPos != expectedPos) return false;

        await gridManager.MoveEnemyToStage(enemy, transition.Value.targetStage, transition.Value.targetPos, moveDirection);
        return true;
    }

    private bool IsPushableByBombKnockback(Unit unit)
    {
        if (unit == null) return false;
        if (unit is Crate) return true;
        if (unit is Enemy enemy) return enemy.Type == EnemyType.Bomb;
        return false;
    }

    private async UniTask Explode(Enemy enemy)
    {
        if (isExploded) return;

        // 爆破状態へ遷移
        commandManager.ExecuteCommand(new SetAIStateCommand(this, 2));

        // 爆発演出（白い正方形が広がり、ディゾルブで消える）
        if (gridManager.ExplosionVFXPrefab != null)
        {
            GameObject vfxObj = Object.Instantiate(gridManager.ExplosionVFXPrefab, gridManager.transform);
            if (vfxObj != null)
            {
                ExplosionVFX vfx = vfxObj.GetComponent<ExplosionVFX>();
                if (vfx == null) vfx = vfxObj.AddComponent<ExplosionVFX>();

            // 爆発範囲 (周囲を広くカバー)
            float cellSize = gridManager.Visualizer.CellSize;
            float targetScale = 1f; // 6x6マス分程度に拡大

            vfx.PlayEffect(
                gridManager.ExplosionShader,
                gridManager.ExplosionSprite,
                enemy.transform.position,
                targetScale
            ).Forget();
            }
        }

        // SE再生
        AudioManager.Instance.PlaySE(AudioManager.SEType.explode);

        // さらに強力なカメラ揺れ
        CameraManager.Instance.ShakeCamera(0.8f, 40f, 70f, new Vector2(1f, 1f));

        // 周囲8マスの座標を取得
        Vector2Int currentPos = enemy.CurrentPos;
        StageSO stage = gridManager.GetStageOfEnemy(enemy);

        List<Vector2Int> targetPositions = new List<Vector2Int>();
        List<Vector2Int> wallPositions = new List<Vector2Int>();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;
                Vector2Int pos = currentPos + new Vector2Int(x, y);

                // ユニット（プレイヤー・他の敵・Crate）の存在確認
                if (gridManager.GetUnitAtPosition(pos) != null)
                {
                    targetPositions.Add(pos);
                }

                // 壁の存在確認
                if (gridManager.GetCellTypeAt(stage, pos) == CellType.Wall)
                {
                    wallPositions.Add(pos);
                }
            }
        }

        // 1. ユニット・Crateへのダメージ（爆発演出としてAttackCommandを実行）
        if (targetPositions.Count > 0)
        {
            commandManager.ExecuteCommand(new AttackCommand(enemy, targetPositions.ToArray(), gridManager, true, true));
        }

        // 2. 壁の破壊を実行
        foreach (var wallPos in wallPositions)
        {
            commandManager.ExecuteCommand(new BreakWallCommand(gridManager, stage, wallPos, enemy, 0));
        }

        // 爆発演出（SE等があればここで）
        // AudioManager.Instance.PlaySE(AudioManager.SEType.explosion);

        // 3. 自身を撃破（SelfDestructCommandを使用。dropsCoinのOFFも内部で行う）
        commandManager.ExecuteCommand(new SelfDestructCommand(enemy, currentPos));
    }
}
