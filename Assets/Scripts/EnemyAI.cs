using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class EnemyAI: MonoBehaviour
{
    protected GridManager gridManager;
    protected TurnManager turnManager;
    protected CommandManager commandManager;
    protected float fastSpeedAttackDelayMs = 200f;

    public void Initialize(GridManager gridMgr,
                           TurnManager turnMgr,
                           CommandManager cmdMgr)
    {
        this.gridManager = gridMgr;
        this.turnManager = turnMgr;
        this.commandManager = cmdMgr;
    }

    public abstract UniTask ExecuteAI(Enemy enemy);

    // AI State Persistence
    public virtual int GetState() { return 0; }
    public virtual void SetState(int state) { }

    public virtual int ModifyDamage(int damage) { return damage; }
    public virtual void OnDamageTaken(Enemy enemy, Vector2Int attackerPos) { }

    public virtual string GetHPText(int hp) { return null; }
    public virtual string GetOrderText(int order) { return null; }
}