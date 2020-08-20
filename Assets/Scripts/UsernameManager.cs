using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UsernameManager : GenericSingleton<UsernameManager>
{
    public GameObject UsernamePrefab;
    public UsernameDisplay GetUsernameDisplay(UserDisplay userDisp)
    {
        GameObject obj = SimplePool.Instance.SpawnUI(UsernamePrefab, transform);
        UsernameDisplay nameDisp = obj.GetComponent<UsernameDisplay>();
        nameDisp.Init(userDisp);
        return nameDisp;
    }
    public void ReturnUsernameDisplay(UsernameDisplay disp)
    {
        disp.Reset();
        SimplePool.Instance.DespawnUI(disp.gameObject);
    }
}
