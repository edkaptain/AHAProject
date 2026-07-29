using System.Collections;
using UnityEngine;
using static View_MPI;

public class MRPI : MonoBehaviour
{
    #region ===== Unity Inspector =====
    [System.Serializable]
    public class ViewData
    {
        public View_MPI.paths path;
        public GameObject viewObject;
        public View_MPI controller;
        public int currentSlice;
        

        [HideInInspector] public Renderer renderer;
    }

    [Header("Views")]
    [SerializeField] private ViewData[] views;

    private GameObject selectedSlicer;
    private int lastAxialIndex = -1;
    private int lastCoronalIndex = -1;
    private int lastSagittalIndex = -1;
    #endregion

    // Testing
    public ControllerVibration vib = new ControllerVibration();

    #region ====== Singlenton ======
    public static MRPI Instance { get; private set; }

    private void Awake()
    {

        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }
    #endregion

    #region ===== Functions =====

    private void Start()
    {
        foreach (ViewData view in views)
        {
            if (view.viewObject != null)
            {
                // Set up each render from each object
                view.renderer = view.viewObject.GetComponent<Renderer>();
            }

            if (view.renderer == null)
            {
                Debug.LogWarning($"Renderer missing for view: {view.path}");
            }
        }
    }

    private void Update()
    {
        if (selectedSlicer == null) return;

        HandleSlicerRuntime(selectedSlicer);
    }

    /// <summary>
    /// This function only updates the current slice value from the data retrieved by axiscontroller
    /// </summary>
    /// <param name="myPath"></param>
    /// <param name="sliceValue"></param>
    public void UpdateIndex(int sliceValue, View_MPI.paths myPath)
    {
        ViewData view = GetView(myPath);

        if (view == null || view.renderer == null)
            return;

        view.currentSlice = sliceValue;
    }

    /// <summary>
    /// Changes the current slicer picture from the sliders 
    /// </summary>
    /// <param name="texture"></param>
    /// <param name="mypath"></param>
    public void ChangeRender(Texture2D texture, View_MPI.paths mypath)
    {
        ViewData view = GetView(mypath);

        if (view == null || view.renderer == null)
            return;

        view.renderer.material.mainTexture = texture;
    }
    /// <summary>
    /// Retrieves the view data associated with the specified path, if it exists.
    /// </summary>
    /// <remarks>A warning is logged if no view is found for the specified path.</remarks>
    /// <param name="mypath">The path for which to retrieve the corresponding view data.</param>
    /// <returns>The view data associated with the specified path, or null if no matching view is found.</returns>

    private ViewData GetView(View_MPI.paths mypath)
    {
        foreach (ViewData view in views)
        {
            if (view.path == mypath)
                return view;
        }

        Debug.LogWarning($"No view found for path: {mypath}");
        return null;
    }
    /// <summary>
    /// Changes the current position to the specified value within the given path.
    /// </summary>
    /// <param name="value">The new position to set. The meaning and valid range depend on the implementation of the specified path.</param>
    /// <param name="mypath">The path in which to change the position. Must be a valid instance of <see cref="View_MPI.paths"/>.</param>
    public void ChangePosition(float value, View_MPI.paths mypath)
    {
        ViewData current = GetView(mypath);
        if (current == null || current.renderer == null)
            return;

        // Normalized value
        float norm = value - 0.5f;

        if (mypath == paths.Coronal)
        {
            norm = -0.25f + 0.25f * value;

            current.viewObject.transform.localPosition = new Vector3(norm, current.viewObject.transform.localPosition.y, current.viewObject.transform.localPosition.z);
        }
        else if (mypath == paths.Axial)
        {

            current.viewObject.transform.localPosition = new Vector3(current.viewObject.transform.localPosition.x, norm, current.viewObject.transform.localPosition.z);
        }
        else // Sagittal
        {
            current.viewObject.transform.localPosition = new Vector3(current.viewObject.transform.localPosition.x, current.viewObject.transform.localPosition.y, norm);
        }

    }

    public void OnHandleSlicer(GameObject myGameObject)
    {
        selectedSlicer = myGameObject; // se guarda cuando el usuario selecciona
    }


    private void HandleSlicerRuntime(GameObject myGameObject)
    {
        if (myGameObject == null)
            return;

        string axis = myGameObject.name;
        Transform position = myGameObject.transform;

        int totalSlices = 512;

        float value = 0f;
        float normalized = 0f;

        View_MPI.paths currentPath;

        // =========================
        // AXIS SELECTION
        // =========================

        if (axis == View_MPI.paths.Axial.ToString())
        {
            currentPath = View_MPI.paths.Axial;

            // Y axis
            value = position.localPosition.y;

            // Range: -0.5 -> 0.5
            normalized = Mathf.InverseLerp(-0.5f, 0.5f, value);
        }
        else if (axis == View_MPI.paths.Coronal.ToString())
        {
            totalSlices = 64;
            currentPath = View_MPI.paths.Coronal;

            // X axis
            value = position.localPosition.x;

            // Range: -0.25 -> 0
            normalized = Mathf.InverseLerp(-0.25f, 0f, value);
        }
        else if (axis == View_MPI.paths.Saggital.ToString())
        {
            currentPath = View_MPI.paths.Saggital;

            // Z axis
            value = position.localPosition.z;

            // Range: -0.5 -> 0.5
            normalized = Mathf.InverseLerp(-0.5f, 0.5f, value);
        }
        else
        {
            return;
        }

        // =========================
        // INDEX CALCULATION
        // =========================

        int currentIndex = Mathf.FloorToInt(normalized * (totalSlices - 1));

        currentIndex = Mathf.Clamp(currentIndex, 0, totalSlices - 1);

        // =========================
        // AVOID RELOADING SAME SLICE
        // =========================

        bool shouldUpdate = false;

        switch (currentPath)
        {
            case View_MPI.paths.Axial:

                if (currentIndex != lastAxialIndex)
                {
                    lastAxialIndex = currentIndex;
                    shouldUpdate = true;


                    OVRInput.Controller controller = OVRInput.Controller.None;

                    if (OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch) > 0.1f)
                    {
                        controller = OVRInput.Controller.RTouch;
                    }
                    else if (OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch) > 0.1f)
                    {
                        controller = OVRInput.Controller.LTouch;
                    }

                    if (controller != OVRInput.Controller.None)
                    {
                        vib.Vibrate(0.1f, controller);
                    }
                }

                break;

            case View_MPI.paths.Coronal:

                if (currentIndex != lastCoronalIndex)
                {
                    lastCoronalIndex = currentIndex;
                    shouldUpdate = true;


                    OVRInput.Controller controller = OVRInput.Controller.None;

                    if (OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch) > 0.1f)
                    {
                        controller = OVRInput.Controller.RTouch;
                    }
                    else if (OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch) > 0.1f)
                    {
                        controller = OVRInput.Controller.LTouch;
                    }

                    if (controller != OVRInput.Controller.None)
                    {
                        vib.Vibrate(0.1f, controller);
                    }
                }

                break;

            case View_MPI.paths.Saggital:

                if (currentIndex != lastSagittalIndex)
                {
                    lastSagittalIndex = currentIndex;
                    shouldUpdate = true;


                    OVRInput.Controller controller = OVRInput.Controller.None;

                    if (OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch) > 0.1f)
                    {
                        controller = OVRInput.Controller.RTouch;
                    }
                    else if (OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch) > 0.1f)
                    {
                        controller = OVRInput.Controller.LTouch;
                    }

                    if (controller != OVRInput.Controller.None)
                    {
                        vib.Vibrate(0.1f, controller);
                    }
                }

                break;
        }

        if (!shouldUpdate)
            return;

        Debug.Log($"[{currentPath}] Slice: {currentIndex}");

        // =========================
        // LOAD TEXTURE
        // =========================

        string path = $"{GetCustomPath(currentPath)}slice_{currentIndex}";

        Texture2D tex = Resources.Load<Texture2D>(path);

        if (tex == null)
        {
            Debug.LogWarning($"Texture not found: {path}");
            return;
        }

        // =========================
        // UPDATE VIEW
        // =========================

        UpdateSlicerView(currentIndex, currentPath);
        UpdateDashboardImage(myGameObject, currentIndex, currentPath);
        ChangeRender(tex, currentPath);
    }

    private void UpdateSlicerView(int current_index, View_MPI.paths myPath)
    {
        // Loads the texture from Resources folder in unity
        string path = $"{GetCustomPath(paths.Axial)}slice_{current_index}";

        Texture2D tex = Resources.Load<Texture2D>(path);

        // Changes the render
        ChangeRender(tex, myPath);


        // Updates the silder and ImageView
    }


    /// <summary>
    /// Returns the correct Axis selection from inspector
    /// </summary>
    /// <param name="path">Path parameter selection</param>
    /// <returns>Returns the path from resources</returns>
    public string GetCustomPath(paths path)
    {
        switch (path)
        {
            case paths.Axial:
                return "image_axial/";
            case paths.Coronal:
                return "image_coronal/";
            case paths.Saggital:
                return "image_saggital/";

            default:
                return null;
        }
    }

    public void UpdateDashboardImage(GameObject myObject, int currentIndex, paths myPath)
    {
        ViewData view = GetView(myPath);

        if (view == null || view.controller == null)
            return;

        int totalSlices = myPath == paths.Coronal ? 64 : 512;

        float value = currentIndex / (float)(totalSlices - 1);

        value = Mathf.Clamp01(value);

        view.controller.slider.SetValueWithoutNotify(value);
        view.controller.ChangeImage(value);
    }

   

    #endregion
}
