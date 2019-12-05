using System;
using System.Reflection;

namespace VortexHarmonyInstaller.Util
{
    public static class Singleton<T> where T: class
    {
        static volatile T m_instance;
        static object m_lock = new object();

        static Singleton()
        {
        }

        public static T Instance
        {
            get
            {
                if (m_instance == null)
                {
                    lock (m_lock)
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

                        m_instance = (T)constructor.Invoke(null);
                    }
                }

                return m_instance;
            }
        }
    }
}
