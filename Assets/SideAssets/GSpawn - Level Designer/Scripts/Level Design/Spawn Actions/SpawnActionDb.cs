#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace GSPAWN
{
    public class SpawnActionDb : ScriptableObject
    {
        [SerializeField] private List<SpawnAction> _actions = new List<SpawnAction>();

        public List<SpawnAction> actions { get { return _actions; } }

        public void addAction(SpawnAction action)
        {
            if (action && !_actions.Contains(action)) _actions.Add(action);
        }

        public void removeAction(SpawnAction action)
        {
            _actions.Remove(action);
        }
    }
}
#endif
