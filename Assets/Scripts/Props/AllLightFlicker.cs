
using UnityEngine;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class RandomLightFlicker : MonoBehaviour
{
    [Header("Tempo LIGADO")]
    [SerializeField] private float minOnTime = 0.3f;
    [SerializeField] private float maxOnTime = 1f;

    [Header("Tempo DESLIGADO")]
    [SerializeField] private float minOffTime = 0.1f;
    [SerializeField] private float maxOffTime = 0.3f;

    private Light2D[] lights;

    private void Start()
    {
        // Encontra todas as Light2D dos filhos
        lights = GetComponentsInChildren<Light2D>(true);

        // Começa com todas as luzes desligadas
        SetLights(false);

        StartCoroutine(FlickerLights());
    }

    private IEnumerator FlickerLights()
    {
        while (true)
        {
            // Liga todas as luzes
            SetLights(true);

            // Sorteia o tempo que ficarão ligadas
            float onTime = Random.Range(minOnTime, maxOnTime);
            yield return new WaitForSeconds(onTime);

            // Desliga todas as luzes
            SetLights(false);

            // Sorteia o tempo que ficarão desligadas
            float offTime = Random.Range(minOffTime, maxOffTime);
            yield return new WaitForSeconds(offTime);
        }
    }

    private void SetLights(bool state)
    {
        foreach (Light2D light in lights)
        {
            light.enabled = state;
        }
    }
}
