using System.Collections.Generic;

namespace ET
{
    /// <summary>
    /// 封装List，用于重用
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ListComponent<T>: Object
    {
        private bool _isDispose;

        public static ListComponent<T> Create()
        {
            ListComponent<T> listComponent = ObjectPool.Instance.Fetch<ListComponent<T>>();
            listComponent._isDispose = false;
            return listComponent;
        }

        public List<T> List { get; } = new List<T>();

        public override void Dispose()
        {
            if (this._isDispose)
            {
                return;
            }

            _isDispose = true;

            base.Dispose();

            List.Clear();
            ObjectPool.Instance.Recycle(this);
        }
    }

    public class ListComponentDisposeChildren<T>: Object where T : Object
    {
        private bool _isDispose;

        public static ListComponentDisposeChildren<T> Create()
        {
            ListComponentDisposeChildren<T> listComponent = ObjectPool.Instance.Fetch<ListComponentDisposeChildren<T>>();
            listComponent._isDispose = false;
            return listComponent;
        }

        public List<T> List = new List<T>();

        public override void Dispose()
        {
            if (this._isDispose)
            {
                return;
            }

            this._isDispose = true;

            base.Dispose();

            foreach (T entity in List)
            {
                entity.Dispose();
            }

            List.Clear();

            ObjectPool.Instance.Recycle(this);
        }
    }
}