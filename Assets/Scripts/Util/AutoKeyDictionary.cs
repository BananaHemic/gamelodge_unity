using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AutoKeyDictionary<TValue> : IEnumerable<KeyValuePair<int, TValue>>, IEnumerable
{
    private readonly Dictionary<int, TValue> inner;
    private readonly Stack<int> freeKeys;
    private int currentKey;

    public AutoKeyDictionary()
    {
        inner = new Dictionary<int, TValue>();
        freeKeys = new Stack<int>();
        currentKey = 0;
    }

    public int Add(TValue value) //returns the used key
    {
        int usedKey;

        if (freeKeys.Count > 0)
        {
            usedKey = freeKeys.Pop();
            inner.Add(usedKey, value);
        }
        else
        {
            usedKey = currentKey;
            inner.Add(usedKey, value);
            currentKey++;
        }

        return usedKey;
    }

    public void Clear()
    {
        inner.Clear();
        freeKeys.Clear();
        currentKey = 0;
    }

    public bool Remove(int key)
    {
        if (inner.Remove(key))
        {
            if (inner.Count > 0)
            {
                freeKeys.Push(key);
            }
            else
            {
                freeKeys.Clear();
                currentKey = 0;
            }
            return true;
        }
        return false;
    }

    public bool TryGetValue(int key, out TValue value) { return inner.TryGetValue(key, out value); }
    public TValue this[int key] { get { return inner[key]; } set { inner[key] = value; } }
    public bool ContainsKey(int key) { return inner.ContainsKey(key); }
    public bool ContainsValue(TValue value) { return inner.ContainsValue(value); }
    public int Count { get { return inner.Count; } }
    public Dictionary<int, TValue>.KeyCollection Keys { get { return inner.Keys; } }
    public Dictionary<int, TValue>.ValueCollection Values { get { return inner.Values; } }
    public IEnumerator<KeyValuePair<int, TValue>> GetEnumerator() { return inner.GetEnumerator(); }
    IEnumerator IEnumerable.GetEnumerator() { return ((IEnumerable)inner).GetEnumerator(); }
}
