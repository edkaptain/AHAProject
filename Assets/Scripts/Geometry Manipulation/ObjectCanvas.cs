using Oculus.Interaction;
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ObjectCanvas : MonoBehaviour
{
    #region ===== Types =====

    enum Types
    {
        Move, Rotate, Scale
    }

    #endregion

    #region ===== Inspector References =====

    [Header("Object")]
    [SerializeField] private GameObject target;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI title;
    [SerializeField] private TextMeshProUGUI selection;

    [Header("Buttons")]
    [SerializeField] private Button moveBtn;
    [SerializeField] private Button rotateBtn;
    [SerializeField] private Button ScaleBtn;

    [Header("PositionsControls")]
    [SerializeField] private GameObject grabMove;

    [Header("Position Settings")]
    [SerializeField] private GrabFreeTransformer grabFreeTransformer;

    [Header("Rotation Settings")]
    [SerializeField] private GameObject grabRotate;


    #endregion

    #region === Unity Lifecycle

    private void Awake()
    {
        if (target == null)
            target = transform.parent.gameObject;
    }

    private void OnEnable()
    {
        PositionMode();
        RotationMode();
        ScaleMode();
    }

    private void OnDisable()
    {
        if (moveBtn != null)
            moveBtn.onClick.RemoveListener(MoveObject);

        if (rotateBtn != null)
            rotateBtn.onClick.RemoveListener(RotateObject);

        if (ScaleBtn != null)
            ScaleBtn.onClick.RemoveListener(ScaleObject);
    }

    #endregion

    #region ===== POSITION ====

    /// <summary>
    /// Starts the move configuration for the 3D object
    /// </summary>
    private void PositionMode()
    {
        if (moveBtn != null)
            moveBtn.onClick.AddListener(MoveObject);
    }

    private void MoveObject()
    {
        if (!target || !selection) return;
        // Activa el movimiento con las manos
        grabMove.GetComponent<GrabInteractable>().enabled = true;
        target.GetComponent<Grabbable>().InjectOptionalOneGrabTransformer(grabFreeTransformer);
        target.GetComponent<Grabbable>().InjectOptionalTwoGrabTransformer(null);
        grabRotate.SetActive(false);
        selection.text = Types.Move.ToString();
    }

    #endregion

    #region ==== ROTATION =====

    private void RotationMode()
    {
        if (rotateBtn != null)
            rotateBtn.onClick.AddListener(RotateObject);
    }

    private void RotateObject()
    {
        if (!target || !selection) return;
        grabRotate.SetActive(true);
        grabMove.GetComponent<GrabInteractable>().enabled = false;
        selection.text = Types.Rotate.ToString();
    }

    #endregion

    #region ==== SCALE =====

    private void ScaleMode()
    {
        if (ScaleBtn != null)
            ScaleBtn.onClick.AddListener(ScaleObject);
    }

    private void ScaleObject()
    {
        if (!target || !selection) return;
        grabRotate.SetActive(false);
        grabMove.GetComponent<GrabInteractable>().enabled = true;
        target.GetComponent<Grabbable>().InjectOptionalOneGrabTransformer(null);
        target.GetComponent<Grabbable>().InjectOptionalTwoGrabTransformer(grabFreeTransformer);
        selection.text = Types.Scale.ToString();
    }

    #endregion
}