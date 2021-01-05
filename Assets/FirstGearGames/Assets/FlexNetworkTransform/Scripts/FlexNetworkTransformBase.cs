using FirstGearGames.Utilities.Editors;
using FirstGearGames.Utilities.Maths;
using FirstGearGames.Utilities.Objects;
using Mirror;
using System;
using UnityEngine;

namespace FirstGearGames.Mirrors.Assets.FlexNetworkTransforms
{


    public abstract class FlexNetworkTransformBase : NetworkBehaviour
    {
        #region Types.
        public class PlatformData
        {
            /// <summary>
            /// Platform.
            /// </summary>
            public NetworkIdentity Identity;
            /// <summary>
            /// Last position for the platform.
            /// </summary>
            public Vector3? LastPosition;
            /// <summary>
            /// Last rotation for the platform.
            /// </summary>
            public Quaternion? LastRotation;
        }
        /// <summary>
        /// Extrapolation for the most recent received data.
        /// </summary>
        protected class ExtrapolationData
        {
            public float Remaining;
            public Vector3 Position;
        }
        /// <summary>
        /// Move rates for the most recent received data.
        /// </summary>
        protected struct MoveRateData
        {
            public float Position;
            public float Rotation;
            public float Scale;
        }
        /// <summary>
        /// Data used to manage moving towards a target.
        /// </summary>
        protected class TargetSyncData
        {
            public TargetSyncData(TransformSyncData goalData, MoveRateData moveRates, ExtrapolationData extrapolationData)
            {
                GoalData = goalData;
                MoveRates = moveRates;
                Extrapolation = extrapolationData;
            }

            /// <summary>
            /// Transform goal data for this update.
            /// </summary>
            public readonly TransformSyncData GoalData;
            /// <summary>
            /// How quickly to move towards each transform property.
            /// </summary>
            public readonly MoveRateData MoveRates;
            /// <summary>
            /// How much extrapolation time remains.
            /// </summary>
            public readonly ExtrapolationData Extrapolation;
        }
        /// <summary>
        /// Ways to synchronize datas.
        /// </summary>
        [System.Serializable]
        private enum SynchronizeTypes : int
        {
            Normal = 0,
            NoSynchronization = 1
        }
        /// <summary>
        /// Interval types to determine when to synchronize data.
        /// </summary>
        [System.Serializable]
        private enum IntervalTypes : int
        {
            Timed = 0,
            FixedUpdate = 1
        }
        #endregion

        #region Public.
        /// <summary>
        /// Dispatched when server receives data from a client while using client authoritative.
        /// </summary>
        public event Action<ReceivedClientData> OnClientDataReceived;
        /// <summary>
        /// Transform to monitor and modify.
        /// </summary>
        public abstract Transform TargetTransform { get; }
        /// <summary>
        /// PlatformData for the platform this object resides on.
        /// </summary>
        public PlatformData Platform { get; private set; } = new PlatformData();
        /// <summary>
        /// Sets which platform this transform is on.
        /// </summary>
        /// <param name="platform"></param>
        protected void SetPlatformInternal(NetworkIdentity platform)
        {
            uint netId = (platform == null) ? 0 : platform.netId;
            UpdatePlatform(netId);
        }

        #endregion

        #region Serialized.
        /// <summary>
        /// 
        /// </summary>
        [Tooltip("True to synchronize using localSpace rather than worldSpace. If you are to child this object throughout it's lifespan using worldspace is recommended. However, when using worldspace synchronization may not behave properly on VR. LocalSpace is the default.")]
        [SerializeField]
        private bool _useLocalSpace = true;
        /// <summary>
        /// True to synchronize using localSpace rather than worldSpace. If you are to child this object throughout it's lifespan using worldspace is recommended. However, when using worldspace synchronization may not behave properly on VR. LocalSpace is the default.
        /// </summary>
        protected bool UseLocalSpace { get { return _useLocalSpace; } }
        /// <summary>
        /// How to operate synchronization timings. Timed will synchronized every specified interval while FixedUpdate will synchronize every FixedUpdate.
        /// </summary>
        [Tooltip("How to operate synchronization timings. Timed will synchronized every specified interval while FixedUpdate will synchronize every FixedUpdate.")]
        [SerializeField]
        private IntervalTypes _intervalType = IntervalTypes.Timed;
        /// <summary>
        /// How often to synchronize this transform.
        /// </summary>
        [Tooltip("How often to synchronize this transform.")]
        [Range(0.01f, 0.5f)]
        [SerializeField]
        private float _synchronizeInterval = 0.1f;
        /// <summary>
        /// True to synchronize using the reliable channel. False to synchronize using the unreliable channel. Your project must use 0 as reliable, and 1 as unreliable for this to function properly. This feature is not supported on TCP transports.
        /// </summary>
        [Tooltip("True to synchronize using the reliable channel. False to synchronize using the unreliable channel. Your project must use 0 as reliable, and 1 as unreliable for this to function properly.")]
        [SerializeField]
        private bool _reliable = true;
        /// <summary>
        /// True to synchronize data anytime it has changed. False to allow greater differences before synchronizing.
        /// </summary>
        [Tooltip("True to synchronize data anytime it has changed. False to allow greater differences before synchronizing.")]
        [SerializeField]
        private bool _preciseSynchronization = false;
        /// <summary>
        /// How far in the past objects should be for interpolation. Higher values will result in smoother movement with network fluctuations but lower values will result in objects being closer to their actual position. Lower values can generally be used for longer synchronization intervalls.
        /// </summary>
        [Tooltip("How far in the past objects should be for interpolation. Higher values will result in smoother movement with network fluctuations but lower values will result in objects being closer to their actual position. Lower values can generally be used for longer synchronization intervals.")]
        [Range(0.00f, 0.5f)]
        [SerializeField]
        private float _interpolationFallbehind = 0.06f;
        /// <summary>
        /// How long to extrapolate when data is expected but does not arrive. Smaller values are best for fast synchronization intervals. For precision or fast reaction games you may want to use no extrapolation or only one or two synchronization intervals worth. Extrapolation is client-side only.
        /// </summary>
        [Tooltip("How long to extrapolate when data is expected but does not arrive. Smaller values are best for fast synchronization intervals. For precision or fast reaction games you may want to use no extrapolation or only one or two synchronization intervals worth. Extrapolation is client-side only.")]
        [Range(0f, 5f)]
        [SerializeField]
        private float _extrapolationSpan = 0f;
        /// <summary>
        /// Teleport the transform if the distance between received data exceeds this value. Use 0f to disable.
        /// </summary>
        [Tooltip("Teleport the transform if the distance between received data exceeds this value. Use 0f to disable.")]
        [SerializeField]
        private float _teleportThreshold = 0f;
        /// <summary>
        /// True if using client authoritative movement.
        /// </summary>
        [Tooltip("True if using client authoritative movement.")]
        [SerializeField]
        private bool _clientAuthoritative = true;
        /// <summary>
        /// True to synchronize server results back to owner. Typically used when you are sending inputs to the server and are relying on the server response to move the transform.
        /// </summary>
        [Tooltip("True to synchronize server results back to owner. Typically used when you are sending inputs to the server and are relying on the server response to move the transform.")]
        [SerializeField]
        private bool _synchronizeToOwner = true;
        /// <summary>
        /// Synchronize options for position.
        /// </summary>
        [Tooltip("Synchronize options for position.")]
        [SerializeField]
        private SynchronizeTypes _synchronizePosition = SynchronizeTypes.Normal;
        /// <summary>
        /// Euler axes on the position to snap into place rather than move towards over time.
        /// </summary>
        [Tooltip("Euler axes on the rotation to snap into place rather than move towards over time.")]
        [SerializeField]
        [BitMask(typeof(Axes))]
        private Axes _snapPosition = (Axes)0;
        /// <summary>
        /// Sets SnapPosition value. For internal use only. Must be public for editor script.
        /// </summary>
        /// <param name="value"></param>
        public void SetSnapPosition(Axes value) { _snapPosition = value; }
        /// <summary>
        /// Synchronize states for rotation.
        /// </summary>
        [Tooltip("Synchronize states for position.")]
        [SerializeField]
        private SynchronizeTypes _synchronizeRotation = SynchronizeTypes.Normal;
        /// <summary>
        /// Euler axes on the rotation to snap into place rather than move towards over time.
        /// </summary>
        [Tooltip("Euler axes on the rotation to snap into place rather than move towards over time.")]
        [SerializeField]
        [BitMask(typeof(Axes))]
        private Axes _snapRotation = (Axes)0;
        /// <summary>
        /// Sets SnapRotation value. For internal use only. Must be public for editor script.
        /// </summary>
        /// <param name="value"></param>
        public void SetSnapRotation(Axes value) { _snapRotation = value; }
        /// <summary>
        /// Synchronize states for scale.
        /// </summary>
        [Tooltip("Synchronize states for scale.")]
        [SerializeField]
        private SynchronizeTypes _synchronizeScale = SynchronizeTypes.Normal;
        /// <summary>
        /// Euler axes on the scale to snap into place rather than move towards over time.
        /// </summary>
        [Tooltip("Euler axes on the scale to snap into place rather than move towards over time.")]
        [SerializeField]
        [BitMask(typeof(Axes))]
        private Axes _snapScale = (Axes)0;
        /// <summary>
        /// Sets SnapScale value. For internal use only. Must be public for editor script.
        /// </summary>
        /// <param name="value"></param>
        public void SetSnapScale(Axes value) { _snapScale = value; }
        #endregion

        #region Private.
        /// <summary>
        /// Last SyncData sent by client.
        /// </summary>
        private TransformSyncData _clientSyncData = null;
        /// <summary>
        /// Last SyncData sent by server.
        /// </summary>
        private TransformSyncData _serverSyncData = null;
        /// <summary>
        /// TargetSyncData to move between.
        /// </summary>
        private TargetSyncData _targetData = null;
        /// <summary>
        /// Last SequenceId sent by the client.
        /// </summary>
        private uint _lastClientSentSequenceId = 0;
        /// <summary>
        /// Last SequenceId sent by the server.
        /// </summary>
        private uint _lastServerSentSequenceId = 0;
        /// <summary>
        /// Last SequenceId received from client.
        /// </summary>
        private uint _lastClientReceivedSequenceId = 0;
        /// <summary>
        /// Last SequenceId received from server.
        /// </summary>
        private uint _lastServerReceivedSequenceId = 0;
        /// <summary>
        /// Next time client may send data.
        /// </summary>
        private float _nextClientSendTime = 0f;
        /// <summary>
        /// Next time server may send data.
        /// </summary>
        private float _nextServerSendTime = 0f;
        /// <summary>
        /// When sending data from client, after the transform stops changing and when using unreliable this becomes true while a reliable packet is being sent.
        /// </summary>
        private bool _clientSettleSent = false;
        /// <summary>
        /// When sending data from server, after the transform stops changing and when using unreliable this becomes true while a reliable packet is being sent.
        /// </summary>
        private bool _serverSettleSent = false;
        /// <summary>
        /// Last frame FixedUpdate ran.
        /// </summary>
        private int _lastFixedFrame = -1;
        /// <summary>
        /// TeleportThreshold value squared.
        /// </summary>
        private float _teleportThresholdSquared;
        #endregion

        protected virtual void Awake()
        {
            SetTeleportThresholdSquared();
#if MIRRORNG
            base.NetIdentity.OnStartClient.AddListener(StartClient);
            base.NetIdentity.OnStartAuthority.AddListener(StartAuthority);
            base.NetIdentity.OnStartAuthority.AddListener(StopAuthority);
#endif
        }
        protected virtual void OnDestroy()
        {
#if MIRRORNG
            base.NetIdentity.OnStartClient.RemoveListener(StartClient);
            base.NetIdentity.OnStopAuthority.RemoveListener(StartAuthority);
            base.NetIdentity.OnStopAuthority.RemoveListener(StopAuthority);
#endif            
        }

        public override bool OnSerialize(NetworkWriter writer, bool initialState)
        {
            uint platformNetId = (PlatformValid()) ? Platform.Identity.netId : 0;
            writer.WriteUInt32(platformNetId);

            return base.OnSerialize(writer, initialState);
        }

        public override void OnDeserialize(NetworkReader reader, bool initialState)
        {
            /* Get current platform. */
            uint platformNetId = reader.ReadUInt32();
            if (platformNetId != 0)
            {
                UpdatePlatform(platformNetId);
                /* If not authoritative client with client auth, and platform
                 * exist then make a new target data based on transforms offsets
                 * to platform with instant move rates. This is so the transform 
                 * sticks to the platform until new data comes in. */
                if ((_clientAuthoritative && !base.hasAuthority) && PlatformValid())
                {
                    Vector3 positionOffset = Platform.Identity.transform.position - transform.position;
                    Quaternion rotationOffset = Platform.Identity.transform.rotation * Quaternion.Inverse(transform.rotation);
                    TransformSyncData goalData = new TransformSyncData((byte)ReturnConfiguredSynchronizedProperties(), 0, positionOffset, rotationOffset, transform.localScale, Platform.Identity.netId);
                    _targetData = new TargetSyncData(goalData, SetInstantMoveRates(), null);
                }
            }

            base.OnDeserialize(reader, initialState);
        }

#if MIRROR
        public override void OnStartClient()
        {
            base.OnStartClient();
            StartClient();
        }
#endif
        private void StartClient()
        {
            CheckCreateTransformTargetData();
        }


#if MIRROR
        public override void OnStartAuthority()
        {
            base.OnStartAuthority();
            StartAuthority();
        }
#endif
        private void StartAuthority()
        {
            /* If have authority and client authoritative then there is
            * no reason to have a targe data. */
            if (_clientAuthoritative)
                _targetData = null;
        }

#if MIRROR
        public override void OnStopAuthority()
        {
            base.OnStopAuthority();
            StopAuthority();
        }
#endif

        private void StopAuthority()
        {
            CheckCreateTransformTargetData();
        }

        protected virtual void Update()
        {
            CheckSendToServer();
            CheckSendToClients();
            MoveTowardsTargetSyncData();
        }

        private void FixedUpdate()
        {
            /* Don't send if the same frame. Since
             * physics aren't actually involved there is
             * no reason to run logic twice on the
             * same frame; that will only hurt performance
             * and the network more. */
            if (Time.frameCount == _lastFixedFrame)
                return;
            _lastFixedFrame = Time.frameCount;

            CheckSendToServer();
            CheckSendToClients();
        }

        /// <summary>
        /// Sets TeleportThresholdSquared value.
        /// </summary>
        private void SetTeleportThresholdSquared()
        {
            if (_teleportThreshold < 0f)
                _teleportThreshold = 0f;

            _teleportThresholdSquared = (_teleportThreshold * _teleportThreshold);
        }

        /// <summary>
        /// Checks if conditions are met to call CreateTransformTargetData.
        /// </summary>
        private void CheckCreateTransformTargetData()
        {
            /* If a client starts without being allowed to move the object a target data
            * must be set using current transform values so that the client may not move
             * the object. */
            /* If target data has not already been received.
             * If not server, since server is boss and shouldn't be blocked.
             * If client does not have authority or 
             * have authority but not client authoritative. */
            if (_targetData == null && !NetworkIsServer() && (!NetworkHasAuthority() || (NetworkHasAuthority() && !_clientAuthoritative)))
                CreateTransformTargetData();
        }

        /// <summary>
        /// Creates target data according to where the transform is currently.
        /// </summary>
        protected void CreateTransformTargetData()
        {
            /* Use a -1 sequence id so that it may be overriden by any
            * new data. */
            TransformSyncData tsd = new TransformSyncData(0, 0,
                TargetTransform.GetPosition(UseLocalSpace), TargetTransform.GetRotation(UseLocalSpace), TargetTransform.GetScale(),
                0
                );

            //Set move rates to move instantly to goal.
            MoveRateData mrd = SetInstantMoveRates();
            //Create new target data without extrpaolation.
            _targetData = new TargetSyncData(tsd, mrd, null);
        }

        /// <summary>
        /// Returns synchronization interval used.
        /// </summary>
        /// <returns></returns>
        private float ReturnSyncInterval()
        {
            return (_intervalType == IntervalTypes.FixedUpdate) ? Time.fixedDeltaTime : _synchronizeInterval;
        }

        /// <summary>
        /// Checks if client needs to send data to server.
        /// </summary>
        private void CheckSendToServer()
        {
            //Timed interval.
            if (_intervalType == IntervalTypes.Timed)
            {
                if (Time.inFixedTimeStep)
                    return;

                if (Time.time < _nextClientSendTime)
                    return;
            }
            //Fixed interval.
            else
            {
                if (!Time.inFixedTimeStep)
                    return;
            }

            //Not using client auth movement.
            if (!_clientAuthoritative)
                return;
            //Only send to server if client.
            if (!NetworkIsClient())
                return;
            //Not authoritative client.
            if (!NetworkHasAuthority())
                return;

            SyncProperties sp = ReturnDifferentProperties(_clientSyncData);

            bool useReliable = _reliable;
            if (!CanSendProperties(ref sp, ref _clientSettleSent, ref useReliable))
                return;
            //Add additional sync properties.
            ApplyRequiredSyncProperties(ref sp);

            uint platformNetId = GetPlatformNetworkId();
            Vector3 position = GetPlatformPositionOffset();
            Quaternion rotation = GetPlatformRotationOffset();

            /* This only applies if using interval but
             * add anyway since the math operation is fast. */
            _nextClientSendTime = Time.time + _synchronizeInterval;
            _clientSyncData = new TransformSyncData((byte)sp, _lastClientSentSequenceId,
                position, rotation, TargetTransform.GetScale(),
                platformNetId
                );


            _lastClientSentSequenceId += 1;

            //send to clients.
            if (useReliable)
                CmdSendSyncDataReliable(_clientSyncData);
            else
                CmdSendSyncDataUnreliable(_clientSyncData);
        }

        /// <summary>
        /// Checks if server needs to send data to clients.
        /// </summary>
        private void CheckSendToClients()
        {
            //Timed interval.
            if (_intervalType == IntervalTypes.Timed)
            {
                if (Time.inFixedTimeStep)
                    return;

                if (Time.time < _nextServerSendTime)
                    return;
            }
            //Fixed interval.
            else
            {
                if (!Time.inFixedTimeStep)
                    return;
            }

            //Only send to clients if server.
            if (!NetworkIsServer())
                return;

            /* If server only or has authority then use transforms current position.
             * When server only client values are set immediately, but as client host
             * they are smoothed so transforms do not snap. When smoothed instead of
             * sending the transforms current data we will send the goal data. This prevents
             * clients from receiving slower updates when running as a client host. */
            bool useServerSyncData = (_targetData == null);
            SyncProperties sp;
            //Breaking if statements down for easier reading.
            if (useServerSyncData)
                sp = ReturnDifferentProperties(_serverSyncData);
            //No authority and not server only.
            else
                sp = ReturnDifferentProperties(_serverSyncData, _targetData);

            bool useReliable = _reliable;
            if (!CanSendProperties(ref sp, ref _serverSettleSent, ref useReliable))
                return;

            //Add additional sync properties.
            ApplyRequiredSyncProperties(ref sp);

            /* This only applies if using interval but
            * add anyway since the math operation is fast. */
            _nextServerSendTime = Time.time + _synchronizeInterval;
            /* If not using server sync data then we are using
             * targetdata. This is when running as a client host, and
             * the data is what was received from a spectator. */
            if (!useServerSyncData)
            {
                /* Have to use just calculated sync properties because the sync properties
                 * from server to client can vary from what they were client to server when
                 * using client authority. */
                _serverSyncData = new TransformSyncData((byte)sp, _lastServerSentSequenceId,
                    _targetData.GoalData.Position, _targetData.GoalData.Rotation, _targetData.GoalData.Scale, _targetData.GoalData.PlatformNetId);
            }
            /* If using server data then transform was snapped into position
             * or has authority and is client host.
             * So there is no need to read from targetdata. */
            else
            {
                uint platformNetId = GetPlatformNetworkId();
                Vector3 position = GetPlatformPositionOffset();
                Quaternion rotation = GetPlatformRotationOffset();

                _serverSyncData = new TransformSyncData((byte)sp, _lastServerSentSequenceId,
                    position, rotation, TargetTransform.GetScale(),
                    platformNetId
                    );
            }

            _lastServerSentSequenceId += 1;

            //send to clients.
            if (useReliable)
                RpcSendSyncDataReliable(_serverSyncData);
            else
                RpcSendSyncDataUnreliable(_serverSyncData);
        }

        /// <summary>
        /// Updates the platform cache and returns true if platform exist.
        /// </summary>
        private bool UpdatePlatform(uint netId)
        {
            NetworkIdentity currentPlatformIdentity = Platform.Identity;
            //If net id is 0 then platform cannot be looked up.
            if (netId == 0)
            {
                Platform.Identity = null;
            }
            else
            {
                /* True if the update should be blocked. This is to save performance.
                 * 
                 * True if current platform exist, and it's netId matches passed in Id. */
                bool blockUpdate = (currentPlatformIdentity != null && currentPlatformIdentity.netId == netId);
                
                if (!blockUpdate)
                    Platform.Identity = NetworkIdentity.spawned[netId];
            }

            //If values need to be reset.
            if (currentPlatformIdentity != Platform.Identity)
            {
                Platform.LastPosition = null;
                Platform.LastRotation = null;
            }

            return PlatformValid();
        }
        /// <summary>
        /// Returns if a platform is valid.
        /// </summary>
        /// <returns></returns>
        private bool PlatformValid()
        {
            return (Platform.Identity != null && Platform.Identity.netId != 0);
        }

        /// <summary>
        /// Sets which platform netId to use.
        /// </summary>
        /// <param name="platformNetId"></param>
        private uint GetPlatformNetworkId()
        {
            //Platform exist.
            if (PlatformValid())
                return Platform.Identity.netId;
            else
                return 0;
        }

        /// <summary>
        /// Gets the position value to use for a platform offset, if a platform exist. Otherwise returns transforms position.
        /// </summary>
        private Vector3 GetPlatformPositionOffset()
        {
            //Platform exist.
            if (PlatformValid())
                return (Platform.Identity.transform.position - transform.position);
            //No platform.
            else
                return TargetTransform.GetPosition(UseLocalSpace);
        }

        /// <summary>
        /// Gets the position value to use for a platform offset, if a platform exist. Otherwise returns transforms position.
        /// </summary>
        private Quaternion GetPlatformRotationOffset()
        {
            //Platform exist.
            if (PlatformValid())
                return (Platform.Identity.transform.rotation * Quaternion.Inverse(transform.rotation));
            //No platform.
            else
                return TargetTransform.GetRotation(UseLocalSpace);
        }

        /// <summary>
        /// Applies SyncProperties which are required based on settings.
        /// </summary>
        /// <param name="sp"></param>
        private void ApplyRequiredSyncProperties(ref SyncProperties sp)
        {
            /* //FEATURE Always send platform if platform is valid.
             * In the future I'd like to only send if changed. */
            if (PlatformValid())
                sp |= SyncProperties.Platform;

            //If not reliable must send everything that is generally synchronized.
            if (!_reliable)
            {
                sp |= (ReturnConfiguredSynchronizedProperties() | SyncProperties.Sequenced);
            }
            //If has settled then must include all transform values to ensure a perfect match.
            else if (EnumContains.SyncPropertiesContains(sp, SyncProperties.Settled))
            {
                sp |= ReturnConfiguredSynchronizedProperties();
            }
        }

        /// <summary>
        /// Returns properties which are configured to be synchronized. This does not mean all of these properties will send. These only send if using unreliable or if settled to force a synchronization.
        /// </summary>
        /// <returns></returns>
        private SyncProperties ReturnConfiguredSynchronizedProperties()
        {
            SyncProperties sp = SyncProperties.None;

            if (_synchronizePosition == SynchronizeTypes.Normal)
                sp |= SyncProperties.Position;
            if (_synchronizeRotation == SynchronizeTypes.Normal)
                sp |= SyncProperties.Rotation;
            if (_synchronizeScale == SynchronizeTypes.Normal)
                sp |= SyncProperties.Scale;

            //Platforms are always synchronized since they are set manually.
            sp |= SyncProperties.Platform;

            return sp;
        }

        /// <summary>
        /// Returns if data updates should send based on SyncProperties, Reliable, and send history.
        /// </summary>
        /// <param name=""></param>
        /// <returns></returns>
        private bool CanSendProperties(ref SyncProperties sp, ref bool settleSent, ref bool useReliable)
        {
            //If nothing has changed.
            if (sp == SyncProperties.None)
            {
                /* If reliable is default and there's no extrapolation
                 * then there is no reason to send a settle packet.
                 * This is because extrapolation can overshoot while
                 * waiting for a new packet, but with extrapolation off
                 * the most recent reliable packet is always the latest
                 * data. */
                if (_reliable && _extrapolationSpan == 0f)
                    return false;

                //Settle has already been sent.
                if (settleSent)
                {
                    return false;
                }
                //Settle has not been sent yet.
                else
                {
                    settleSent = true;
                    useReliable = true;
                    sp |= SyncProperties.Settled;
                    return true;
                }
            }
            //Properties need to be synchronized.
            else
            {
                //Unset settled.
                settleSent = false;

                return true;
            }

        }

        /// <summary>
        /// Returns which properties need to be sent to maintain synchronization with the transforms current properties.
        /// </summary>
        /// <returns></returns>
        private SyncProperties ReturnDifferentProperties(TransformSyncData data)
        {
            return ReturnDifferentProperties(data, null);
        }
        /// <summary>
        /// Returns which properties need to be sent to maintain synchronization with targetData properties.
        /// </summary>
        /// <param name="targetData">When specified data is compared against targetData. Otherwise, data is compared against the transform.</param>
        /// <returns></returns>
        private SyncProperties ReturnDifferentProperties(TransformSyncData data, TargetSyncData targetData)
        {
            //Data is null, so it's definitely not a match.
            if (data == null)
                return (SyncProperties.Position | SyncProperties.Rotation | SyncProperties.Scale | SyncProperties.Platform);

            SyncProperties sp = SyncProperties.None;

            if (_synchronizePosition == SynchronizeTypes.Normal && !PositionMatches(data, targetData, _preciseSynchronization))
                sp |= SyncProperties.Position;
            if (_synchronizeRotation == SynchronizeTypes.Normal && !RotationMatches(data, targetData, _preciseSynchronization))
                sp |= SyncProperties.Rotation;
            if (_synchronizeScale == SynchronizeTypes.Normal && !ScaleMatches(data, targetData, _preciseSynchronization))
                sp |= SyncProperties.Scale;

            return sp;
        }

        /// <summary>
        /// Moves towards TargetSyncData.
        /// </summary>
        private void MoveTowardsTargetSyncData()
        {
            //No SyncData to check against.
            if (_targetData == null)
                return;
            /* Client authority but there is no owner.
             * Can happen when client authority is ticked but
            * the server takes away authority. */
            if (NetworkIsServer() && _clientAuthoritative && !NetworkHasOwner() && _targetData != null)
            {
                /* Remove sync data so server no longer tries to sync up to last data received from client.
                 * Object may be moved around on server at this point. */
                _targetData = null;
                return;
            }
            //Client authority, don't need to synchronize with self.
            if (NetworkHasAuthority() && _clientAuthoritative)
                return;
            //Not client authority but also not synchronize to owner.
            if (NetworkHasAuthority() && !_clientAuthoritative && !_synchronizeToOwner)
                return;

            bool extrapolate = (_targetData.Extrapolation != null && _targetData.Extrapolation.Remaining > 0f);
            //Already at the correct position and no more remaining extrapolation to use.
            if (SyncDataMatchesTransform(_targetData.GoalData, true) && !extrapolate)
                return;

            //Position
            if (_synchronizePosition != SynchronizeTypes.NoSynchronization)
            {
                /* Only use localspace if configured to local space
                 * and if not using a platform. */
                bool useLocalSpace = (UseLocalSpace && !PlatformValid());
                Vector3 positionGoal = PositionAfterPlatform(_targetData.GoalData.Position);

                if (_targetData.MoveRates.Position == -1f)
                {
                    TargetTransform.SetPosition(useLocalSpace, positionGoal);
                }
                else
                {
                    //If to extrapolate then overwrite position goal with extrapolation.
                    if (extrapolate)
                        positionGoal = PositionAfterPlatform(_targetData.Extrapolation.Position);

                    TargetTransform.SetPosition(useLocalSpace,
                        Vector3.MoveTowards(TargetTransform.GetPosition(useLocalSpace), positionGoal, _targetData.MoveRates.Position * Time.deltaTime)
                        );

                    /* If the platform is valid then also update position
                     * against the last platform position. */
                    if (PlatformValid())
                    {
                        Vector3 platformDifference = Vector3.zero;
                        if (Platform.LastPosition != null)
                            platformDifference = (Platform.Identity.transform.position - Platform.LastPosition.Value);

                        Platform.LastPosition = Platform.Identity.transform.position;
                        TargetTransform.SetPosition(false, TargetTransform.GetPosition(false) + platformDifference);
                    }
                }
            }
            //Rotation.
            if (_synchronizeRotation != SynchronizeTypes.NoSynchronization)
            {
                /* Only use localspace if configured to local space
                * and if not using a platform. */
                bool useLocalSpace = (UseLocalSpace && !PlatformValid());
                Quaternion rotationGoal = RotationAfterPlatform(_targetData.GoalData.Rotation);

                if (_targetData.MoveRates.Rotation == -1f)
                {
                    TargetTransform.SetRotation(useLocalSpace, rotationGoal);
                }
                else
                {
                    TargetTransform.SetRotation(UseLocalSpace,
                    Quaternion.RotateTowards(TargetTransform.GetRotation(useLocalSpace), rotationGoal, _targetData.MoveRates.Rotation * Time.deltaTime)
                    );

                    /* If the platform is valid then also update position
                     * against the last platform position. */
                    if (PlatformValid())
                    {
                        Quaternion platformDifference = Quaternion.identity;
                        if (Platform.LastRotation != null)
                            platformDifference = (Platform.Identity.transform.rotation * Quaternion.Inverse(Platform.LastRotation.Value));

                        Platform.LastRotation = Platform.Identity.transform.rotation;
                        TargetTransform.SetRotation(false, TargetTransform.GetRotation(false) * platformDifference);
                    }
                }
            }
            //Scale.
            if (_synchronizeScale != SynchronizeTypes.NoSynchronization)
            {
                if (_targetData.MoveRates.Scale == -1f)
                {
                    TargetTransform.SetScale(_targetData.GoalData.Scale);
                }
                else
                {
                    TargetTransform.SetScale(
                    Vector3.MoveTowards(TargetTransform.GetScale(), _targetData.GoalData.Scale, _targetData.MoveRates.Scale * Time.deltaTime)
                    );
                }
            }

            //Remove from remaining extrapolation time.
            if (extrapolate)
                _targetData.Extrapolation.Remaining -= Time.deltaTime;
        }


        /// <summary>
        /// Returns true if the passed in axes contains all axes.
        /// </summary>
        /// <param name="axes"></param>
        /// <returns></returns>
        private bool SnapAll(Axes axes)
        {
            return (axes == (Axes.X | Axes.Y | Axes.Z));
        }

        /// <summary>
        /// Returns true if the passed in SyncData values match this transforms values.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private bool SyncDataMatchesTransform(TransformSyncData data, bool precise)
        {
            if (data == null)
                return false;

            return (
                PositionMatches(data, null, precise) &&
                RotationMatches(data, null, precise) &&
                ScaleMatches(data, null, precise)
                );
        }

        /// <summary>
        /// Returns if this transform position matches data.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private bool PositionMatches(TransformSyncData data, TargetSyncData targetData, bool precise)
        {
            if (data == null)
                return false;

            //Position to check data against.
            Vector3 position;
            //Target data not specified, use transform data.
            if (targetData == null)
            {
                //If using a platform then we must compare in world space as platform values are always world space.
                if (EnumContains.SyncPropertiesContains((SyncProperties)data.SyncProperties, SyncProperties.Platform))
                    position = GetPlatformPositionOffset();
                else
                    position = TargetTransform.GetPosition(UseLocalSpace);
            }
            //Use target data.
            else
            {
                position = targetData.GoalData.Position;
            }

            if (precise)
            {
                return (position == data.Position);
            }
            else
            {
                float dist = Vector3.SqrMagnitude(position - data.Position);
                return (dist < 0.0001f);
            }
        }
        /// <summary>
        /// Returns if this transform rotation matches data.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private bool RotationMatches(TransformSyncData data, TargetSyncData targetData, bool precise)
        {
            if (data == null)
                return false;

            //Rotation to check data against.
            Quaternion rotation;
            //Target data not specified, use transform data.
            if (targetData == null)
            {
                //If using a platform then we must compare in world space as platform values are always world space.
                if (EnumContains.SyncPropertiesContains((SyncProperties)data.SyncProperties, SyncProperties.Platform))
                    rotation = GetPlatformRotationOffset();
                else
                    rotation = TargetTransform.GetRotation(UseLocalSpace);
            }
            //Use target data.
            else
            {
                rotation = targetData.GoalData.Rotation;
            }

            if (precise)
                return rotation.Matches(data.Rotation);
            else
                return rotation.Matches(data.Rotation, 1f);
        }

        /// <summary>
        /// Returns if this transform scale matches data.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private bool ScaleMatches(TransformSyncData data, TargetSyncData targetData, bool precise)
        {
            if (data == null)
                return false;

            Vector3 scale;
            //Target data not specified, use transform data.
            if (targetData == null)
            {
                scale = TargetTransform.GetScale();
            }
            //Use target data.
            else
            {
                scale = targetData.GoalData.Scale;
            }

            if (precise)
            {
                return (scale == data.Scale);
            }
            else
            {
                float dist = Vector3.SqrMagnitude(scale - data.Scale);
                return (dist < 0.0001f);
            }
        }

        /// <summary>
        /// Sends SyncData to the server. Only used with client auth.
        /// </summary>
        /// <param name="data"></param>
#if MIRROR
        [Command(channel = 0)]
#elif MIRRORNG
        [ServerRpc(channel = 0)]
#endif
        private void CmdSendSyncDataReliable(TransformSyncData data)
        {
            ClientDataReceived(data);
        }
        /// <summary>
        /// Sends SyncData to the server. Only used with client auth.
        /// </summary>
        /// <param name="data"></param>
#if MIRROR
        [Command(channel = 1)]
#elif MIRRORNG
        [ServerRpc(channel = 1)]
#endif
        private void CmdSendSyncDataUnreliable(TransformSyncData data)
        {
            ClientDataReceived(data);
        }

        //TODO need to send platform data if present with initial payload.

        /// <summary>
        /// Called on clients when server data is received.
        /// </summary>
        /// <param name="data"></param>
        [Server]
        private void ClientDataReceived(TransformSyncData data)
        {
            //Sent to self.
            if (NetworkHasAuthority())
                return;
            if (OutOfSequence(data, ref _lastClientReceivedSequenceId))
                return;

            //Fill in missing data for properties that werent included in send.
            FillMissingData(data, _targetData);

            if (OnClientDataReceived != null)
            {
                ReceivedClientData rcd = new ReceivedClientData(ReceivedClientData.DataTypes.Interval, UseLocalSpace, data);
                OnClientDataReceived.Invoke(rcd);

                //If data was nullified then do nothing.
                if (rcd.Data == null || data == null)
                    return;
            }

            UpdatePlatform(data.PlatformNetId);

            /* If server only then snap to target position. 
             * Should I ever add extrapolation on server only
             * then I would need to move smoothly instead and
             * perform extrapolation calculations. */
            if (NetworkIsServerOnly())
            {
                ApplyTransformSnapping(data, true);
                /* If there is a platform then target data must
                 * be set to keep object on platform. Otherwise
                 * targetdata can be nullified. */
                if (PlatformValid())
                {
                    MoveRateData moveRates = SetInstantMoveRates();
                    _targetData = new TargetSyncData(data, moveRates, null);
                }
                else
                {
                    _targetData = null;
                }
            }
            /* If not server only, so if client host, then set data
             * normally for smoothing. */
            else
            {
                ExtrapolationData extrapolation = null;
                MoveRateData moveRates;
                //If teleporting set move rates to be instantaneous.
                if (ShouldTeleport(data))
                {
                    moveRates = SetInstantMoveRates();
                }
                //If not teleporting calculate extrapolation and move rates.
                else
                {
                    extrapolation = SetExtrapolation(data, _targetData);
                    moveRates = SetMoveRates(data);
                }

                ApplyTransformSnapping(data, false);
                _targetData = new TargetSyncData(data, moveRates, extrapolation);
            }
        }

        /// <summary>
        /// Returns if the transform should teleport.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private bool ShouldTeleport(TransformSyncData data)
        {
            if (_teleportThresholdSquared <= 0f)
                return false;

            float dist = Vector3.SqrMagnitude(TargetTransform.GetPosition(UseLocalSpace) - data.Position);
            return dist >= _teleportThresholdSquared;
        }

        /// <summary>
        /// Returns if the sequence on data is out of order against last sequence and updates last sequence.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="lastSequence"></param>
        /// <returns></returns>
        private bool OutOfSequence(TransformSyncData data, ref uint lastSequence)
        {
            SyncProperties sp = (SyncProperties)data.SyncProperties;
            /* If not reliable then compare sequence id to ensure
             * this did not arrive out of order. */
            if (EnumContains.SyncPropertiesContains(sp, SyncProperties.Sequenced))
            {
                //If data Id is less than last Id then it's old data.
                if (data.SequenceId < lastSequence)
                    return true;

                lastSequence = data.SequenceId;
            }

            //Fall through. If here not out of sequence.
            return false;
        }

        /// <summary>
        /// Sets MoveRates to move instantly.
        /// </summary>
        /// <returns></returns>
        private MoveRateData SetInstantMoveRates()
        {
            return new MoveRateData()
            {
                Position = -1f,
                Rotation = -1f,
                Scale = -1f
            };
        }

        /// <summary>
        /// Sets MoveRates based on data, transform position, and synchronization interval.
        /// </summary>
        /// <param name="data"></param>
        private MoveRateData SetMoveRates(TransformSyncData data)
        {
            float past = ReturnSyncInterval() + _interpolationFallbehind;

            MoveRateData moveRates = new MoveRateData();
            float distance;
            /* Position. */
            Vector3 position = GetPlatformPositionOffset();
            distance = Vector3.Distance(position, data.Position);
            moveRates.Position = distance / past;
            /* Rotation. */
            Quaternion rotation = GetPlatformRotationOffset();
            distance = Quaternion.Angle(rotation, data.Rotation);
            moveRates.Rotation = distance / past;
            /* Scale. */
            distance = Vector3.Distance(TargetTransform.GetScale(), data.Scale);
            moveRates.Scale = distance / past;

            return moveRates;
        }

        /// <summary>
        /// Returns a modified position after including platform support.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        private Vector3 PositionAfterPlatform(Vector3 pos)
        {
            if (PlatformValid())
                return (Platform.Identity.transform.position - pos);
            else
                return pos;
        }

        /// <summary>
        /// Returns a modified rotation after including platform support.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        private Quaternion RotationAfterPlatform(Quaternion rot)
        {
            if (PlatformValid())
                return (Platform.Identity.transform.rotation * Quaternion.Inverse(rot));
            else
                return rot;
        }


        /// <summary>
        /// Sets ExtrapolationExtra using TransformSyncData.
        /// </summary>
        /// <param name="extrapolation"></param>
        /// <param name="data"></param>
        private ExtrapolationData SetExtrapolation(TransformSyncData data, TargetSyncData previousTargetSyncData)
        {
            /* If platform Id changed.
             * Cannot extrapolate when platform Ids change because
             * the space used is changed. */
            if (previousTargetSyncData != null && previousTargetSyncData.GoalData.PlatformNetId != data.PlatformNetId)
                return null;
            //No extrapolation.
            if (_extrapolationSpan == 0f || previousTargetSyncData == null)
                return null;
            //Settled packet.
            if (EnumContains.SyncPropertiesContains((SyncProperties)data.SyncProperties, SyncProperties.Settled))
                return null;

            Vector3 positionDirection = (data.Position - previousTargetSyncData.GoalData.Position);
            Vector3 position = GetPlatformPositionOffset();
            Vector3 goalDirectionNormalzied = (data.Position - position).normalized;
            /* If direction to goal is different from extrapolation direction
             * then do not extrapolate. This can occur when the extrapolation
             * overshoots. If the extrapolation was to continue like this then
             * it would likely overshoot more and more, becoming extremely
             * offset. */
            if (goalDirectionNormalzied != positionDirection.normalized)
                return null;

            float multiplier = _extrapolationSpan / ReturnSyncInterval();

            return new ExtrapolationData()
            {
                Position = data.Position + (positionDirection * multiplier),
                Remaining = ReturnSyncInterval() + _extrapolationSpan,
            };
        }

        /// <summary>
        /// Snaps the transform to targetData where snapping is applicable.
        /// </summary>
        /// <param name="targetData">Data to snap from.</param>
        private void ApplyTransformSnapping(TransformSyncData targetData, bool snapAll)
        {
            bool platformValid = PlatformValid();

            SyncProperties sp = (SyncProperties)targetData.SyncProperties;
            if (snapAll || EnumContains.SyncPropertiesContains(sp, SyncProperties.Position))
            {
                /* Position will either be targetData position if not
                 * using platforms, or adjusted position for platforms. */
                bool useLocalSpace = (UseLocalSpace && !platformValid);
                Vector3 position;
                if (platformValid)
                    position = Platform.Identity.transform.position - targetData.Position;
                else
                    position = targetData.Position;

                //If to snap all.
                if (snapAll || SnapAll(_snapPosition))
                {
                    TargetTransform.SetPosition(useLocalSpace, position);
                }
                //Snap some or none.
                else
                {
                    //Snap X.
                    if (EnumContains.AxesContains(_snapPosition, Axes.X))
                        TargetTransform.SetPosition(useLocalSpace, new Vector3(position.x, TargetTransform.GetPosition(useLocalSpace).y, TargetTransform.GetPosition(useLocalSpace).z));
                    //Snap Y.
                    if (EnumContains.AxesContains(_snapPosition, Axes.Y))
                        TargetTransform.SetPosition(useLocalSpace, new Vector3(TargetTransform.GetPosition(useLocalSpace).x, position.y, TargetTransform.GetPosition(useLocalSpace).z));
                    //Snap Z.
                    if (EnumContains.AxesContains(_snapPosition, Axes.Z))
                        TargetTransform.SetPosition(useLocalSpace, new Vector3(TargetTransform.GetPosition(useLocalSpace).x, TargetTransform.GetPosition(useLocalSpace).y, position.z));
                }
            }

            /* Rotation. */
            if (snapAll || EnumContains.SyncPropertiesContains(sp, SyncProperties.Rotation))
            {
                /* Position will either be targetData position if not
                * using platforms, or adjusted position for platforms. */
                bool useLocalSpace = (UseLocalSpace && !platformValid);
                Quaternion rotation;
                if (platformValid)
                    rotation = Platform.Identity.transform.rotation * Quaternion.Inverse(targetData.Rotation);
                else
                    rotation = targetData.Rotation;

                //If to snap all.
                if (snapAll || SnapAll(_snapRotation))
                {
                    TargetTransform.SetRotation(useLocalSpace, rotation);
                }
                //Snap some or none.
                else
                {
                    /* Only perform snap checks if snapping at least one
                     * to avoid extra cost of calculations. */
                    if ((int)_snapRotation != 0)
                    {
                        /* Convert to eulers since that is what is shown
                         * in the inspector. */
                        Vector3 startEuler = TargetTransform.GetRotation(UseLocalSpace).eulerAngles;
                        Vector3 targetEuler = rotation.eulerAngles;
                        //Snap X.
                        if (EnumContains.AxesContains(_snapRotation, Axes.X))
                            startEuler.x = targetEuler.x;
                        //Snap Y.
                        if (EnumContains.AxesContains(_snapRotation, Axes.Y))
                            startEuler.y = targetEuler.y;
                        //Snap Z.
                        if (EnumContains.AxesContains(_snapRotation, Axes.Z))
                            startEuler.z = targetEuler.z;

                        //Rebuild into quaternion.
                        TargetTransform.SetRotation(useLocalSpace, Quaternion.Euler(startEuler));
                    }
                }
            }

            if (snapAll || EnumContains.SyncPropertiesContains(sp, SyncProperties.Scale))
            {
                //If to snap all.
                if (snapAll || SnapAll(_snapScale))
                {
                    TargetTransform.SetScale(targetData.Scale);
                }
                //Snap some or none.
                else
                {
                    //Snap X.
                    if (EnumContains.AxesContains(_snapScale, Axes.X))
                        TargetTransform.SetScale(new Vector3(targetData.Scale.x, TargetTransform.GetScale().y, TargetTransform.GetScale().z));
                    //Snap Y.
                    if (EnumContains.AxesContains(_snapScale, Axes.Y))
                        TargetTransform.SetPosition(UseLocalSpace, new Vector3(TargetTransform.GetScale().x, targetData.Scale.y, TargetTransform.GetScale().z));
                    //Snap Z.
                    if (EnumContains.AxesContains(_snapScale, Axes.Z))
                        TargetTransform.SetPosition(UseLocalSpace, new Vector3(TargetTransform.GetScale().x, TargetTransform.GetScale().y, targetData.Scale.z));
                }
            }
        }

        /// <summary>
        /// Sends SyncData to clients.
        /// </summary>
        /// <param name="data"></param>
        [ClientRpc(channel = 0)]
        private void RpcSendSyncDataReliable(TransformSyncData data)
        {
            ServerDataReceived(data);
        }
        /// <summary>
        /// Sends SyncData to clients.
        /// </summary>
        /// <param name="data"></param>
        [ClientRpc(channel = 1)]
        private void RpcSendSyncDataUnreliable(TransformSyncData data)
        {
            ServerDataReceived(data);
        }

        /// <summary>
        /// Called on clients when server data is received.
        /// </summary>
        /// <param name="data"></param>
        [Client]
        private void ServerDataReceived(TransformSyncData data)
        {
            //If client host exit method.
            if (NetworkIsServer())
                return;

            //If owner of object.
            if (NetworkHasAuthority())
            {
                //Client authoritative, already in sync.
                if (_clientAuthoritative)
                    return;
                //Not client authoritative, but also not sync to owner.
                if (!_clientAuthoritative && !_synchronizeToOwner)
                    return;
            }

            if (OutOfSequence(data, ref _lastServerReceivedSequenceId))
                return;

            //Fill in missing data for properties that werent included in send.
            FillMissingData(data, _targetData);

            UpdatePlatform(data.PlatformNetId);
            ExtrapolationData extrapolation = null;
            MoveRateData moveRates;

            //If teleporting set move rates to be instantaneous.
            if (ShouldTeleport(data))
            {
                moveRates = SetInstantMoveRates();
            }
            //If not teleporting calculate extrapolation and move rates.
            else
            {
                extrapolation = SetExtrapolation(data, _targetData);
                moveRates = SetMoveRates(data);
            }

            ApplyTransformSnapping(data, false);
            _targetData = new TargetSyncData(data, moveRates, extrapolation);
        }

        /// <summary>
        /// Modifies values within goalData based on what data was included in the packet.
        /// For example, if rotation was not included in the packet then the last datas rotation will be used, or transforms current rotation if there is no previous packet.
        /// </summary>
        private void FillMissingData(TransformSyncData data, TargetSyncData targetSyncData)
        {
            SyncProperties sp = (SyncProperties)data.SyncProperties;
            /* Begin by setting goal data using what has been serialized
            * via the writer. */
            //Position wasn't included.
            if (!EnumContains.SyncPropertiesContains(sp, SyncProperties.Position))
            {
                if (targetSyncData == null)
                    data.Position = TargetTransform.GetPosition(UseLocalSpace);
                else
                    data.Position = targetSyncData.GoalData.Position;
            }
            //Rotation wasn't included.
            if (!EnumContains.SyncPropertiesContains(sp, SyncProperties.Rotation))
            {
                if (targetSyncData == null)
                    data.Rotation = TargetTransform.GetRotation(UseLocalSpace);
                else
                    data.Rotation = targetSyncData.GoalData.Rotation;
            }
            //Scale wasn't included.
            if (!EnumContains.SyncPropertiesContains(sp, SyncProperties.Scale))
            {
                if (targetSyncData == null)
                    data.Scale = TargetTransform.GetScale();
                else
                    data.Scale = targetSyncData.GoalData.Scale;
            }

            /* Platform data will always be included every packet
             * if a platform is present. */
        }

        #region Network specific Support.
        /// <summary>
        /// Returns true if object has an owner.
        /// </summary>
        /// <returns></returns>
        protected bool NetworkHasOwner()
        {
#if MIRROR
            return (base.connectionToClient != null);
#elif MIRRORNG
            return (base.ConnectionToClient != null);
#endif
        }
        /// <summary>
        /// Returns if current client has authority.
        /// </summary>
        /// <returns></returns>
        protected bool NetworkHasAuthority()
        {
#if MIRROR
            return base.hasAuthority;
#elif MIRRORNG
            return base.HasAuthority;
#endif
        }
        /// <summary>
        /// Returns if is server.
        /// </summary>
        /// <returns></returns>
        protected bool NetworkIsServer()
        {
#if MIRROR
            return base.isServer;
#elif MIRRORNG
            return base.IsServer;
#endif

        }
        protected bool NetworkIsServerOnly()
        {
#if MIRROR
            return base.isServerOnly;
#elif MIRRORNG
            return base.IsServerOnly;
#endif

        }
        /// <summary>
        /// Returns if is client.
        /// </summary>
        /// <returns></returns>
        protected bool NetworkIsClient()
        {
#if MIRROR
            return base.isClient;
#elif MIRRORNG
            return base.IsClient;
#endif

        }
        #endregion

        #region Editor.
        private void OnValidate()
        {
            SetTeleportThresholdSquared();
        }
        #endregion
    }
}

