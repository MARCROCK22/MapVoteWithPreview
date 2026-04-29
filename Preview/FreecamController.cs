using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

using MapVoteWithPreview;

namespace MapVoteWithPreview.Preview
{
    public class FreecamController : MonoBehaviour
    {
        private float _yaw;
        private float _pitch;
        private float _speed;
        private PostProcessVolume _ppVolume;
        private RenderTexture _renderTexture;
        private Camera _camera;

        public void Initialize(float speed, Vector3 startPosition)
        {
            _speed = speed;
            transform.position = startPosition;

            var cam = gameObject.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1000f;
            cam.fieldOfView = 75f;
            cam.depth = 100f;

            // Render at lower resolution for the retro/pixelated look (like the game)
            // The game uses a render texture at reduced resolution
            int pixelWidth = Screen.width / 3;
            int pixelHeight = Screen.height / 3;
            var renderTex = new RenderTexture(pixelWidth, pixelHeight, 24);
            renderTex.filterMode = FilterMode.Point; // no smoothing = pixelated
            cam.targetTexture = renderTex;
            _renderTexture = renderTex;
            _camera = cam;

            // Add post-processing layer to camera
            try
            {
                var ppLayer = gameObject.AddComponent<PostProcessLayer>();
                ppLayer.volumeTrigger = transform;
                ppLayer.volumeLayer = ~0;

                // Init with PostProcessResources from the game
                var resources = Resources.FindObjectsOfTypeAll<PostProcessResources>();
                if (resources != null && resources.Length > 0)
                {
                    ppLayer.Init(resources[0]);
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Log.LogWarning($"[PREVIEW] PostProcessLayer setup failed: {ex.Message}");
            }

            gameObject.AddComponent<AudioListener>();

            _yaw = transform.eulerAngles.y;
            _pitch = transform.eulerAngles.x;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>
        /// Apply post-processing effects matching the level's visual settings.
        /// </summary>
        public void ApplyLevelEffects(Level level)
        {
            // Create a post-process volume on the camera
            var ppObj = new GameObject("PreviewPostProcess");
            ppObj.hideFlags = HideFlags.HideAndDontSave;
            ppObj.transform.SetParent(transform, false);

            _ppVolume = ppObj.AddComponent<PostProcessVolume>();
            _ppVolume.isGlobal = true;
            _ppVolume.priority = 100f;

            var profile = ScriptableObject.CreateInstance<PostProcessProfile>();

            // Bloom — reduce intensity from level value (game's pipeline handles it differently)
            var bloom = ScriptableObject.CreateInstance<Bloom>();
            bloom.enabled.value = true;
            bloom.intensity.overrideState = true;
            bloom.intensity.value = Mathf.Min(level.BloomIntensity * 0.3f, 3f);
            bloom.threshold.overrideState = true;
            bloom.threshold.value = Mathf.Max(level.BloomThreshold, 1.0f);
            profile.AddSettings(bloom);

            // Color Grading
            var colorGrading = ScriptableObject.CreateInstance<ColorGrading>();
            colorGrading.enabled.value = true;
            colorGrading.colorFilter.overrideState = true;
            colorGrading.colorFilter.value = level.ColorFilter;
            colorGrading.temperature.overrideState = true;
            colorGrading.temperature.value = level.ColorTemperature;
            profile.AddSettings(colorGrading);

            // Vignette
            var vignette = ScriptableObject.CreateInstance<Vignette>();
            vignette.enabled.value = true;
            vignette.color.overrideState = true;
            vignette.color.value = level.VignetteColor;
            vignette.intensity.overrideState = true;
            vignette.intensity.value = level.VignetteIntensity;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = level.VignetteSmoothness;
            profile.AddSettings(vignette);

            // Chromatic Aberration
            var chromaticAberration = ScriptableObject.CreateInstance<ChromaticAberration>();
            chromaticAberration.enabled.value = true;
            chromaticAberration.intensity.overrideState = true;
            chromaticAberration.intensity.value = 0.15f;
            profile.AddSettings(chromaticAberration);

            // Grain
            var grain = ScriptableObject.CreateInstance<Grain>();
            grain.enabled.value = true;
            grain.intensity.overrideState = true;
            grain.intensity.value = 0.3f;
            grain.size.overrideState = true;
            grain.size.value = 1.2f;
            profile.AddSettings(grain);

            // Motion Blur (subtle)
            var motionBlur = ScriptableObject.CreateInstance<MotionBlur>();
            motionBlur.enabled.value = true;
            motionBlur.shutterAngle.overrideState = true;
            motionBlur.shutterAngle.value = 150f;
            motionBlur.sampleCount.overrideState = true;
            motionBlur.sampleCount.value = 8;
            profile.AddSettings(motionBlur);

            // Auto Exposure
            var autoExposure = ScriptableObject.CreateInstance<AutoExposure>();
            autoExposure.enabled.value = true;
            autoExposure.minLuminance.overrideState = true;
            autoExposure.minLuminance.value = -2f;
            autoExposure.maxLuminance.overrideState = true;
            autoExposure.maxLuminance.value = 2f;
            profile.AddSettings(autoExposure);

            _ppVolume.profile = profile;

            Plugin.Log.LogInfo($"[PREVIEW] Applied post-processing: bloom={level.BloomIntensity}, colorFilter={level.ColorFilter}, vignette={level.VignetteIntensity}, +chromaticAberration, grain, motionBlur, autoExposure");
        }

        private void Update()
        {
            if (UnityEngine.Input.GetMouseButton(1))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                _yaw += UnityEngine.Input.GetAxis("Mouse X") * 3f;
                _pitch -= UnityEngine.Input.GetAxis("Mouse Y") * 3f;
                _pitch = Mathf.Clamp(_pitch, -90f, 90f);
                transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

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

        private void OnGUI()
        {
            // Draw the low-res render texture to the full screen (pixelated look)
            if (_renderTexture != null && Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), _renderTexture, ScaleMode.StretchToFill);
            }
        }

        private void OnDestroy()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (_ppVolume != null && _ppVolume.profile != null)
                Destroy(_ppVolume.profile);

            if (_renderTexture != null)
            {
                if (_camera != null) _camera.targetTexture = null;
                Destroy(_renderTexture);
            }
        }
    }
}
