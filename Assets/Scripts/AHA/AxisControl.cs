using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class View_MPI : MonoBehaviour
{
    [Header("References"), Tooltip("Outputs the image visualizer")]
    public paths myPath;
    public RawImage image_output;
    public int current_index = 0;
    public Slider slider;
    public TextMeshProUGUI txt_index;
    Texture2D texture;

    public enum paths
    {
        Axial,
        Coronal,
        Saggital
    }

    private void Start()
    {
        // Setup the slider and text output - Note: all the slices will be in middle
        slider.value = 0.5f;
        ChangeImage(0.5f);
        slider.onValueChanged.AddListener(ChangeImage);
        //// Load textures
        //texture = Resources.Load<Texture2D>(GetCustomPath(myPath) + "slice_0");
        //image_output.texture = texture;
    }

    /// <summary>
    /// Changes the current image from the slider slicer.
    /// </summary>
    /// <param name="value"></param>
    public void ChangeImage(float value)
    {
        // Sets the current number of slices from the path
        int total_slices = myPath == paths.Coronal ? 64 : 512;

        // Normalizes the slices value
        current_index = Mathf.FloorToInt(value * (total_slices - 1));
        MRPI.Instance.UpdateIndex(current_index, myPath);

        // Loads the image from resources
        string path = $"{MRPI.Instance.GetCustomPath(myPath)}slice_{current_index}";
        Texture2D tex = Resources.Load<Texture2D>(path);

        // Changes the MRPI 3D viewer
        MRPI.Instance.ChangeRender(tex, myPath);
        MRPI.Instance.ChangePosition(value, myPath);

        // Output results
        image_output.texture = tex;
        txt_index.text = current_index.ToString();
    }

    
}
