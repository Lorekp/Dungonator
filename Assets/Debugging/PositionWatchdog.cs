using UnityEngine;

namespace Debugging
{
    public class PositionWatchdog : MonoBehaviour
    {
        private Vector3 _last;
        private bool _reported;

        private void Awake() => _last = transform.position;

        private void LateUpdate()
        {
            if (_reported) return;
            if (transform.position != _last)
            {
                _reported = true;
                Debug.LogError($"Position changed from {_last} to {transform.position}\n{new System.Diagnostics.StackTrace(1, true)}");
            }
            _last = transform.position;
        }
    }
}
