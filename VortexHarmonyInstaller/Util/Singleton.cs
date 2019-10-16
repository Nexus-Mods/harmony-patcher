using System;
using System.Reflection;

namespace VortexHarmonyInstaller.Util
{
    public static class Singleton<T> where T: class
    {
        static volatile T _instance;
        static object _lock = new object();

        static Singleton()
        {
        }

        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        ConstructorInfo constructor = null;
                        try
                        {
                            constructor = typeof(T).GetConstructor(
                                BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[0], null);
                        } catch (Exception exc)
                        {
                            throw new ReflectionTypeLoadException(new Type[] { typeof(T) }, 
                                new Exception[] { exc });
                        }

                        if ((constructor == null) || (constructor.IsAssembly))
                        {
                            throw new ReflectionTypeLoadException(new Type[] { typeof(T) },
                                new Exception[] { new Exception(
                                    string.Format("Constructor is missing for '{0}'", typeof(T).Name)) });
                        }

                        _instance = (T)constructor.Invoke(null);
                    }
                }

                return _instance;
            }
        }
    }
}
