using System;
using System.Collections.Generic;
using UnityEngine;
using UniRx;

public class UnitManager : MonoBehaviour
{
    private GameObject playerObject;
    private Player playerScript;
    private List<GameObject> enemyObjects = new List<GameObject>();
    private List<Enemy> enemyScripts = new List<Enemy>();

    public Player Player => playerScript;
    public List<Enemy> Enemies => enemyScripts;
    public GameObject PlayerObject => playerObject;
    public List<GameObject> EnemyObjects => enemyObjects;

    // ユニットの状態変化通知イベント (UniRx)
    private Subject<Unit> unitStateChangedSubject = new Subject<Unit>();
    public IObservable<Unit> OnUnitStateChanged => unitStateChangedSubject;

    public void ClearAllUnits()
    {
        foreach (var enemy in enemyObjects)
        {
            if (enemy != null) Destroy(enemy);
        }
        if (playerObject != null) Destroy(playerObject);

        enemyObjects.Clear();
        enemyScripts.Clear();
        playerScript = null;
        playerObject = null;
    }

    public void RegisterPlayer(GameObject playerObj)
    {
        playerObject = playerObj;
        playerScript = playerObj.GetComponent<Player>();
    }

    public void RegisterEnemy(GameObject enemyObj)
    {
        if (!enemyObjects.Contains(enemyObj))
        {
            enemyObjects.Add(enemyObj);
            var script = enemyObj.GetComponent<Enemy>();
            enemyScripts.Add(script);
        }
    }

    public void SetEnemies(List<Enemy> enemies)
    {
        enemyScripts = new List<Enemy>(enemies);
        enemyObjects.Clear();
        foreach(var e in enemies)
        {
            if(e != null) enemyObjects.Add(e.gameObject);
        }
    }

    public void SetAllEnemiesActive(bool isActive)
    {
        foreach (var enemy in enemyObjects)
        {
            if (enemy != null) enemy.gameObject.SetActive(isActive);
        }
    }

    public void SortEnemiesByOrder()
    {
         enemyScripts.Sort((a, b) => a.Order.CompareTo(b.Order));
    }

    public Unit GetUnitAtPosition(Vector2Int pos)
    {
        if (playerScript != null && playerScript.CurrentPos == pos)
            return playerScript;

        foreach (var enemy in enemyScripts)
        {
            if (enemy != null && !enemy.IsDead && enemy.CurrentPos == pos)
                return enemy;
        }

        return null;
    }

    public bool HasAliveEnemy()
    {
        foreach (var enemy in enemyScripts)
        {
            if (enemy != null && !enemy.IsDead)
            {
                return true;
            }
        }
        return false;
    }

    public void NotifyUnitStateChanged(Unit unit = null)
    {
        unitStateChangedSubject.OnNext(unit);
    }
}
