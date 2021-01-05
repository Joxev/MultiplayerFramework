using FirstGearGames.AuthoritativeMovement.Managers.Global.MilestoneUDP;
using FirstGearGames.AuthoritativeMovement.States.MiletoneUDP;
using FirstGearGames.Utilities.Maths;
using Mirror;
using System.Collections.Generic;
using UnityEngine;

namespace FirstGearGames.AuthoritativeMovement.Motors.MiletoneUDP
{

    public class Motor : NetworkBehaviour
    {
        #region Types.
        /// <summary>
        /// Inputs which can be stored.
        /// </summary>
        private class InputData
        {
            public bool Jump = false;
        }
        #endregion

        #region Serialized.
        /// <summary>
        /// Move rate for the rigidbody.
        /// </summary>
        [Tooltip("Move rate for the rigidbody.")]
        [SerializeField]
        private float _moveRate = 3f;
        /// <summary>
        /// How much force to apply as impulse when jumping.
        /// </summary>
        [Tooltip("How much force to apply as impulse when jumping.")]
        [SerializeField]
        private float _jumpImpulse = 8f;
        #endregion

        #region Private.
        /// <summary>
        /// Rigidbody on this object.
        /// </summary>
        private Rigidbody _rigidbody = null;
        /// <summary>
        /// Stored client motor states.
        /// </summary>
        private List<ClientMotorState> _clientMotorStates = new List<ClientMotorState>();
        /// <summary>
        /// Motor states received from the client.
        /// </summary>
        private Queue<ClientMotorState> _receivedClientMotorStates = new Queue<ClientMotorState>();
        /// <summary>
        /// Last FixedFrame processed from client.
        /// </summary>
        private uint _lastClientStateReceived = 0;
        /// <summary>
        /// Most current motor state received from the server.
        /// </summary>
        private ServerMotorState? _receivedServerMotorState = null;
        /// <summary>
        /// Inputs stored from Update.
        /// </summary>
        private InputData _storedInputs = new InputData();
        #endregion

        #region Const
        /// <summary>
        /// Maximum number of entries that may be held within ReceivedClientMotorStates.
        /// </summary>
        private const int MAXIMUM_RECEIVED_CLIENT_MOTOR_STATES = 10;
        /// <summary>
        /// How many past states to send to the server.
        /// </summary>
        private const int PAST_STATES_TO_SEND = 3;
        #endregion

        private void Awake()
        {
            FirstInitialize();
        }

        private void OnEnable()
        {
            FixedUpdateManager.OnFixedUpdate += FixedUpdateManager_OnFixedUpdate;
        }

        private void OnDisable()
        {
            FixedUpdateManager.OnFixedUpdate -= FixedUpdateManager_OnFixedUpdate;
        }

        private void Update()
        {
            if (base.hasAuthority)
            {
                CheckJump();
            }
        }

        /// <summary>
        /// Received when a simulated fixed update occurs.
        /// </summary>
        private void FixedUpdateManager_OnFixedUpdate()
        {
            if (!base.hasAuthority && !base.isServer)
            {
                CancelVelocity(false);
            }

            if (base.hasAuthority)
            {
                ProcessReceivedServerMotorState();
                SendInputs();
            }

            if (base.isServer)
            {
                ProcessReceivedClientMotorState();
            }

        }


        /// <summary>
        /// Initializes this script for use. Should only be called once.
        /// </summary>
        private void FirstInitialize()
        {
            _rigidbody = GetComponent<Rigidbody>();
        }


        /// <summary>
        /// Cancels velocity on the rigidbody.
        /// </summary>
        /// <param name="useForces"></param>
        [Client]
        private void CancelVelocity(bool useForces)
        {
            if (useForces)
            {
                _rigidbody.AddForce(-_rigidbody.velocity, ForceMode.Impulse);
                _rigidbody.AddTorque(-_rigidbody.angularVelocity, ForceMode.Impulse);
            }
            else
            {
                _rigidbody.velocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Checks if jump is pressed.
        /// </summary>
        [Client]
        private void CheckJump()
        {
            if (Input.GetKeyDown(KeyCode.Space))
                _storedInputs.Jump = true;
        }

        /// <summary>
        /// Processes the last received server motor state.
        /// </summary>
        [Client]
        private void ProcessReceivedServerMotorState()
        {
            if (_receivedServerMotorState == null)
                return;

            ServerMotorState serverState = _receivedServerMotorState.Value;
            FixedUpdateManager.AddTiming(serverState.TimingStepChange);
            _receivedServerMotorState = null;

            //Remove entries which have been handled by the server.
            int index = _clientMotorStates.FindIndex(x => x.FixedFrame == serverState.FixedFrame);
            if (index != -1)
                _clientMotorStates.RemoveRange(0, index);

            //Snap motor to server values.
            transform.position = serverState.Position;
            transform.rotation = serverState.Rotation;
            _rigidbody.velocity = serverState.Velocity;
            _rigidbody.angularVelocity = serverState.AngularVelocity;
            Physics.SyncTransforms();

            foreach (ClientMotorState clientState in _clientMotorStates)
            {
                ProcessInputs(clientState);
                Physics.Simulate(Time.fixedDeltaTime);
            }
        }

        /// <summary>
        /// Processes the last received client motor state.
        /// </summary>
        [Server]
        private void ProcessReceivedClientMotorState()
        {
            if (base.isClient && base.hasAuthority)
                return;

            sbyte timingStepChange = 0;

            /* If there are no states then set timing change step
            * to a negative value, which will speed up the client
            * simulation. In result this will increase the chances
            * the client will send a packet which will arrive by every
            * fixed on the server. */
            if (_receivedClientMotorStates.Count == 0)
                timingStepChange = -1;
            /* Like subtracting a step, if there is more than one entry
            * then the client is sending too fast. Send a positive step
            * which will slow the clients send rate. */
            else if (_receivedClientMotorStates.Count > 1)
                timingStepChange = 1;

            //If there is input to process.
            if (_receivedClientMotorStates.Count > 0)
            {
                ClientMotorState state = _receivedClientMotorStates.Dequeue();
                //Process input of last received motor state.
                ProcessInputs(state);

                ServerMotorState responseState = new ServerMotorState
                {
                    FixedFrame = state.FixedFrame,
                    Position = transform.position,
                    Rotation = transform.rotation,
                    Velocity = _rigidbody.velocity,
                    AngularVelocity = _rigidbody.angularVelocity,
                    TimingStepChange = timingStepChange
                };

                //Send results back to the owner.
                TargetServerStateUpdate(base.connectionToClient, responseState);
            }
            //If there is no input to process.
            else if (timingStepChange != 0)
            {
                //Send timing step change to owner.
                TargetChangeTimingStep(base.connectionToClient, timingStepChange);
            }
        }

        /// <summary>
        /// Processes input from a state.
        /// </summary>
        /// <param name="motorState"></param>
        private void ProcessInputs(ClientMotorState motorState)
        {
            motorState.Horizontal = Floats.PreciseSign(motorState.Horizontal);
            motorState.Forward = Floats.PreciseSign(motorState.Forward);

            Vector3 forces = new Vector3(motorState.Horizontal, 0f, motorState.Forward) * _moveRate;
            _rigidbody.AddForce(forces, ForceMode.Force);

            Vector3 impulses = Vector3.zero;
            if ((ActionCodes)motorState.ActionCodes == ActionCodes.Jump)
                impulses += (Vector3.up * _jumpImpulse);
            _rigidbody.AddForce(impulses, ForceMode.Impulse);
        }

        /// <summary>
        /// Sends inputs for the client.
        /// </summary>
        [Client]
        private void SendInputs()
        {
            float horizontal = Input.GetAxisRaw("Horizontal");
            float forward = Input.GetAxisRaw("Vertical");

            /* Action Codes. */
            ActionCodes ac = ActionCodes.None;
            if (_storedInputs.Jump)
            {
                _storedInputs.Jump = false;
                ac |= ActionCodes.Jump;
            }

            ClientMotorState state = new ClientMotorState
            {
                FixedFrame = FixedUpdateManager.FixedFrame,
                Horizontal = horizontal,
                Forward = forward,
                ActionCodes = (byte)ac
            };
            _clientMotorStates.Add(state);

            //Only send at most up to client motor states count.
            int targetArraySize = Mathf.Min(_clientMotorStates.Count, 1 + PAST_STATES_TO_SEND);
            //Resize array to accomodate 
            ClientMotorState[] statesToSend = new ClientMotorState[targetArraySize];
            /* Start at the end of cached inputs, and add to the end of inputs to send.
             * This will add the older inputs first. */
            for (int i = 0; i < targetArraySize; i++)
            {
                //Add from the end of states first.
                statesToSend[targetArraySize - 1 - i] = _clientMotorStates[_clientMotorStates.Count - 1 - i];
            }

            ProcessInputs(state);
            CmdSendInputs(statesToSend);
        }

        /// <summary>
        /// Send inputs from client to server.
        /// </summary>
        /// <param name="states"></param>
        [Command(channel = 1)]
        private void CmdSendInputs(ClientMotorState[] states)
        {
            //No states to process.
            if (states == null || states.Length == 0)
                return;
            //Only for client host.
            if (base.isClient && base.hasAuthority)
                return;

            /* Go through every new state and if the fixed frame
             * for that state is newer than the last received
             * fixed frame then add it to motor states. */
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].FixedFrame > _lastClientStateReceived)
                { 
                    _receivedClientMotorStates.Enqueue(states[i]);
                    _lastClientStateReceived = states[i].FixedFrame;
                }
            }

            while (_receivedClientMotorStates.Count > MAXIMUM_RECEIVED_CLIENT_MOTOR_STATES)
                _receivedClientMotorStates.Dequeue();
        }

        /// <summary>
        /// Received on the owning client after the server processes ClientMotorState.
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="state"></param>
        [TargetRpc(channel = 1)]
        private void TargetServerStateUpdate(NetworkConnection conn, ServerMotorState state)
        {
            //Exit if received state is older than most current.
            if (_receivedServerMotorState != null && state.FixedFrame < _receivedServerMotorState.Value.FixedFrame)
                return;

            _receivedServerMotorState = state;
        }

        /// <summary>
        /// Received on the owning client after server fails to process any inputs.
        /// </summary>
        /// <param name="state"></param>
        [TargetRpc(channel = 1)]
        private void TargetChangeTimingStep(NetworkConnection conn, sbyte steps)
        {
            FixedUpdateManager.AddTiming(steps);
        }
    }

}