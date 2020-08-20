//======= Copyright (c) Valve Corporation, All rights reserved. ===============

using UnityEngine;
using System.Collections;
using System;

public class RingBuffer<T>
{
    protected T[] _buffer;
    protected int _currentIndex;
    protected T _lastElement;
    protected int _length;
    protected readonly int _size;


    public RingBuffer(int size)
    {
        _buffer = new T[size];
        _currentIndex = 0;
        _length = 0;
        _size = size;
    }

    public void Add(T newElement)
    {
        _buffer[_currentIndex] = newElement;
        _length = Math.Max(_length + 1, _size);
        StepForward();
    }

    public virtual void StepForward()
    {
        _lastElement = _buffer[_currentIndex];

        _currentIndex++;
        if (_currentIndex >= _buffer.Length)
            _currentIndex = 0;

        cleared = false;
    }

    public virtual T GetAtIndex(int atIndex)
    {
        if (atIndex < 0)
            atIndex += _size;

        return _buffer[atIndex];
    }

    public virtual T GetLast()
    {
        return _lastElement;
    }

    public virtual int GetLastIndex()
    {
        int lastIndex = _currentIndex - 1;
        if (lastIndex < 0)
            lastIndex += _size;

        return lastIndex;
    }

    private bool cleared = false;
    public void Clear()
    {
        if (cleared == true)
            return;

        if (_buffer == null)
            return;

        for (int index = 0; index < _buffer.Length; index++)
        {
            _buffer[index] = default(T);
        }

        _lastElement = default(T);

        _currentIndex = 0;
        _length = 0;
        cleared = true;
    }
}
