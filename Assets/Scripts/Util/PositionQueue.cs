using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class PositionQueue
{
    private readonly Queue<PosRotTime> _positions;
    private readonly int _size;

    public PositionQueue(int size)
    {
        _positions = new Queue<PosRotTime>(size);
        _size = size;
    }
    public void Add(Vector3 position)
    {
        PosRotTime posRotTime = null;
        if (_positions.Count == _size)
            posRotTime = _positions.Dequeue();

        if (posRotTime == null)
            posRotTime = new PosRotTime();

        posRotTime.position = position;
        //posRotTime.rotation = rotation;
        posRotTime.timeInTicks = System.DateTime.Now.Ticks;
        _positions.Enqueue(posRotTime);
    }
    private Vector3Double VelocityBetween(PosRotTime before, PosRotTime after)
    {
        // TODO will fail on tick rollover
        double deltaTime = (double)(after.timeInTicks - before.timeInTicks) / (double)TimeSpan.TicksPerSecond;
        return (new Vector3Double(after.position) - new Vector3Double(before.position)) / deltaTime;
        //long deltaTick = (after.timeInTicks - before.timeInTicks);
        //return ((after.position - before.position) * deltaTick) / TimeSpan.TicksPerSecond;
    }
    public Vector3 ReadMedianVelocity()
    {
        if (_positions.Count < 2)
            return Vector3.zero;

        PosRotTime before = null;
        PosRotTime after = _positions.Dequeue();
        SortedList<double> x_vel = new SortedList<double>();
        SortedList<double> y_vel = new SortedList<double>();
        SortedList<double> z_vel = new SortedList<double>();
        int numDataPoints = 0;
        
        while(_positions.Count > 0)
        {
            before = after;
            after = _positions.Dequeue();
            Vector3Double vel = VelocityBetween(before, after);
            x_vel.Add(vel.X);
            y_vel.Add(vel.Y);
            z_vel.Add(vel.Z);
            numDataPoints++;
        }

        int middleIndex = numDataPoints / 2;
        double avgX = x_vel[middleIndex];
        double avgY = y_vel[middleIndex];
        double avgZ = z_vel[middleIndex];
        if(numDataPoints % 2 != 0)
        {
            avgX += x_vel[middleIndex + 1];
            avgY += y_vel[middleIndex + 1];
            avgZ += z_vel[middleIndex + 1];

            avgX = avgX / 2;
            avgY = avgY / 2;
            avgZ = avgZ / 2;
        }

        var ret = new Vector3((float)avgX, (float)avgY, (float)avgZ);
        return ret;
    }
    public void Clear()
    {
        _positions.Clear();
    }
    public int Count()
    {
        return _positions.Count;
    }
}
public class PosRotTime
{
    public Vector3 position;
    public Quaternion rotation;
    public long timeInTicks = -1;
}