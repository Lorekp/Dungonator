using System;
using FishNet.Object;
using UnityEngine;

namespace Dungonator.Shared.Helpers
{
    public class EventBroadcaster : NetworkBehaviour
    {
        public static event Action<NetworkBehaviour> OnStartClientEvent;

        public override void OnStartClient()
        {
            base.OnStartClient();
            OnStartClientEvent?.Invoke(this);
        }
    }
}
