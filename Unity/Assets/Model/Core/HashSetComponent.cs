using System.Collections.Generic;

namespace ET
{
    public class HashSetComponent<T>: Object
    {
        private bool _isDispose;
        
        public static HashSetComponent<T> Create()
        {
            HashSetComponent<T> hashSetComponent = ObjectPool.Instance.Fetch<HashSetComponent<T>>();
            hashSetComponent._isDispose = false;
            return hashSetComponent;
        }
        
        public HashSet<T> Set = new HashSet<T>();

        public override void Dispose()
        {
            if (_isDispose)
                return;

            _isDispose = true;
            
            base.Dispose();
            
            Set.Clear();
            ObjectPool.Instance.Recycle(this);
        }
    }
}