using System;
using UnityEngine;

namespace FirstGearGames.AuthoritativeMovement.Managers.Global.MilestoneUDP
{


    public class FixedUpdateManager : MonoBehaviour
    {

        #region Public.
        /// <summary>
        /// Dispatched before a simulated fixed update occurs.
        /// </summary>
        public static event Action OnPreFixedUpdate;
        /// <summary>
        /// Dispatched when a simulated fixed update occurs.
        /// </summary>
        public static event Action OnFixedUpdate;
        /// <summary>
        /// Dispatched after a simulated fixed update occurs. Physics would have simulated prior to this event.
        /// </summary>
        public static event Action OnPostFixedUpdate;
        /// <summary>
        /// Current fixed frame. Applied before any events are invoked.
        /// </summary>
        public static uint FixedFrame { get; private set; } = 0;
        #endregion

        #region Private.
        /// <summary>
        /// Ticks applied from updates.
        /// </summary>
        private float _updateTicks = 0f;
        /// <summary>
        /// Range which the timing may reside within.
        /// </summary>
        private static float[] _timingRange;
        /// <summary>
        /// Value to change timing per step.
        /// </summary>
        private static float _timingPerStep;
        /// <summary>
        /// Current FixedUpdate timing.
        /// </summary>
        private static float _adjustedFixedUpdate;
        #endregion

        #region Const.
        /// <summary>
        /// Maximum percentage timing may vary from the FixedDeltaTime.
        /// </summary>
        private const float MAXIMUM_OFFSET_PERCENT = 0.35f;
        /// <summary>
        /// How quickly timing can recover to it's default value.
        /// </summary>
        private const float TIMING_RECOVER_RATE = 0.0025f;
        /// <summary>
        /// Percentage of FixedDeltaTime to modify timing by when a step must occur.
        /// </summary>
        public const float TIMING_STEP_PERCENT = 0.015f;
        #endregion

        private void Update()
        {
            UpdateTicks(Time.deltaTime);
        }

        /// <summary>
        /// Initializes this script for use. Should only be completed once.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void FirstInitialize()
        {
            GameObject go = new GameObject();
            go.AddComponent<FixedUpdateManager>();
            DontDestroyOnLoad(go);

            Physics.autoSimulation = false;
            Physics2D.autoSimulation = false;

            _adjustedFixedUpdate = Time.fixedDeltaTime;
            _timingPerStep = Time.fixedDeltaTime * TIMING_STEP_PERCENT;
            _timingRange = new float[]
            {
                Time.fixedDeltaTime * (1f - MAXIMUM_OFFSET_PERCENT),
                Time.fixedDeltaTime * (1f + MAXIMUM_OFFSET_PERCENT)
            };
        }

        /// <summary>
        /// Adds onto AdjustedFixedDeltaTime.
        /// </summary>
        /// <param name="steps"></param>
        public static void AddTiming(sbyte steps)
        {
            if (steps == 0)
                return;

            _adjustedFixedUpdate = Mathf.Clamp(_adjustedFixedUpdate + (steps * _timingPerStep), _timingRange[0], _timingRange[1]);
        }

        /// <summary>
        /// Adds the current deltaTime to update ticks and processes simulated fixed update.
        /// </summary>
        private void UpdateTicks(float deltaTime)
        {
            _updateTicks += deltaTime;
            while (_updateTicks >= _adjustedFixedUpdate)
            {
                _updateTicks -= _adjustedFixedUpdate;
                /* If at maximum value then reset fixed frame.
                * This would probably break the game but even at
                * 128t/s it would take over a year of the server
                 * running straight to ever reach this value! */
                if (FixedFrame == uint.MaxValue)
                    FixedFrame = 0;
                FixedFrame++;

                OnPreFixedUpdate?.Invoke();
                OnFixedUpdate?.Invoke();

                Physics2D.Simulate(Time.fixedDeltaTime);
                Physics.Simulate(Time.fixedDeltaTime);

                OnPostFixedUpdate?.Invoke();
            }

            //Recover timing towards default fixedDeltaTime.
            _adjustedFixedUpdate = Mathf.MoveTowards(_adjustedFixedUpdate, Time.fixedDeltaTime, TIMING_RECOVER_RATE * deltaTime);
        }
    }

}
