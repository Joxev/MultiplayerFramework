using FirstGearGames.AuthoritativeMovement.Managers.Global.MilestoneUDP;
using Mirror;
using UnityEngine;

namespace FirstGearGames.AuthoritativeMovement.Motors.MiletoneUDP
{


    public class SmoothToZero : NetworkBehaviour
    {
        #region Lerp Method.
        //    #region Private.
        //    /// <summary>
        //    /// True if subscribed to FixedUpdateManager events.
        //    /// </summary>
        //    private bool _subscribed = false;
        //    /// <summary>
        //    /// Position before simulation is performed.
        //    /// </summary>
        //    private Vector3 _prePosition;
        //    /// <summary>
        //    /// Rotation before simulation is performed.
        //    /// </summary>
        //    private Quaternion _preRotation;
        //    /// <summary>
        //    /// Position before simulation is performed.
        //    /// </summary>
        //    private Vector3 _postLocalPosition;
        //    /// <summary>
        //    /// Rotation before simulation is performed.
        //    /// </summary>
        //    private Quaternion _postLocalRotation;
        //    /// <summary>
        //    /// Time passed since last fixed frame.
        //    /// </summary>
        //    private float _frameTimePassed = 0f;
        //    #endregion

        //    #region Const.
        //    /// <summary>
        //    /// Multiplier to apply towards delta time.
        //    /// </summary>
        //    private const float FRAME_TIME_MULTIPLIER = 0.75f;
        //    #endregion

        //    public override void OnStartAuthority()
        //    {
        //        base.OnStartAuthority();
        //        SubscribeToFixedUpdateManager(true);
        //    }

        //    public override void OnStopAuthority()
        //    {
        //        base.OnStopAuthority();
        //        SubscribeToFixedUpdateManager(false);
        //    }

        //    private void OnEnable()
        //    {
        //        if (base.hasAuthority)
        //            SubscribeToFixedUpdateManager(true);
        //    }
        //    private void OnDisable()
        //    {
        //        SubscribeToFixedUpdateManager(false);
        //    }

        //    private void Update()
        //    {
        //        if (base.hasAuthority)
        //        {
        //            Smooth();
        //        }
        //    }

        //    /// <summary>
        //    /// Smooths position and rotation to zero values.
        //    /// </summary>
        //    private void Smooth()
        //    {
        //        _frameTimePassed += (Time.deltaTime * FRAME_TIME_MULTIPLIER);
        //        float percent = Mathf.InverseLerp(0f, FixedUpdateManager.AdjustedFixedDeltaTime, _frameTimePassed);

        //        transform.localPosition = Vector3.Lerp(_postLocalPosition, Vector3.zero, percent);
        //        transform.localRotation = Quaternion.Lerp(_postLocalRotation, Quaternion.identity, percent);
        //    }

        //    /// <summary>
        //    /// Changes event subscriptions on the FixedUpdateManager.
        //    /// </summary>
        //    /// <param name="subscribe"></param>
        //    private void SubscribeToFixedUpdateManager(bool subscribe)
        //    {
        //        if (subscribe == _subscribed)
        //            return;

        //        if (subscribe)
        //        {
        //            FixedUpdateManager.OnPreFixedUpdate += FixedUpdateManager_OnPreFixedUpdate;
        //            FixedUpdateManager.OnPostFixedUpdate += FixedUpdateManager_OnPostFixedUpdate;
        //        }
        //        else
        //        {
        //            FixedUpdateManager.OnPreFixedUpdate -= FixedUpdateManager_OnPreFixedUpdate;
        //            FixedUpdateManager.OnPostFixedUpdate -= FixedUpdateManager_OnPostFixedUpdate;
        //        }

        //        _subscribed = subscribe;
        //    }

        //    private void FixedUpdateManager_OnPostFixedUpdate()
        //    {
        //        _frameTimePassed = 0f;
        //        transform.position = _prePosition;
        //        transform.rotation = _preRotation;
        //        _postLocalPosition = transform.localPosition;
        //        _postLocalRotation = transform.localRotation;
        //    }

        //    private void FixedUpdateManager_OnPreFixedUpdate()
        //    {
        //        transform.localPosition = Vector3.zero;
        //        transform.localRotation = Quaternion.identity;
        //        _prePosition = transform.position;
        //        _preRotation = transform.rotation;
        //    }
        //}
        #endregion

        #region Serialized.
        /// <summary>
        /// How quickly to smooth to zero.
        /// </summary>
        [Tooltip("How quickly to smooth to zero.")]
        [SerializeField]
        private float _smoothRate = 20f;
        #endregion

        #region Private.
        /// <summary>
        /// True if subscribed to FixedUpdateManager events.
        /// </summary>
        private bool _subscribed = false;
        /// <summary>
        /// Position before simulation is performed.
        /// </summary>
        private Vector3 _position;
        /// <summary>
        /// Rotation before simulation is performed.
        /// </summary>
        private Quaternion _rotation; 
        #endregion

        public override void OnStartAuthority()
        {
            base.OnStartAuthority();
            SubscribeToFixedUpdateManager(true);
        }

        public override void OnStopAuthority()
        {
            base.OnStopAuthority();
            SubscribeToFixedUpdateManager(false);
        }

        private void OnEnable()
        {
            if (base.hasAuthority)
                SubscribeToFixedUpdateManager(true);
        }
        private void OnDisable()
        {
            SubscribeToFixedUpdateManager(false);
        }

        private void Update()
        {
            if (base.hasAuthority)
            {
                Smooth();
            }
        }

        /// <summary>
        /// Smooths position and rotation to zero values.
        /// </summary>
        private void Smooth()
        {
            float distance;
            distance = Mathf.Max(0.01f, Vector3.Distance(transform.localPosition, Vector3.zero));
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, Vector3.zero, distance * _smoothRate * Time.deltaTime);
            distance = Mathf.Max(1f, Quaternion.Angle(transform.localRotation, Quaternion.identity));
            transform.localRotation = Quaternion.RotateTowards(transform.localRotation, Quaternion.identity, distance * _smoothRate * Time.deltaTime);
        }

        /// <summary>
        /// Changes event subscriptions on the FixedUpdateManager.
        /// </summary>
        /// <param name="subscribe"></param>
        private void SubscribeToFixedUpdateManager(bool subscribe)
        {
            if (subscribe == _subscribed)
                return;

            if (subscribe)
            {
                FixedUpdateManager.OnPreFixedUpdate += FixedUpdateManager_OnPreFixedUpdate;
                FixedUpdateManager.OnPostFixedUpdate += FixedUpdateManager_OnPostFixedUpdate;
            }
            else
            {
                FixedUpdateManager.OnPreFixedUpdate -= FixedUpdateManager_OnPreFixedUpdate;
                FixedUpdateManager.OnPostFixedUpdate -= FixedUpdateManager_OnPostFixedUpdate;
            }

            _subscribed = subscribe;
        }

        private void FixedUpdateManager_OnPostFixedUpdate()
        {
            transform.position = _position;
            transform.rotation = _rotation;
        }

        private void FixedUpdateManager_OnPreFixedUpdate()
        {
            _position = transform.position;
            _rotation = transform.rotation;
        }
    }

}