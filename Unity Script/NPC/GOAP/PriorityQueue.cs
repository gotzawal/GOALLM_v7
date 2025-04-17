// https://github.com/gotzawal/GOALLM_v7

using System;
using System.Collections.Generic;
using System.Linq;

public class PriorityQueue<T>
{
    private SortedDictionary<float, Queue<T>> _sortedDict = new SortedDictionary<float, Queue<T>>();

    public void Enqueue(T item, float priority)
    {
        if (!_sortedDict.ContainsKey(priority))
        {
            _sortedDict[priority] = new Queue<T>();
        }
        _sortedDict[priority].Enqueue(item);
    }

    public T Dequeue()
    {
        if (_sortedDict.Count == 0)
            throw new InvalidOperationException("The priority queue is empty.");

        var firstPair = _sortedDict.First();
        var item = firstPair.Value.Dequeue();
        if (firstPair.Value.Count == 0)
            _sortedDict.Remove(firstPair.Key);
        return item;
    }

    public bool IsEmpty()
    {
        return _sortedDict.Count == 0;
    }
}
