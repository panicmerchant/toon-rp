﻿using ToonRP.Runtime.Shadows;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace ToonRP.Editor
{
    [CustomPropertyDrawer(typeof(ToonShadowSettings))]
    public class ToonShadowsSettingsPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var root = new VisualElement();

            SerializedProperty modeProperty =
                property.FindPropertyRelative(nameof(ToonShadowSettings.Mode));
            SerializedProperty crispAntiAliasedProperty =
                property.FindPropertyRelative(nameof(ToonShadowSettings.CrispAntiAliased));
            var crispAntiAliasedField = new PropertyField(crispAntiAliasedProperty);
            var smoothnessField =
                new PropertyField(property.FindPropertyRelative(nameof(ToonShadowSettings.Smoothness)));
            var modeField = new PropertyField(modeProperty);

            var enabledContainer = new VisualElement();
            var vsmContainer = new VisualElement();

            void RefreshFields()
            {
                var mode = (ToonShadowSettings.ShadowMode) modeProperty.intValue;
                enabledContainer.SetVisible(mode != ToonShadowSettings.ShadowMode.Off);
                vsmContainer.SetVisible(mode == ToonShadowSettings.ShadowMode.Vsm);
                smoothnessField.SetEnabled(!crispAntiAliasedProperty.boolValue);
            }

            RefreshFields();

            modeField.RegisterValueChangeCallback(_ => RefreshFields());
            crispAntiAliasedField.RegisterValueChangeCallback(_ => RefreshFields());

            root.Add(new ToonRpHeaderLabel("Shadows"));
            root.Add(modeField);

            // ramp
            {
                enabledContainer.Add(
                    new PropertyField(property.FindPropertyRelative(nameof(ToonShadowSettings.Threshold)))
                );
                enabledContainer.Add(crispAntiAliasedField);
                enabledContainer.Add(smoothnessField);
            }

            {
                vsmContainer.Add(new PropertyField(property.FindPropertyRelative(nameof(ToonShadowSettings.Vsm)))
                    { label = "VSM" }
                );
                enabledContainer.Add(vsmContainer);
            }


            root.Add(enabledContainer);
            return root;
        }
    }
}