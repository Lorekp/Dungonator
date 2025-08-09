using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using GameKit.Dependencies.Utilities;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Dungonator.Shared.Player
{
    public class PlayerPrediction : NetworkBehaviour
    {
        [SerializeField] private float _moveForce = 15f;
        private Vector2 _moveInput;
        
        public PredictionRigidbody2D PredictionRigidbody;

        private void Awake()
        {
            PredictionRigidbody = ObjectCaches<PredictionRigidbody2D>.Retrieve();
            PredictionRigidbody.Initialize(GetComponent<Rigidbody2D>());
        }

        private void OnDestroy()
        {
            ObjectCaches<PredictionRigidbody2D>.StoreAndDefault(ref PredictionRigidbody);
        }

        public override void OnStartNetwork()
        {
            base.OnStartNetwork();
            PredictionRigidbody.Velocity(Vector3.zero);
            PredictionRigidbody.AngularVelocity(0f);
            
            base.TimeManager.OnTick += TimeManager_OnTick;
            base.TimeManager.OnPostTick += TimeManager_OnPostTick;
        }
        
        public override void OnStopNetwork()
        {
            base.TimeManager.OnTick -= TimeManager_OnTick;
            base.TimeManager.OnPostTick -= TimeManager_OnPostTick;
        }

        private void TimeManager_OnTick()
        {
            if (IsOwner)
                RunInputs(CreateReplicateData(_moveInput));
        }

        private void TimeManager_OnPostTick()
        {
            CreateReconcile();
        }
        
        public void OnMove(InputValue value)
        {
            _moveInput = value.Get<Vector2>();
        }

        public override void CreateReconcile()
        {
            //We must send back the state of the rigidbody. Using your
            //PredictionRigidbody field in the reconcile data is an easy
            //way to accomplish this. More advanced states may require other
            //values to be sent; this will be covered later on.
            var rd = new ReconcileData(PredictionRigidbody);
            //Like with the replicate you could specify a channel here, though
            //it's unlikely you ever would with a reconcile.
            ReconcileState(rd);
        }

        private ReplicatePlayerData CreateReplicateData(Vector2 direction)
        {
            if (!base.IsOwner)
                return default;
            
            ReplicatePlayerData md = new(direction.x, direction.y);

            return md;
        }

        [Replicate]
        private void RunInputs(ReplicatePlayerData data, ReplicateState state = ReplicateState.Invalid,
            Channel channel = Channel.Unreliable)
        {
            /* ReplicateState is set based on if the data is new, being replayed, ect.
             * Visit the ReplicationState enum for more information on what each value
             * indicates. At the end of this guide a more advanced use of state will
             * be demonstrated. */
    
            //Be sure to always apply and set velocties using PredictionRigidbody
            //and never on the rigidbody itself; this includes if also accessing from
            //another script.
            var forces = new Vector2(data.Horizontal, data.Vertical) * _moveForce;
            
            PredictionRigidbody.AddForce(forces); 

            //Simulate the added forces.
            //Typically you call this at the end of your replicate. Calling
            //Simulate is ultimately telling the PredictionRigidbody to iterate
            //the forces we added above.
            PredictionRigidbody.Simulate();
        }

        [Reconcile]
        private void ReconcileState(ReconcileData data, Channel channel = Channel.Unreliable)
        {
            PredictionRigidbody.Reconcile(data.PredictionRigidbody);
        }
    }
    
    public struct ReplicatePlayerData : IReplicateData
    {
        public float Horizontal;
        public float Vertical;
        public ReplicatePlayerData(float horizontal, float vertical) : this()
        {
            Horizontal = horizontal;
            Vertical = vertical;
        }

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }
        
    public struct ReconcileData : IReconcileData
    {
        //PredictionRigidbody is used to synchronize rigidbody states
        //and forces. This could be done manually but the PredictionRigidbody
        //type makes this process considerably easier. Velocities, kinematic state,
        //transform properties, pending velocities and more are automatically
        //handled with PredictionRigidbody.
        public PredictionRigidbody2D PredictionRigidbody;
    
        public ReconcileData(PredictionRigidbody2D pr) : this()
        {
            PredictionRigidbody = pr;
        }

        private uint _tick;
        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }
}
