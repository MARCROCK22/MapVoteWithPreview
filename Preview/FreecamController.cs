using UnityEngine;

namespace MapVoteWithPreview.Preview
{
    public class FreecamController : MonoBehaviour
    {
        private float _yaw;
        private float _pitch;
        private float _speed;

        public void Initialize(float speed, Vector3 startPosition)
        {
            _speed = speed;
            transform.position = startPosition;

            var cam = gameObject.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            cam.fieldOfView = 75f;
            cam.depth = 100f;

            gameObject.AddComponent<AudioListener>();

            _yaw = transform.eulerAngles.y;
            _pitch = transform.eulerAngles.x;

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            _yaw += UnityEngine.Input.GetAxis("Mouse X") * 3f;
            _pitch -= UnityEngine.Input.GetAxis("Mouse Y") * 3f;
            _pitch = Mathf.Clamp(_pitch, -90f, 90f);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            float currentSpeed = _speed;
            if (UnityEngine.Input.GetKey(KeyCode.LeftShift))
                currentSpeed *= 3f;

            Vector3 move = Vector3.zero;
            if (UnityEngine.Input.GetKey(KeyCode.W)) move += transform.forward;
            if (UnityEngine.Input.GetKey(KeyCode.S)) move -= transform.forward;
            if (UnityEngine.Input.GetKey(KeyCode.A)) move -= transform.right;
            if (UnityEngine.Input.GetKey(KeyCode.D)) move += transform.right;
            if (UnityEngine.Input.GetKey(KeyCode.Space)) move += Vector3.up;
            if (UnityEngine.Input.GetKey(KeyCode.LeftControl)) move -= Vector3.up;

            transform.position += move.normalized * currentSpeed * Time.deltaTime;
        }

        private void OnDestroy()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
