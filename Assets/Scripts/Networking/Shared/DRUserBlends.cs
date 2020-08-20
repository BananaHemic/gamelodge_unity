using DarkRift;
using System;
using System.Collections;
using System.Collections.Generic;

public class DRUserBlends : IDarkRiftSerializable
{
    // All the non-default blend shapes for the user
    // OTHER than the ones for the mouth. Those are
    // considered ephemeral and are sent with the voice
    private Dictionary<int, float> _allBlends;
    public int Count { get { return _allBlends != null ? _allBlends.Count : 0; } }
    public DRUserBlends() { }
    public DRUserBlends(int initialIdx, float initialVal) {
        _allBlends = new Dictionary<int, float>();
        _allBlends.Add(initialIdx, initialVal);
    }
    public void SetBlend(int idx, float val)
    {
        if (_allBlends == null)
            _allBlends = new Dictionary<int, float>();
        _allBlends[idx] = val;
    }
    public void Clear()
    {
        if (_allBlends == null)
            return;
        _allBlends.Clear();
    }
    public void Deserialize(DeserializeEvent e)
    {
        int numBlends = e.Reader.DecodeInt32();
        //DRCompat.Log("Num blends: " + numBlends);
        _allBlends = new Dictionary<int, float>(numBlends);
        for(int i = 0; i < numBlends; i++)
        {
            int idx = e.Reader.DecodeInt32();
            float val = e.Reader.ReadSingle();
            _allBlends.Add(idx, val);
        }
    }
    public void Serialize(SerializeEvent e)
    {
        // Serialize the number of blends
        int numBlends = _allBlends != null ? _allBlends.Count : 0;
        e.Writer.EncodeInt32(numBlends);
        if (numBlends == 0)
            return;
        // Encode each blend, with both the index (as var int)
        // and the value as a float
        foreach(var blend in _allBlends)
        {
            e.Writer.EncodeInt32(blend.Key);
            e.Writer.Write(blend.Value);
        }
    }
}
