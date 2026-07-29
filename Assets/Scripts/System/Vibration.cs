using UnityEngine;
using System.Collections;

public class ControllerVibration : MonoBehaviour
{
    public void Vibrate(float duration, OVRInput.Controller controller)
    {
        StartCoroutine(VibrateCoroutine(duration, controller));
    }

    private IEnumerator VibrateCoroutine(float duration, OVRInput.Controller controller)
    {
        OVRInput.SetControllerVibration(0.1f, 0.1f, controller);

        yield return new WaitForSeconds(duration);

        OVRInput.SetControllerVibration(0f, 0f, controller);
    }

    public void Vibrate(float duration = 2f)
    {
        StartCoroutine(VibrateCoroutine2(duration));
    }

    private IEnumerator VibrateCoroutine2(float duration)
    {
        OVRInput.SetControllerVibration(0.1f, 0.1f,OVRInput.Controller.Active);

        yield return new WaitForSeconds(duration);

        OVRInput.SetControllerVibration(0f, 0f, OVRInput.Controller.Active);
    }
}