using System;

namespace VortexHarmonyInstaller.Delegates
{
    public class MonoBehaviourHooks
    {
        public delegate void OnAwake();
        public OnAwake Awake = null;

        public delegate void OnStart();
        public OnStart Start = null;

        public delegate void OnReset();
        public OnReset Reset = null;

        public delegate void OnEnable();
        public OnEnable Enable = null;

        public delegate void OnDisable();
        public OnDisable Disable = null;

        public delegate void OnDestroy();
        public OnDestroy Destroy = null;

        public delegate void OnUpdate();
        public OnUpdate Update = null;

        public delegate void OnFixedUpdate();
        public OnFixedUpdate FixedUpdate = null;

        public delegate void OnLateUpdate();
        public OnLateUpdate LateUpdate = null;
    }
}
