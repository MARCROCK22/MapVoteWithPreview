using ExitGames.Client.Photon;
using UnityEngine;

namespace MapVoteWithPreview.Preview
{
    /// <summary>
    /// Manages the map preview freecam feature.
    /// Attach this component to a persistent GameObject in Awake().
    /// </summary>
    internal class MapPreviewManager : MonoBehaviour
    {
        private static MapPreviewManager? _instance;

        private void Awake()
        {
            _instance = this;
        }

        /// <summary>
        /// Immediately closes any active map preview. Called before level transitions and voting finalization.
        /// </summary>
        public static void ForceClose()
        {
            if (_instance != null)
            {
                _instance.ClosePreview();
            }
        }

        private void ClosePreview()
        {
            // TODO: implement freecam teardown
        }

        /// <summary>
        /// Handles an OnPreviewStart networked event broadcast by another client.
        /// </summary>
        public static void HandlePreviewStart(EventData data)
        {
            // TODO: show remote player preview indicator
        }

        /// <summary>
        /// Handles an OnPreviewEnd networked event broadcast by another client.
        /// </summary>
        public static void HandlePreviewEnd(EventData data)
        {
            // TODO: hide remote player preview indicator
        }
    }
}
