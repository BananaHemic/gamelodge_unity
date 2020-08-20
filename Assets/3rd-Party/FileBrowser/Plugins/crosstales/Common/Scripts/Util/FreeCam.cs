using UnityEngine;

namespace Crosstales.Common.Util
{
    /// <summary>
    /// A simple free camera to be added to a Unity game object.
    /// 
    /// Keys:
    ///	wasd / arrows	- movement
    ///	q/e 			- up/down (local space)
    ///	r/f 			- up/down (world space)
    ///	pageup/pagedown	- up/down (world space)
    ///	hold shift		- enable fast movement mode
    ///	right mouse  	- enable free look
    ///	mouse			- free look / rotation
    /// </summary>
    //[HelpURL("https://www.crosstales.com/media/data/assets/radio/api/class_crosstales_1_1_radio_1_1_demo_1_1_util_1_1_platform_controller.html")]
    public class FreeCam : MonoBehaviour
    {
        #region Variables

        /// <summary>Normal speed of camera movement.</summary>
        public float MovementSpeed = 10f;

        /// <summary>Speed of camera movement when shift is held down.</summary>
        public float FastMovementSpeed = 100f;

        /// <summary>Sensitivity for free look.</summary>
        public float FreeLookSensitivity = 3f;

        /// <summary>Amount to zoom the camera when using the mouse wheel.</summary>
        public float ZoomSensitivity = 10f;

        /// <summary>Amount to zoom the camera when using the mouse wheel (fast mode).</summary>
        public float FastZoomSensitivity = 50f;

        private Transform tf;
        private bool looking = false;

        #endregion


        #region MonoBehaviour methods

        public void Start()
        {
            tf = transform;
        }

        public void Update()
        {
            bool fastMode = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            float movementSpeed = fastMode ? this.FastMovementSpeed : this.MovementSpeed;

            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                tf.position = tf.position + (-tf.right * movementSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                tf.position = tf.position + (tf.right * movementSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
            {
                tf.position = tf.position + (tf.forward * movementSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                tf.position = tf.position + (-tf.forward * movementSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.Q))
            {
                tf.position = tf.position + (tf.up * movementSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.E))
            {
                tf.position = tf.position + (-tf.up * movementSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.R) || Input.GetKey(KeyCode.PageUp))
            {
                tf.position = tf.position + (Vector3.up * movementSpeed * Time.deltaTime);
            }

            if (Input.GetKey(KeyCode.F) || Input.GetKey(KeyCode.PageDown))
            {
                tf.position = tf.position + (-Vector3.up * movementSpeed * Time.deltaTime);
            }

            if (looking)
            {
                float newRotationX = tf.localEulerAngles.y + Input.GetAxis("Mouse X") * FreeLookSensitivity;
                float newRotationY = tf.localEulerAngles.x - Input.GetAxis("Mouse Y") * FreeLookSensitivity;
                tf.localEulerAngles = new Vector3(newRotationY, newRotationX, 0f);
            }

            float axis = Input.GetAxis("Mouse ScrollWheel");
            if (axis != 0)
            {
                var zoomSensitivity = fastMode ? this.FastZoomSensitivity : this.ZoomSensitivity;
                tf.position = tf.position + tf.forward * axis * zoomSensitivity;
            }

            if (Input.GetKeyDown(KeyCode.Mouse1))
            {
                StartLooking();
            }
            else if (Input.GetKeyUp(KeyCode.Mouse1))
            {
                StopLooking();
            }
        }

        public void OnDisable()
        {
            StopLooking();
        }

        #endregion


        #region Public methods

        /// <summary>
        /// Enable free looking.
        /// </summary>
        public void StartLooking()
        {
            looking = true;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        /// <summary>
        /// Disable free looking.
        /// </summary>
        public void StopLooking()
        {
            looking = false;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        #endregion
    }
}
// © 2019 crosstales LLC (https://www.crosstales.com)