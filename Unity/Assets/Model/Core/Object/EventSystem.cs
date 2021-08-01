using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ET
{
    public sealed class EventSystem: IDisposable
    {
        private static EventSystem _instance;

        public static EventSystem Instance
        {
            get
            {
                return _instance ??= new EventSystem();
            }
        }

        // 组件表
        private readonly Dictionary<long, Entity> _allComponents = new Dictionary<long, Entity>();
        // 程序集表
        private readonly Dictionary<string, Assembly> _assemblies = new Dictionary<string, Assembly>();
        // 类型表
        private readonly UnOrderMultiMapSet<Type, Type> _types = new UnOrderMultiMapSet<Type, Type>();
        // 事件表
        private readonly Dictionary<Type, List<object>> _allEvents = new Dictionary<Type, List<object>>();

        private readonly UnOrderMultiMap<Type, IAwakeSystem> _awakeSystems = new UnOrderMultiMap<Type, IAwakeSystem>();

        private readonly UnOrderMultiMap<Type, IStartSystem> _startSystems = new UnOrderMultiMap<Type, IStartSystem>();

        private readonly UnOrderMultiMap<Type, IDestroySystem> _destroySystems = new UnOrderMultiMap<Type, IDestroySystem>();

        private readonly UnOrderMultiMap<Type, ILoadSystem> _loadSystems = new UnOrderMultiMap<Type, ILoadSystem>();

        private readonly UnOrderMultiMap<Type, IUpdateSystem> _updateSystems = new UnOrderMultiMap<Type, IUpdateSystem>();

        private readonly UnOrderMultiMap<Type, ILateUpdateSystem> _lateUpdateSystems = new UnOrderMultiMap<Type, ILateUpdateSystem>();

        private readonly UnOrderMultiMap<Type, IChangeSystem> _changeSystems = new UnOrderMultiMap<Type, IChangeSystem>();

        private readonly UnOrderMultiMap<Type, IDeserializeSystem> _deserializeSystems = new UnOrderMultiMap<Type, IDeserializeSystem>();

        private Queue<long> _updates = new Queue<long>();
        private Queue<long> _updates2 = new Queue<long>();

        private readonly Queue<long> _starts = new Queue<long>();

        private Queue<long> _loaders = new Queue<long>();
        private Queue<long> _loaders2 = new Queue<long>();

        private Queue<long> _lateUpdates = new Queue<long>();
        private Queue<long> _lateUpdates2 = new Queue<long>();

        private EventSystem()
        {
            Add(typeof(EventSystem).Assembly);
        }

        /// <summary>
        /// 添加程序集
        /// </summary>
        /// <param name="assembly"></param>
        /// <exception cref="Exception"></exception>
        public void Add(Assembly assembly)
        {
            _assemblies[assembly.ManifestModule.ScopeName] = assembly;
            
            //
            _types.Clear();
            foreach (Assembly value in _assemblies.Values)
            {
                foreach (Type type in value.GetTypes())
                {
                    // 跳过抽象类
                    if (type.IsAbstract)
                    {
                        continue;
                    }

                    // 获取自定义特性类型
                    object[] objects = type.GetCustomAttributes(typeof(BaseAttribute), true);
                    if (objects.Length == 0)
                    {
                        continue;
                    }

                    foreach (BaseAttribute baseAttribute in objects)
                    {
                        _types.Add(baseAttribute.AttributeType, type);
                    }
                }
            }
            
            //
            _awakeSystems.Clear();
            _lateUpdateSystems.Clear();
            _updateSystems.Clear();
            _startSystems.Clear();
            _loadSystems.Clear();
            _changeSystems.Clear();
            _destroySystems.Clear();
            _deserializeSystems.Clear();
            foreach (Type type in GetTypes(typeof(ObjectSystemAttribute)))
            {
                object obj = Activator.CreateInstance(type);
                switch (obj)
                {
                    case IAwakeSystem objectSystem:
                        _awakeSystems.Add(objectSystem.Type(), objectSystem);
                        break;
                    case IUpdateSystem updateSystem:
                        _updateSystems.Add(updateSystem.Type(), updateSystem);
                        break;
                    case ILateUpdateSystem lateUpdateSystem:
                        _lateUpdateSystems.Add(lateUpdateSystem.Type(), lateUpdateSystem);
                        break;
                    case IStartSystem startSystem:
                        _startSystems.Add(startSystem.Type(), startSystem);
                        break;
                    case IDestroySystem destroySystem:
                        _destroySystems.Add(destroySystem.Type(), destroySystem);
                        break;
                    case ILoadSystem loadSystem:
                        _loadSystems.Add(loadSystem.Type(), loadSystem);
                        break;
                    case IChangeSystem changeSystem:
                        _changeSystems.Add(changeSystem.Type(), changeSystem);
                        break;
                    case IDeserializeSystem deserializeSystem:
                        _deserializeSystems.Add(deserializeSystem.Type(), deserializeSystem);
                        break;
                }
            }

            // 注册所有事件处理器
            _allEvents.Clear();
            foreach (Type type in _types[typeof(EventAttribute)])
            {
                IEvent obj = Activator.CreateInstance(type) as IEvent;
                if (obj == null)
                {
                    throw new Exception($"type not is AEvent: {obj.GetType().Name}");
                }

                Type eventType = obj.GetEventType();
                if (!_allEvents.ContainsKey(eventType))
                {
                    _allEvents.Add(eventType, new List<object>());
                }

                _allEvents[eventType].Add(obj);
            }

            // 
            Load();
        }

        public Assembly GetAssembly(string name)
        {
            return _assemblies[name];
        }

        public HashSet<Type> GetTypes(Type systemAttributeType)
        {
            if (!_types.ContainsKey(systemAttributeType))
            {
                return new HashSet<Type>();
            }

            return _types[systemAttributeType];
        }

        public List<Type> GetTypes()
        {
            List<Type> allTypes = new List<Type>();
            foreach (Assembly assembly in _assemblies.Values)
            {
                allTypes.AddRange(assembly.GetTypes());
            }

            return allTypes;
        }

        public Type GetType(string typeName)
        {
            return typeof(Game).Assembly.GetType(typeName);
        }

        public void RegisterSystem(Entity component, bool isRegister = true)
        {
            if (!isRegister)
            {
                Remove(component.InstanceId);
                return;
            }

            _allComponents.Add(component.InstanceId, component);

            Type type = component.GetType();

            if (_loadSystems.ContainsKey(type))
            {
                _loaders.Enqueue(component.InstanceId);
            }

            if (_updateSystems.ContainsKey(type))
            {
                _updates.Enqueue(component.InstanceId);
            }

            if (_startSystems.ContainsKey(type))
            {
                _starts.Enqueue(component.InstanceId);
            }

            if (_lateUpdateSystems.ContainsKey(type))
            {
                _lateUpdates.Enqueue(component.InstanceId);
            }
        }

        public void Remove(long instanceId)
        {
            _allComponents.Remove(instanceId);
        }

        public Entity Get(long instanceId)
        {
            Entity component = null;
            _allComponents.TryGetValue(instanceId, out component);
            return component;
        }

        public bool IsRegister(long instanceId)
        {
            return _allComponents.ContainsKey(instanceId);
        }

        public void Deserialize(Entity component)
        {
            List<IDeserializeSystem> iDeserializeSystems = _deserializeSystems[component.GetType()];
            if (iDeserializeSystems == null)
            {
                return;
            }

            foreach (IDeserializeSystem deserializeSystem in iDeserializeSystems)
            {
                if (deserializeSystem == null)
                {
                    continue;
                }

                try
                {
                    deserializeSystem.Run(component);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void Awake(Entity component)
        {
            List<IAwakeSystem> iAwakeSystems = _awakeSystems[component.GetType()];
            if (iAwakeSystems == null)
            {
                return;
            }

            foreach (IAwakeSystem aAwakeSystem in iAwakeSystems)
            {
                if (aAwakeSystem == null)
                {
                    continue;
                }

                IAwake iAwake = aAwakeSystem as IAwake;
                if (iAwake == null)
                {
                    continue;
                }

                try
                {
                    iAwake.Run(component);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void Awake<P1>(Entity component, P1 p1)
        {
            List<IAwakeSystem> iAwakeSystems = _awakeSystems[component.GetType()];
            if (iAwakeSystems == null)
            {
                return;
            }

            foreach (IAwakeSystem aAwakeSystem in iAwakeSystems)
            {
                if (aAwakeSystem == null)
                {
                    continue;
                }

                IAwake<P1> iAwake = aAwakeSystem as IAwake<P1>;
                if (iAwake == null)
                {
                    continue;
                }

                try
                {
                    iAwake.Run(component, p1);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void Awake<P1, P2>(Entity component, P1 p1, P2 p2)
        {
            List<IAwakeSystem> iAwakeSystems = _awakeSystems[component.GetType()];
            if (iAwakeSystems == null)
            {
                return;
            }

            foreach (IAwakeSystem aAwakeSystem in iAwakeSystems)
            {
                if (aAwakeSystem == null)
                {
                    continue;
                }

                IAwake<P1, P2> iAwake = aAwakeSystem as IAwake<P1, P2>;
                if (iAwake == null)
                {
                    continue;
                }

                try
                {
                    iAwake.Run(component, p1, p2);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void Awake<P1, P2, P3>(Entity component, P1 p1, P2 p2, P3 p3)
        {
            List<IAwakeSystem> iAwakeSystems = _awakeSystems[component.GetType()];
            if (iAwakeSystems == null)
            {
                return;
            }

            foreach (IAwakeSystem aAwakeSystem in iAwakeSystems)
            {
                if (aAwakeSystem == null)
                {
                    continue;
                }

                IAwake<P1, P2, P3> iAwake = aAwakeSystem as IAwake<P1, P2, P3>;
                if (iAwake == null)
                {
                    continue;
                }

                try
                {
                    iAwake.Run(component, p1, p2, p3);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void Awake<P1, P2, P3, P4>(Entity component, P1 p1, P2 p2, P3 p3, P4 p4)
        {
            List<IAwakeSystem> iAwakeSystems = _awakeSystems[component.GetType()];
            if (iAwakeSystems == null)
            {
                return;
            }

            foreach (IAwakeSystem aAwakeSystem in iAwakeSystems)
            {
                if (aAwakeSystem == null)
                {
                    continue;
                }

                IAwake<P1, P2, P3, P4> iAwake = aAwakeSystem as IAwake<P1, P2, P3, P4>;
                if (iAwake == null)
                {
                    continue;
                }

                try
                {
                    iAwake.Run(component, p1, p2, p3, p4);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void Change(Entity component)
        {
            List<IChangeSystem> iChangeSystems = _changeSystems[component.GetType()];
            if (iChangeSystems == null)
            {
                return;
            }

            foreach (IChangeSystem iChangeSystem in iChangeSystems)
            {
                if (iChangeSystem == null)
                {
                    continue;
                }

                try
                {
                    iChangeSystem.Run(component);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void Load()
        {
            while (_loaders.Count > 0)
            {
                long instanceId = _loaders.Dequeue();
                Entity component;
                if (!_allComponents.TryGetValue(instanceId, out component))
                {
                    continue;
                }

                if (component.IsDisposed)
                {
                    continue;
                }

                List<ILoadSystem> iLoadSystems = _loadSystems[component.GetType()];
                if (iLoadSystems == null)
                {
                    continue;
                }

                _loaders2.Enqueue(instanceId);

                foreach (ILoadSystem iLoadSystem in iLoadSystems)
                {
                    try
                    {
                        iLoadSystem.Run(component);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }
            }

            ObjectHelper.Swap(ref _loaders, ref _loaders2);
        }

        private void Start()
        {
            while (_starts.Count > 0)
            {
                long instanceId = _starts.Dequeue();
                Entity component;
                if (!_allComponents.TryGetValue(instanceId, out component))
                {
                    continue;
                }

                List<IStartSystem> iStartSystems = _startSystems[component.GetType()];
                if (iStartSystems == null)
                {
                    continue;
                }

                foreach (IStartSystem iStartSystem in iStartSystems)
                {
                    try
                    {
                        iStartSystem.Run(component);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }
            }
        }

        public void Destroy(Entity component)
        {
            List<IDestroySystem> iDestroySystems = _destroySystems[component.GetType()];
            if (iDestroySystems == null)
            {
                return;
            }

            foreach (IDestroySystem iDestroySystem in iDestroySystems)
            {
                if (iDestroySystem == null)
                {
                    continue;
                }

                try
                {
                    iDestroySystem.Run(component);
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }
        }

        public void Update()
        {
            Start();

            while (_updates.Count > 0)
            {
                long instanceId = _updates.Dequeue();
                Entity component;
                if (!_allComponents.TryGetValue(instanceId, out component))
                {
                    continue;
                }

                if (component.IsDisposed)
                {
                    continue;
                }

                List<IUpdateSystem> iUpdateSystems = _updateSystems[component.GetType()];
                if (iUpdateSystems == null)
                {
                    continue;
                }

                _updates2.Enqueue(instanceId);

                foreach (IUpdateSystem iUpdateSystem in iUpdateSystems)
                {
                    try
                    {
                        iUpdateSystem.Run(component);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }
            }

            ObjectHelper.Swap(ref _updates, ref _updates2);
        }

        public void LateUpdate()
        {
            while (_lateUpdates.Count > 0)
            {
                long instanceId = _lateUpdates.Dequeue();
                Entity component;
                if (!_allComponents.TryGetValue(instanceId, out component))
                {
                    continue;
                }

                if (component.IsDisposed)
                {
                    continue;
                }

                List<ILateUpdateSystem> iLateUpdateSystems = _lateUpdateSystems[component.GetType()];
                if (iLateUpdateSystems == null)
                {
                    continue;
                }

                _lateUpdates2.Enqueue(instanceId);

                foreach (ILateUpdateSystem iLateUpdateSystem in iLateUpdateSystems)
                {
                    try
                    {
                        iLateUpdateSystem.Run(component);
                    }
                    catch (Exception e)
                    {
                        Log.Error(e);
                    }
                }
            }

            ObjectHelper.Swap(ref _lateUpdates, ref _lateUpdates2);
        }

        /// <summary>
        /// 发布事件
        /// </summary>
        /// <param name="a"></param>
        /// <typeparam name="T"></typeparam>
        public async ETTask Publish<T>(T a) where T : struct
        {
            if (!_allEvents.TryGetValue(typeof(T), out List<object> iEvents))
            {
                return;
            }

            using var list = ListComponent<ETTask>.Create();
            
            foreach (object obj in iEvents)
            {
                if (!(obj is BaseEvent<T> aEvent))
                {
                    Log.Error($"event error: {obj.GetType().Name}");
                    continue;
                }

                list.List.Add(aEvent.Handle(a));
            }

            try
            {
                await ETTaskHelper.WaitAll(list.List);
            }
            catch (Exception e)
            {
                Log.Error(e);
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            HashSet<Type> noParent = new HashSet<Type>();
            Dictionary<Type, int> typeCount = new Dictionary<Type, int>();

            HashSet<Type> noDomain = new HashSet<Type>();

            foreach (var kv in _allComponents)
            {
                Type type = kv.Value.GetType();
                if (kv.Value.Parent == null)
                {
                    noParent.Add(type);
                }

                if (kv.Value.Domain == null)
                {
                    noDomain.Add(type);
                }

                if (typeCount.ContainsKey(type))
                {
                    typeCount[type]++;
                }
                else
                {
                    typeCount[type] = 1;
                }
            }

            sb.AppendLine("not set parent type: ");
            foreach (Type type in noParent)
            {
                sb.AppendLine($"\t{type.Name}");
            }

            sb.AppendLine("not set domain type: ");
            foreach (Type type in noDomain)
            {
                sb.AppendLine($"\t{type.Name}");
            }

            IOrderedEnumerable<KeyValuePair<Type, int>> orderByDescending = typeCount.OrderByDescending(s => s.Value);

            sb.AppendLine("Entity Count: ");
            foreach (var kv in orderByDescending)
            {
                if (kv.Value == 1)
                {
                    continue;
                }

                sb.AppendLine($"\t{kv.Key.Name}: {kv.Value}");
            }

            return sb.ToString();
        }

        public void Dispose()
        {
            _instance = null;
        }
    }
}