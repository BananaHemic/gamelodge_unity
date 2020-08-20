using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RequestQueue<T>
{
    public const int MaxPriority = 5;
    public const int NumPriorities = MaxPriority + 1; // We use the max priority, and a 0 priority
    private readonly T[] _requested = new T[NumPriorities];
    private readonly bool[] _hasVal = new bool[NumPriorities];
    private readonly BaseBehavior[] _requesters = new BaseBehavior[NumPriorities];

    public RequestQueue(T defaultVal)
    {
        _requested[0] = defaultVal;
        _hasVal[0] = true;
    }
    public int GetMaxPriority()
    {
        return MaxPriority;
    }
    public T Get(out int priority)
    {
        for(int i = MaxPriority; i >= 0; i--)
        {
            if (_hasVal[i])
            {
                priority = i;
                return _requested[i];
            }
        }
        Debug.LogError("Nothing available for LayerTagRequest!");
        throw new System.Exception();
    }
    public bool AddRequest(T val, BaseBehavior requester, int priority)
    {
        if(priority > MaxPriority)
        {
            Debug.LogError("Too high of a request priority! " + priority);
            return false;
        }
        // The only one setting priority 0 is sceneobject
        // Normal behaviors interact differently with this stuff
        if(priority != 0)
        {
            if(requester == null)
            {
                Debug.LogError("Got add request with null requester!");
                return false;
            }

            if(_hasVal[priority] && _requesters[priority] != requester)
                Debug.LogError("Rewriting requester. Was " + _requesters[priority].GetBehaviorInfo().Name + " Now " + requester.GetBehaviorInfo().Name);
        }

        _hasVal[priority] = true;
        _requested[priority] = val;
        _requesters[priority] = requester;

        // Figure out if there are any remaining higher priority requests
        for(int i = MaxPriority; i > priority; i--)
        {
            if (_hasVal[i])
                return false;
        }
        return true;
    }
    public bool ClearRequest(BaseBehavior requester, int priority)
    {
        if(priority > MaxPriority)
        {
            Debug.LogError("Too high of a clear request priority! " + priority);
            return false;
        }
        if(requester == null)
        {
            Debug.LogError("Got clear request with null requester!");
            return false;
        }
        if (!_hasVal[priority])
        {
            Debug.LogWarning("Clearing request, but no val for priority " + priority);
            return false;
        }

        if(_requesters[priority] != requester)
        {
            Debug.LogError("Clear request has wrong requester. Was " + _requesters[priority].GetBehaviorInfo().Name + " Now " + requester.GetBehaviorInfo().Name);
            return false;
        }

        _hasVal[priority] = false;
        _requesters[priority] = null;
        // Figure out if there are any remaining higher priority requests
        for(int i = MaxPriority; i > priority; i--)
        {
            if (_hasVal[i])
                return false;
        }
        return true;
    }
    public bool RemoveRequester(BaseBehavior baseBehavior)
    {
        bool didChange = false;
        for(int i = MaxPriority - 1; i >= 0; i--)
        {
            if(_requesters[i] == baseBehavior)
            {
                _requesters[i] = null;
                _hasVal[i] = false;
                didChange = true;
            }
        }
        return didChange;
    }
}
