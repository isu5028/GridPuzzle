using Cysharp.Threading.Tasks;
using UnityEngine;

public abstract class EnemyAI: MonoBehaviour
{
    protected GridManager gridManager;
    protected CommandManager commandManager;
    protected float fastSpeedAttackDelayMs = 200f;

    public void Initialize(GridManager gridMgr,
                           CommandManager cmdMgr)
    {
        this.gridManager = gridMgr;
        this.commandManager = cmdMgr;
    }

    public abstract UniTask ExecuteAI(Enemy enemy);

    // AI State Persistence
    public virtual int GetState() { return 0; }
    public virtual void SetState(int state) { }
}