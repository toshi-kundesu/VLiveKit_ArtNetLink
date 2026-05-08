// VLiveKit is all Unlicense.
// unlicense: https://unlicense.org/

using toshi.VLiveKit.Lighting;
using UnityEditor;
using UnityEngine;

namespace toshi.VLiveKit.ArtNetLink.Editor
{
    [CustomEditor(typeof(VLiveArtNetReceiver))]
    public sealed class VLiveArtNetReceiverEditor : UnityEditor.Editor
    {
        SerializedProperty _universeToUse;
        SerializedProperty _connection;

        void OnEnable()
        {
            _universeToUse = serializedObject.FindProperty("_universeToUse");
            _connection = serializedObject.FindProperty("_connection");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Art-Net Receiver", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Universe To Use defaults to 0. Some lighting software shows Universe 1 while sending Universe 0; check the actual incoming universe with toshi/VLiveKit/Lighting/ArtNet Monitor.", MessageType.Info);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Receive", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(_universeToUse, new GUIContent("Universe To Use", "0-based Art-Net universe number to read."));
                EditorGUILayout.PropertyField(_connection, new GUIContent("Connection"));
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
