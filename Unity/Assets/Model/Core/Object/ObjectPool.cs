using System;
using System.Collections.Generic;

namespace ET
{
    /// <summary>
    /// 组件队列
    /// </summary>
    public class ComponentQueue: Object
    {
        public string TypeName { get; }

        private readonly Queue<Object> _queue = new Queue<Object>();

        public ComponentQueue(string typeName)
        {
            TypeName = typeName;
        }

        public void Enqueue(Object entity)
        {
            _queue.Enqueue(entity);
        }

        public Object Dequeue()
        {
            return _queue.Dequeue();
        }

        public Object Peek()
        {
            return _queue.Peek();
        }

        public Queue<Object> Queue => _queue;

        public int Count => _queue.Count;

        public override void Dispose()
        {
            while (_queue.Count > 0)
            {
                Object component = _queue.Dequeue();
                component.Dispose();
            }
        }
    }

    /// <summary>
    /// 对象池
    /// </summary>
    public class ObjectPool: Object
    {
        private static ObjectPool _instance;

        public static ObjectPool Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new ObjectPool();
                }

                return _instance;
            }
        }

        private readonly Dictionary<Type, ComponentQueue> _dictionary = new Dictionary<Type, ComponentQueue>();

        public Object Fetch(Type type)
        {
            Object obj;
            if (!_dictionary.TryGetValue(type, out ComponentQueue queue))
            {
                obj = (Object)Activator.CreateInstance(type);
            }
            else if (queue.Count == 0)
            {
                obj = (Object)Activator.CreateInstance(type);
            }
            else
            {
                obj = queue.Dequeue();
            }

            return obj;
        }

        public T Fetch<T>() where T : Object
        {
            T t = (T)Fetch(typeof(T));
            return t;
        }

        public void Recycle(Object obj)
        {
            Type type = obj.GetType();
            ComponentQueue queue;
            if (!_dictionary.TryGetValue(type, out queue))
            {
                queue = new ComponentQueue(type.Name);

#if UNITY_EDITOR && VIEWGO
                if (queue.ViewGO != null)
                {
                    queue.ViewGO.transform.SetParent(ViewGO.transform);
                    queue.ViewGO.name = $"{type.Name}s";
                }
#endif
                _dictionary.Add(type, queue);
            }

#if UNITY_EDITOR && VIEWGO
            if (obj.ViewGO != null)
            {
                obj.ViewGO.transform.SetParent(queue.ViewGO.transform);
            }
#endif
            queue.Enqueue(obj);
        }

        public void Clear()
        {
            foreach (KeyValuePair<Type, ComponentQueue> kv in _dictionary)
            {
                kv.Value.Dispose();
            }

            _dictionary.Clear();
        }

        public override void Dispose()
        {
            foreach (KeyValuePair<Type, ComponentQueue> kv in _dictionary)
            {
                kv.Value.Dispose();
            }

            _dictionary.Clear();
            _instance = null;
        }
    }
}