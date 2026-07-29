using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// Main controller responsible for handling
/// Position, Rotation, and Scale manipulation
/// of a target Transform through UI controls.
/// </summary>
public class ModelController : MonoBehaviour
{
    #region ===== Nested Types =====

    /// <summary>
    /// Represents a single axis control (X, Y, Z) for Position.
    /// Contains UI references and the current value.
    /// </summary>
    [System.Serializable]
    public class AxisControl
    {
        public Text valueText;
        public Button plusButton;
        public Button minusButton;

        [HideInInspector]
        public float value;
    }

    /// <summary>
    /// Represents a single axis control (X, Y, Z) for Rotation.
    /// Contains a slider and its corresponding UI label.
    /// </summary>
    [System.Serializable]
    public class RotationControl
    {
        public Slider slider;
        public Text valueText;
    }

    #endregion

    #region ===== Inspector References =====

    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Model View")]
    [SerializeField] private Text title;
    [SerializeField] private Button resetAll;

    // -------- POSITION --------
    [Header("Position")]
    [SerializeField] private List<AxisControl> positionAxes;
    [SerializeField] private Button resetPosition;
    [SerializeField] private bool useLocalPosition = true;
    [SerializeField] private float positionStep = 1f;

    // -------- ROTATION --------
    [Header("Rotation")]
    [SerializeField] private List<RotationControl> rotationAxes;
    [SerializeField] private Button resetRotation;
    [SerializeField] private bool useLocalRotation = true;

    // -------- SCALE --------
    [Header("Scale")]
    [SerializeField] private Text scaleText;
    [SerializeField] private Button scalePlus;
    [SerializeField] private Button scaleMinus;
    [SerializeField] private Button resetScale;
    [SerializeField] private float scaleStep = 0.1f;

    private float currentScale = 1f;

    #endregion

    #region ===== Unity Lifecycle =====

    private void OnEnable()
    {
        SetupPosition();
        SetupRotation();
        SetupScale();

        if (resetAll != null)
        {
            resetAll.onClick.RemoveListener(ResetAllTransforms);
            resetAll.onClick.AddListener(ResetAllTransforms);
        }
    }

    private void OnDisable()
    {
        // Reset general
        if (resetAll != null)
            resetAll.onClick.RemoveListener(ResetAllTransforms);

        // Position
        if (resetPosition != null)
            resetPosition.onClick.RemoveListener(ResetPosition);

        for (int i = 0; i < positionAxes.Count; i++)
        {
            int index = i;

            if (positionAxes[i].plusButton != null)
                positionAxes[i].plusButton.onClick.RemoveListener(() => ChangePositionAxis(index, positionStep));

            if (positionAxes[i].minusButton != null)
                positionAxes[i].minusButton.onClick.RemoveListener(() => ChangePositionAxis(index, -positionStep));
        }

        // Rotation
        if (resetRotation != null)
            resetRotation.onClick.RemoveListener(ResetRotation);

        foreach (var axis in rotationAxes)
        {
            if (axis.slider != null)
                axis.slider.onValueChanged.RemoveListener(OnRotationChanged);
        }

        // Scale
        if (scalePlus != null)
            scalePlus.onClick.RemoveListener(IncreaseScale);

        if (scaleMinus != null)
            scaleMinus.onClick.RemoveListener(DecreaseScale);

        if (resetScale != null)
            resetScale.onClick.RemoveListener(ResetScale);
    }
    /// <summary>
    /// Updates the actual model parameters
    /// </summary>
    public void UpdateDashboard()
    {
        UpdateScaleText();
        UpdatePositionText();
        UpdateRotationText();
    }

    #endregion

    #region ===== POSITION =====

    /// <summary>
    /// Initializes position UI listeners.
    /// </summary>
    private void SetupPosition()
    {
        if (resetPosition != null)
        {
            resetPosition.onClick.RemoveListener(ResetPosition);
            resetPosition.onClick.AddListener(ResetPosition);
        }

        for (int i = 0; i < positionAxes.Count; i++)
        {
            int index = i;

            if (positionAxes[i].plusButton != null)
            {
                positionAxes[i].plusButton.onClick.RemoveAllListeners();
                positionAxes[i].plusButton.onClick.AddListener(() => ChangePositionAxis(index, positionStep));
            }

            if (positionAxes[i].minusButton != null)
            {
                positionAxes[i].minusButton.onClick.RemoveAllListeners();
                positionAxes[i].minusButton.onClick.AddListener(() => ChangePositionAxis(index, -positionStep));
            }
        }

        UpdatePositionText();
    }

    /// <summary>
    /// Changes a specific position axis value.
    /// </summary>
    private void ChangePositionAxis(int index, float delta)
    {
        if (target == null || positionAxes.Count < 3) return;

        Vector3 currentPosition = useLocalPosition ? target.localPosition : target.position;

        positionAxes[0].value = currentPosition.x;
        positionAxes[1].value = currentPosition.y;
        positionAxes[2].value = currentPosition.z;

        positionAxes[index].value += delta;

        ApplyPosition();
        UpdatePositionText();
    }

    /// <summary>
    /// Applies position values to the target Transform.
    /// </summary>
    private void ApplyPosition()
    {
        if (target == null || positionAxes.Count < 3) return;

        Vector3 newPosition = new Vector3(
            positionAxes[0].value,
            positionAxes[1].value,
            positionAxes[2].value
        );

        if (useLocalPosition)
            target.localPosition = newPosition;
        else
            target.position = newPosition;
    }

    /// <summary>
    /// Resets position to (0,0,0).
    /// </summary>
    private void ResetPosition()
    {
        if (target == null) return;

        foreach (var axis in positionAxes)
            axis.value = 0f;

        ApplyPosition();
        UpdatePositionText();
    }

    /// <summary>
    /// Updates position UI labels.
    /// </summary>
    private void UpdatePositionText()
    {
        if (target == null) return;

        if (positionAxes.Count > 0 && positionAxes[0].valueText != null)
            positionAxes[0].valueText.text = $"X: {target.localPosition.x:F2}";

        if (positionAxes.Count > 1 && positionAxes[1].valueText != null)
            positionAxes[1].valueText.text = $"Y: {target.localPosition.y:F2}";

        if (positionAxes.Count > 2 && positionAxes[2].valueText != null)
            positionAxes[2].valueText.text = $"Z: {target.localPosition.z:F2}";
    }

    #endregion

    #region ===== ROTATION =====

    /// <summary>
    /// Initializes rotation UI listeners.
    /// </summary>
    private void SetupRotation()
    {
        if (resetRotation != null)
        {
            resetRotation.onClick.RemoveListener(ResetRotation);
            resetRotation.onClick.AddListener(ResetRotation);
        }

        foreach (var axis in rotationAxes)
        {
            if (axis.slider != null)
            {
                axis.slider.onValueChanged.RemoveListener(OnRotationChanged);
                axis.slider.onValueChanged.AddListener(OnRotationChanged);
            }
        }

        UpdateRotationText();
        UpdateSliders();
    }

    /// <summary>
    /// Triggered when any rotation slider value changes.
    /// </summary>
    private void OnRotationChanged(float _)
    {
        ApplyRotation();
    }

    /// <summary>
    /// Applies rotation slider values to the target.
    /// </summary>
    private void ApplyRotation()
    {
        if (target == null || rotationAxes.Count < 3) return;

        float x = rotationAxes[0].slider.value * 360f;
        float y = rotationAxes[1].slider.value * 360f;
        float z = rotationAxes[2].slider.value * 360f;

        Quaternion rotation = Quaternion.Euler(x, y, z);

        if (useLocalRotation)
            target.localRotation = rotation;
        else
            target.rotation = rotation;

        UpdateRotationText(x, y, z);
    }

    /// <summary>
    /// Resets rotation to identity (0,0,0).
    /// </summary>
    private void ResetRotation()
    {
        if (target == null) return;

        if (useLocalRotation)
            target.localRotation = Quaternion.identity;
        else
            target.rotation = Quaternion.identity;

        foreach (var axis in rotationAxes)
        {
            if (axis.slider != null)
                axis.slider.SetValueWithoutNotify(0f);
        }

        UpdateRotationText();
    }

    /// <summary>
    /// Updates rotation UI labels.
    /// </summary>
    private void UpdateRotationText()
    {
        if (target == null) return;

        if (rotationAxes.Count > 0 && rotationAxes[0].valueText != null)
            rotationAxes[0].valueText.text = $"X: {target.localEulerAngles.x:F1}°";

        if (rotationAxes.Count > 1 && rotationAxes[1].valueText != null)
            rotationAxes[1].valueText.text = $"Y: {target.localEulerAngles.y:F1}°";

        if (rotationAxes.Count > 2 && rotationAxes[2].valueText != null)
            rotationAxes[2].valueText.text = $"Z: {target.localEulerAngles.z:F1}°";
    }

    /// <summary>
    /// This function is used by the sliders on change showing the output of the change
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    private void UpdateRotationText(float x, float y, float z)
    {
        if (target == null) return;

        if (rotationAxes.Count > 0 && rotationAxes[0].valueText != null)
            rotationAxes[0].valueText.text = $"X: {x:F1}°";

        if (rotationAxes.Count > 1 && rotationAxes[1].valueText != null)
            rotationAxes[1].valueText.text = $"Y: {y:F1}°";

        if (rotationAxes.Count > 2 && rotationAxes[2].valueText != null)
            rotationAxes[2].valueText.text = $"Z: {z:F1}°";
    }


    public void DesactivateSliders()
    {
        foreach (var axis in rotationAxes)
        {
            if (axis.slider != null)
                axis.slider.onValueChanged.RemoveListener(OnRotationChanged);
        }
    }

    public void UpdateSliders()
    {
        if (target == null || rotationAxes.Count < 3) return;

        rotationAxes[0].slider.SetValueWithoutNotify(target.localEulerAngles.x / 360f);
        rotationAxes[1].slider.SetValueWithoutNotify(target.localEulerAngles.y / 360f);
        rotationAxes[2].slider.SetValueWithoutNotify(target.localEulerAngles.z / 360f);
    }

    public void ActivateSliders()
    {
        foreach (var axis in rotationAxes)
        {
            if (axis.slider != null)
            {
                axis.slider.onValueChanged.RemoveListener(OnRotationChanged);
                axis.slider.onValueChanged.AddListener(OnRotationChanged);
            }
        }
    }

    #endregion

    #region ===== SCALE =====

    /// <summary>
    /// Initializes scale UI listeners.
    /// </summary>
    private void SetupScale()
    {
        if (scalePlus != null)
        {
            scalePlus.onClick.RemoveListener(IncreaseScale);
            scalePlus.onClick.AddListener(IncreaseScale);
        }

        if (scaleMinus != null)
        {
            scaleMinus.onClick.RemoveListener(DecreaseScale);
            scaleMinus.onClick.AddListener(DecreaseScale);
        }

        if (resetScale != null)
        {
            resetScale.onClick.RemoveListener(ResetScale);
            resetScale.onClick.AddListener(ResetScale);
        }

        UpdateScaleText();
    }

    private void IncreaseScale()
    {
        ChangeScale(scaleStep);
    }

    private void DecreaseScale()
    {
        ChangeScale(-scaleStep);
    }

    /// <summary>
    /// Changes the scale value.
    /// </summary>
    private void ChangeScale(float delta)
    {
        if (target == null) return;

        currentScale = target.localScale.x;
        currentScale += delta;
        currentScale = Mathf.Max(0.1f, currentScale);

        ApplyScale();
        UpdateScaleText();
    }

    /// <summary>
    /// Applies scale to the target Transform.
    /// </summary>
    private void ApplyScale()
    {
        if (target == null) return;

        target.localScale = Vector3.one * currentScale;
    }

    /// <summary>
    /// Resets scale to default (1,1,1).
    /// </summary>
    private void ResetScale()
    {
        if (target == null) return;

        currentScale = 1f;
        ApplyScale();
        UpdateScaleText();
    }

    /// <summary>
    /// Updates scale UI label.
    /// </summary>
    private void UpdateScaleText()
    {
        if (target == null) return;

        if (scaleText != null)
            scaleText.text = $"Scale: {target.localScale.x:F2}";
    }

    #endregion

    #region ===== RESET ALL =====

    private void ResetAllTransforms()
    {
        ResetPosition();
        ResetRotation();
        ResetScale();
    }

    #endregion
}