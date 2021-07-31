using System.Collections.Generic;

namespace ET
{
    public class QueueDictionary<T, K>
    {
        private readonly List<T> _list = new List<T>();
        
        private readonly Dictionary<T, K> _dictionary = new Dictionary<T, K>();

        public int Count
        {
            get
            {
                return _list.Count;
            }
        }

        public T FirstKey
        {
            get
            {
                return _list[0];
            }
        }

        public K FirstValue
        {
            get
            {
                T t = _list[0];
                return this[t];
            }
        }

        public K this[T t]
        {
            get
            {
                return _dictionary[t];
            }
        }

        public void Enqueue(T t, K k)
        {
            _list.Add(t);
            _dictionary.Add(t, k);
        }

        public void Dequeue()
        {
            if (_list.Count == 0)
            {
                return;
            }

            T t = _list[0];
            _list.RemoveAt(0);
            _dictionary.Remove(t);
        }

        public void Remove(T t)
        {
            _list.Remove(t);
            _dictionary.Remove(t);
        }

        public bool ContainsKey(T t)
        {
            return _dictionary.ContainsKey(t);
        }

        public void Clear()
        {
            _list.Clear();
            _dictionary.Clear();
        }
    }
}