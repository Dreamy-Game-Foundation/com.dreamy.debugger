namespace Dreamy.Debugger.Foundation.Service
{
    public abstract class ServiceBase<T> : ComponentBehaviour where T : class
    {
        protected override void Awake()
        {
            base.Awake();
            ServiceManager.RegisterService<T>(this);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if(ServiceManager.HasService<T>() && ReferenceEquals(ServiceManager.GetService<T>(),this))ServiceManager.UnRegisterService<T>();
        }
    }
}
