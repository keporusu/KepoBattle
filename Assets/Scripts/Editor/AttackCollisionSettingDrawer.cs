using UnityEditor;
using UnityEngine;
using Data;

namespace Editor
{
    [CustomPropertyDrawer(typeof(AttackCollisionSetting))]
    public class AttackCollisionSettingDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var shapeProp  = property.FindPropertyRelative("shape");
            var offsetProp = property.FindPropertyRelative("offset");

            float lineH   = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            var rect = new Rect(position.x, position.y, position.width, lineH);

            EditorGUI.PropertyField(rect, shapeProp);
            rect.y += lineH + spacing;

            var shape = (ColliderShape)shapeProp.enumValueIndex;

            switch (shape)
            {
                case ColliderShape.Circle:
                    EditorGUI.PropertyField(rect, property.FindPropertyRelative("circleRadius"));
                    rect.y += lineH + spacing;
                    break;

                case ColliderShape.Capsule:
                    EditorGUI.PropertyField(rect, property.FindPropertyRelative("capsuleRadius"));
                    rect.y += lineH + spacing;
                    EditorGUI.PropertyField(rect, property.FindPropertyRelative("capsuleHeight"));
                    rect.y += lineH + spacing;
                    EditorGUI.PropertyField(rect, property.FindPropertyRelative("capsuleDirection"));
                    rect.y += lineH + spacing;
                    break;

                case ColliderShape.Box:
                    EditorGUI.PropertyField(rect, property.FindPropertyRelative("boxSize"));
                    rect.y += lineH + spacing;
                    break;
            }

            EditorGUI.PropertyField(rect, offsetProp);
            rect.y += lineH + spacing;

            EditorGUI.PropertyField(rect, property.FindPropertyRelative("attackPower"));
            rect.y += lineH + spacing;

            EditorGUI.PropertyField(rect, property.FindPropertyRelative("damage"));

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float lineH   = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            var shapeProp = property.FindPropertyRelative("shape");
            var shape     = (ColliderShape)shapeProp.enumValueIndex;

            // shape + offset + attackPower + damage の 4 行は共通
            int lines = 4;
            lines += shape switch
            {
                ColliderShape.Circle  => 1,
                ColliderShape.Capsule => 3,
                ColliderShape.Box     => 1,
                _                     => 0,
            };

            return lines * lineH + (lines - 1) * spacing;
        }
    }

    [CustomPropertyDrawer(typeof(AttackCollisionSettingForAction))]
    public class AttackCollisionSettingForActionDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            float lineH   = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            //形状・攻撃情報の描画は AttackCollisionSettingDrawer に任せる
            var collisionProp = property.FindPropertyRelative("collision");
            float collisionH  = EditorGUI.GetPropertyHeight(collisionProp);

            var rect = new Rect(position.x, position.y, position.width, collisionH);
            EditorGUI.PropertyField(rect, collisionProp, GUIContent.none, true);
            rect.y += collisionH + spacing;

            //この構造体だけが持つ、発生区間
            rect.height = lineH;
            EditorGUI.Slider(rect, property.FindPropertyRelative("spanStart"), 0f, 1f, "Span Start");
            rect.y += lineH + spacing;
            EditorGUI.Slider(rect, property.FindPropertyRelative("spanEnd"),   0f, 1f, "Span End");

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float lineH   = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            float collisionH = EditorGUI.GetPropertyHeight(property.FindPropertyRelative("collision"));

            // collision + spanStart + spanEnd
            return collisionH + (lineH + spacing) * 2;
        }
    }
}
