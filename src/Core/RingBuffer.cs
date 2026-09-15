using System;

namespace MoonlightSuite.Core;

internal sealed class RingBuffer<T>
{
    private readonly T[] _items;
    private int _head;

    public RingBuffer(int capacity)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity));

        _items = new T[capacity];
    }

    public int Capacity => _items.Length;

    public int Count { get; private set; }

    public T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            var offset = _head - 1 - index;
            return _items[offset < 0 ? offset + _items.Length : offset];
        }
    }

    public void Add(T item)
    {
        _items[_head] = item;
        _head = _head + 1 == _items.Length ? 0 : _head + 1;

        if (Count < _items.Length)
            Count++;
    }

    public void Fill(T item)
    {
        for (var i = 0; i < _items.Length; i++)
            _items[i] = item;

        _head = 0;
        Count = _items.Length;
    }
}
