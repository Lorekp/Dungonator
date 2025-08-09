using UnityEngine;

namespace Dungonator.Shared.Helpers
{
    /// <summary>
    /// Utility extensions for GameObject.
    /// </summary>
    public static class GameObjectExtensions
    {
        /// <summary>
        /// Adds <typeparamref name="T"/> to <paramref name="go"/> only if it isn’t already present.
        /// </summary>
        public static T GetOrAddComponent<T>(this GameObject go) where T : Component
        {
            if (!go.TryGetComponent<T>(out var comp))
                comp = go.AddComponent<T>();
            return comp;
        }
    }
}