using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Сцена Assets/Scenes/Dev/BurnFxPreview.unity (в сборку не входит): поджечь цепочку, потушить водой, включить ветер,
/// крутить параметры BurnFxSettings ползунками. Ползунки меняют копию настроек (BurnFxSettings.Override), ассет не трогают.
/// </summary>
public class BurnFxPreview : MonoBehaviour
{
    [SerializeField] private MatchLogic[] chain;
    [SerializeField] private MatchLogic waterTarget;
    [SerializeField] private GameObject fan;
    [SerializeField] private TMP_Text stats;
    [SerializeField] private Slider[] sliders;
    [SerializeField] private TMP_Text[] sliderLabels;
    [SerializeField] private TMP_Text qualityLabel;

    private static readonly string[] Names = { "Мерцание", "Частота", "Вспышка", "Фронт", "Дым к концу", "Искры при поджоге", "Огней со светом" };
    private BurnFxSettings settings;

    private void Awake()
    {
        settings = Instantiate(BurnFxSettings.Current);
        BurnFxSettings.Override = settings;
        float[] values = { settings.flickerScale, settings.flickerSpeed, settings.flareScale, settings.emberSize, settings.smokeGrowth, settings.sparksOnSpread, settings.maxLights };
        Vector2[] ranges = { new Vector2(0f, 0.5f), new Vector2(1f, 20f), new Vector2(1f, 3f), new Vector2(0f, 1f), new Vector2(1f, 5f), new Vector2(0f, 30f), new Vector2(0f, 16f) };
        for (int i = 0; i < sliders.Length && i < values.Length; i++)
        {
            int k = i;
            sliders[i].minValue = ranges[i].x;
            sliders[i].maxValue = ranges[i].y;
            sliders[i].wholeNumbers = i >= 5;
            sliders[i].value = values[i];
            sliders[i].onValueChanged.AddListener(v => Set(k, v));
            Set(i, values[i]);
        }
        if (fan != null) fan.SetActive(false);
        RefreshQuality();
    }

    private void OnDestroy()
    {
        if (BurnFxSettings.Override == settings) BurnFxSettings.Override = null;
    }

    private void Set(int i, float v)
    {
        switch (i)
        {
            case 0: settings.flickerScale = v; break;
            case 1: settings.flickerSpeed = v; break;
            case 2: settings.flareScale = v; break;
            case 3: settings.emberSize = v; break;
            case 4: settings.smokeGrowth = v; break;
            case 5: settings.sparksOnSpread = Mathf.RoundToInt(v); break;
            case 6: settings.maxLights = Mathf.RoundToInt(v); break;
        }
        if (i < sliderLabels.Length) sliderLabels[i].text = $"{Names[i]}: {v:0.##}";
    }

    public void Ignite()
    {
        if (chain.Length > 0 && chain[0] != null) chain[0].Ignite();
        if (waterTarget != null) waterTarget.Ignite();
    }

    public void Water()
    {
        if (waterTarget != null) waterTarget.Wet(1f);
    }

    public void ToggleWind()
    {
        if (fan != null) fan.SetActive(!fan.activeSelf);
    }

    public void ToggleQuality()
    {
        FxQuality.High = !FxQuality.High;
        RefreshQuality();
    }

    public void Restart()
    {
#if UNITY_EDITOR
        // Сцена не в Build Settings — в редакторе перезагружаем по пути
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(SceneManager.GetActiveScene().path, new LoadSceneParameters(LoadSceneMode.Single));
#else
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
#endif
    }

    private void RefreshQuality()
    {
        if (qualityLabel != null) qualityLabel.text = FxQuality.High ? "Качество: высокое" : "Качество: низкое";
    }

    private void Update()
    {
        if (stats == null) return;
        int particles = 0, systems = 0;
        foreach (ParticleSystem ps in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
            if (ps.isPlaying) { systems++; particles += ps.particleCount; }
        stats.text = $"FPS {1f / Mathf.Max(0.001f, Time.smoothDeltaTime):0}  частиц {particles}  систем {systems}  свет {FireLight.ActiveCount}";
    }
}
