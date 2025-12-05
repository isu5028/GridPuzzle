using Cysharp.Threading.Tasks;
using UnityEngine;

public class Enemy : Unit
{
    [SerializeField] private EnemyType type;
    public EnemyType Type { get { return type; } }
    private EnemyAI enemyAI = null!;

    public override void Initialize(GridManager gridMgr,
                           TurnManager turnMgr,
                           CommandManager commandMgr,
                           Vector2Int startPos,
                           Direction startDirection,
                           int maxHp,
                           int startOrder)
    {
        base.Initialize(gridMgr, turnMgr, commandMgr, startPos, startDirection, maxHp, startOrder);
        enemyAI = GetComponent<EnemyAI>();
        enemyAI.Initialize(gridMgr, commandMgr);
    }

    public async UniTask ExecuteTurn()
    {
        await enemyAI.ExecuteAI(this);
    }

    protected override void OnEnable()
    {
        base.OnEnable();
    }

    public void ForceKillSilent()
    {
        isDead = true;
        gameObject.SetActive(false); // 見た目を消す
    }

    // 強制移動（アニメーションなし）
    public void WarpTo(Vector2Int pos, Direction dir)
    {
        currentPos = pos;
        currentDirection = dir;
        transform.position = gridManager.GridToWorldPosition(pos);
        // Animatorの向き更新など
        unitAnimator.SetDirection(dir);
    }

    public void ReviveSilent()
    {
        isDead = false;
        gameObject.SetActive(true);
        unitAnimator.SetDirection(currentDirection);
    }
}