using UnityEngine;

namespace SavingSystem
{
    /// <summary>
    /// Debug UI component for the World Saving System.
    /// Displays statistics and provides testing controls.
    /// </summary>
    public class WorldSavingSystemDebugUI : MonoBehaviour
    {
        #region Inspector Fields

        [Header("Display")]
        [SerializeField] private bool _showUI = true;
        [SerializeField] private KeyCode _toggleKey = KeyCode.F3;

        [Header("Position")]
        [SerializeField] private float _xOffset = 10f;
        [SerializeField] private float _yOffset = 10f;

        [Header("Style")]
        [SerializeField] private int _fontSize = 14;
        [SerializeField] private Color _backgroundColor = new Color(0, 0, 0, 0.7f);
        [SerializeField] private Color _textColor = Color.white;
        [SerializeField] private Color _dirtyColor = Color.yellow;

        #endregion

        #region Private Fields

        private WorldSavingSystem _savingSystem;
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _buttonStyle;
        private bool _stylesInitialized;
        private string _newSaveName = "TestSave";

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            _savingSystem = WorldSavingSystem.Instance;
        }

        private void Update()
        {
            if (Input.GetKeyDown(_toggleKey))
            {
                _showUI = !_showUI;
            }
        }

        private void OnGUI()
        {
            if (!_showUI || _savingSystem == null)
                return;

            InitializeStyles();

            float width = 300f;
            float height = 350f;
            Rect windowRect = new Rect(_xOffset, _yOffset, width, height);

            GUI.Box(windowRect, "", _boxStyle);

            GUILayout.BeginArea(new Rect(windowRect.x + 10, windowRect.y + 10, windowRect.width - 20, windowRect.height - 20));

            // Title
            GUILayout.Label("World Saving System", _labelStyle);
            GUILayout.Space(5);

            // Statistics
            DrawStatistics();
            GUILayout.Space(10);

            // Save/Load controls
            DrawSaveLoadControls();
            GUILayout.Space(10);

            // Chunk info
            DrawChunkInfo();

            GUILayout.EndArea();
        }

        #endregion

        #region UI Sections

        private void DrawStatistics()
        {
            GUILayout.Label("=== Statistics ===", _labelStyle);

            string stats = _savingSystem.GetDebugStats();
            GUILayout.Label(stats, _labelStyle);

            if (_savingSystem.HasUnsavedChanges)
            {
                GUIStyle dirtyStyle = new GUIStyle(_labelStyle);
                dirtyStyle.normal.textColor = _dirtyColor;
                GUILayout.Label("* Unsaved changes!", dirtyStyle);
            }

            GUILayout.Label($"Play Time: {FormatTime(_savingSystem.PlayTime)}", _labelStyle);
        }

        private void DrawSaveLoadControls()
        {
            GUILayout.Label("=== Save/Load ===", _labelStyle);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Save Name:", _labelStyle, GUILayout.Width(80));
            _newSaveName = GUILayout.TextField(_newSaveName, GUILayout.Width(180));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Full Save", _buttonStyle))
            {
                _savingSystem.SaveGame(_newSaveName);
            }
            if (GUILayout.Button("Delta Save", _buttonStyle))
            {
                _savingSystem.SaveDelta(_newSaveName);
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Load", _buttonStyle))
            {
                if (_savingSystem.SaveExists(_newSaveName))
                {
                    _savingSystem.LoadGame(_newSaveName);
                }
                else
                {
                    Debug.LogWarning($"Save '{_newSaveName}' does not exist");
                }
            }
            if (GUILayout.Button("New Game", _buttonStyle))
            {
                _savingSystem.NewGame(_newSaveName);
            }
            GUILayout.EndHorizontal();

            // List available saves
            string[] saves = _savingSystem.GetAvailableSaves();
            if (saves.Length > 0)
            {
                GUILayout.Label($"Available Saves: {string.Join(", ", saves)}", _labelStyle);
            }
        }

        private void DrawChunkInfo()
        {
            GUILayout.Label("=== Chunk Info ===", _labelStyle);

            GUILayout.Label($"Center Chunk: {_savingSystem.CurrentCenterChunk}", _labelStyle);
            GUILayout.Label($"Chunk Size: {_savingSystem.ChunkSize}", _labelStyle);
            GUILayout.Label($"Active Extents: {_savingSystem.ActiveChunkHalfExtents}", _labelStyle);

            Vector3 pos = _savingSystem.transform.position;
            GUILayout.Label($"World Position: ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})", _labelStyle);
        }

        #endregion

        #region Helpers

        private void InitializeStyles()
        {
            if (_stylesInitialized)
                return;

            _boxStyle = new GUIStyle(GUI.skin.box);
            Texture2D bgTexture = new Texture2D(1, 1);
            bgTexture.SetPixel(0, 0, _backgroundColor);
            bgTexture.Apply();
            _boxStyle.normal.background = bgTexture;

            _labelStyle = new GUIStyle(GUI.skin.label);
            _labelStyle.normal.textColor = _textColor;
            _labelStyle.fontSize = _fontSize;
            _labelStyle.wordWrap = true;

            _buttonStyle = new GUIStyle(GUI.skin.button);
            _buttonStyle.fontSize = _fontSize;

            _stylesInitialized = true;
        }

        private string FormatTime(float seconds)
        {
            int hours = Mathf.FloorToInt(seconds / 3600f);
            int minutes = Mathf.FloorToInt((seconds % 3600f) / 60f);
            int secs = Mathf.FloorToInt(seconds % 60f);
            return $"{hours:D2}:{minutes:D2}:{secs:D2}";
        }

        #endregion
    }
}
