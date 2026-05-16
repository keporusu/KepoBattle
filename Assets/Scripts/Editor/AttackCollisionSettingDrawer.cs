using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(AttackCollisionSetting))]
public class AttackCollisionSettingDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        var shapeProp     = property.FindPropertyRelative("shape");
        var offsetProp    = property.FindPropertyRelative("offset");

        float lineH = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        var rect = new Rect(position.x, position.y, position.width, lineH);

        EditorGUI.PropertyField(rect, shapeProp);
        rect.y += lineH + spacing;

        var shape = (CollisionShape)shapeProp.enumValueIndex;

        switch (shape)
        {
            case CollisionShape.Circle:
                EditorGUI.PropertyField(rect, property.FindPropertyRelative("circleRadius"));
                rect.y += lineH + spacing;
                break;

            case CollisionShape.Capsule:
                EditorGUI.PropertyField(rect, property.FindPropertyRelative("capsuleRadius"));
                rect.y += lineH + spacing;
                EditorGUI.PropertyField(rect, property.FindPropertyRelative("capsuleHeight"));
                rect.y += lineH + spacing;
                EditorGUI.PropertyField(rect, property.FindPropertyRelative("capsuleDirection"));
                rect.y += lineH + spacing;
                break;

            case CollisionShape.Box:
                EditorGUI.PropertyField(rect, property.FindPropertyRelative("boxSize"));
                rect.y += lineH + spacing;
                break;
        }

        EditorGUI.PropertyField(rect, offsetProp);
        rect.y += lineH + spacing;

        EditorGUI.Slider(rect, property.FindPropertyRelative("spanStart"), 0f, 1f, "Span Start");
        rect.y += lineH + spacing;
        EditorGUI.Slider(rect, property.FindPropertyRelative("spanEnd"),   0f, 1f, "Span End");

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float lineH = EditorGUIUtility.singleLineHeight;
        float spacing = EditorGUIUtility.standardVerticalSpacing;

        var shapeProp = property.FindPropertyRelative("shape");
        var shape = (CollisionShape)shapeProp.enumValueIndex;

        // shape + offset + spanStart + spanEnd の 4 行は共通
        int lines = 4;
        lines += shape switch
        {
            CollisionShape.Circle  => 1,          // circleRadius
            CollisionShape.Capsule => 3,          // radius + height + direction
            CollisionShape.Box     => 1,          // boxSize
            _                      => 0,
        };

        return lines * lineH + (lines - 1) * spacing;
    }
}
