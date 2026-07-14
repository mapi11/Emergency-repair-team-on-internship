using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;


    public class BooleanToggleVisualsController : MonoBehaviour
    {
        const float k_TargetPositionX = 17f;

        [SerializeField, Tooltip("The boolean toggle knob.")]
        RectTransform m_Knob;

        [SerializeField, Tooltip("How much to translate the button imagery on the z on hover.")]
        float m_ZTranslation = 5f;

        Toggle m_Toggle;

        void Awake()
        {
            m_Toggle = gameObject.GetComponent<Toggle>();

            m_Toggle.onValueChanged.AddListener(ToggleValueChanged);
        }

        void OnEnable()
        {
            ToggleValueChanged(m_Toggle.isOn);
        }

        void ToggleValueChanged(bool value)
        {
            if (value)
            {
                m_Knob.localPosition = new Vector3(k_TargetPositionX, m_Knob.localPosition.y, m_Knob.localPosition.z);
            }
            else
            {
                m_Knob.localPosition = new Vector3(-k_TargetPositionX, m_Knob.localPosition.y, m_Knob.localPosition.z);
            }
        }
    }