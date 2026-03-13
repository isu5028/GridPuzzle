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
    private List<GameObject> crateObjects = new List<GameObject>();
    private List<Crate> crateScripts = new List<Crate>();

    public Player Player => playerScript;
    public List<Enemy> Enemies => enemyScripts;
    public List<Crate> Crates => crateScripts;
    public GameObject PlayerObject => playerObject;
    public List<GameObject> EnemyObjects => enemyObjects;
    public List<GameObject> CrateObjects => crateObjects;

    // ユニットの状態変化通知イベント (UniRx)
    private Subject<Unit> unitStateChangedSubject = new Subject<Unit>();
    public IObservable<Unit> OnUnitStateChanged => unitStateChangedSubject;

    public void ClearAllUnits()
    {
        foreach (var enemy in enemyObjects)
        {
            if (enemy != null)
            {
                enemy.SetActive(false);
                Destroy(enemy);
            }
        }
        if (playerObject != null)
        {
            playerObject.SetActive(false);
            Destroy(playerObject);
        }

        foreach (var crate in crateObjects)
        {
            if (crate != null)
            {
                crate.SetActive(false);
                Destroy(crate);
            }
        }

        enemyObjects.Clear();
        enemyScripts.Clear();
        crateObjects.Clear();
        crateScripts.Clear();
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

    public void AddEnemy(Enemy enemy)
    {
        if (!enemyScripts.Contains(enemy))
        {
            enemyScripts.Add(enemy);
            enemyObjects.Add(enemy.gameObject);
        }
    }

    public void RemoveEnemy(Enemy enemy)
    {
        if (enemyScripts.Contains(enemy))
        {
            enemyScripts.Remove(enemy);
            enemyObjects.Remove(enemy.gameObject);
        }
    }

    public void SetAllEnemiesActive(bool isActive)
    {
        for (int i = enemyObjects.Count - 1; i >= 0; i--)
        {
            var enemy = enemyObjects[i];
            if (enemy != null)
            {
                enemy.SetActive(isActive);
            }
            else
            {
                // Clean up references to destroyed objects
                enemyObjects.RemoveAt(i);
            }
        }

        for (int i = crateObjects.Count - 1; i >= 0; i--)
        {
            var crate = crateObjects[i];
            if (crate != null)
            {
                crate.SetActive(isActive);
            }
            else
            {
                crateObjects.RemoveAt(i);
            }
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
            // Unity Object null check
            if (enemy == null) continue;

            if (!enemy.IsDead && enemy.CurrentPos == pos)
                return enemy;
        }
        foreach (var crate in crateScripts)
        {
            if (crate == null) continue;

            if (!crate.IsDead && crate.CurrentPos == pos)
                return crate;
        }

        return null;
    }

    public bool IsAnyCrateMoving()
    {
        foreach (var crate in crateScripts)
        {
            if (crate != null && crate.IsMoving) return true;
        }
        return false;
    }

    public void RegisterCrate(GameObject crateObj)
    {
        if (!crateObjects.Contains(crateObj))
        {
            crateObjects.Add(crateObj);
            var script = crateObj.GetComponent<Crate>();
            crateScripts.Add(script);
        }
    }

    public void AddCrate(Crate crate)
    {
        if (!crateScripts.Contains(crate))
        {
            crateScripts.Add(crate);
            crateObjects.Add(crate.gameObject);
        }
    }

    public void RemoveCrate(Crate crate)
    {
        if (crateScripts.Contains(crate))
        {
            crateScripts.Remove(crate);
            crateObjects.Remove(crate.gameObject);
        }
    }

    public bool HasAliveEnemy()
    {
        foreach (var enemy in enemyScripts)
        {
            if (enemy == null) continue;

            if (!enemy.IsDead)
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
