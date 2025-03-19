using UnityEngine;
using UnityEditor;

namespace Mirror.VR.PasswordAttribute.Editor
{
    [CustomPropertyDrawer(typeof(PasswordFieldAttribute))]
    public class PasswordFieldAttributeDrawer : PropertyDrawer
    {
        private bool showpassword;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.String)
            {
                Rect passwordFieldPosition = new Rect(position.x, position.y, position.width - 50, position.height);
                Rect togglePosition = new Rect(position.xMax - 30, position.y, 50, position.height);

                if (showpassword)
                    property.stringValue = EditorGUI.TextField(passwordFieldPosition, label, property.stringValue);
                else
                    property.stringValue = EditorGUI.PasswordField(passwordFieldPosition, label, property.stringValue);

                showpassword = EditorGUI.Toggle(togglePosition, showpassword);
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight;
        }
    }
}
