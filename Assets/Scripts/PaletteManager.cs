using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class PaletteManager : MonoBehaviour
{
    private static PaletteManager _instance;
    public static PaletteManager Instance => _instance;



    [Header("Configuration")]
    [Tooltip("The 4 colors used in the original sprite assets. MUST match the sprite pixels EXACTLY.")]
    [SerializeField] private Color[] sourcePalette = new Color[4];

    [Tooltip("Maximum allowed difference for color matching. Lower = Stricter.")]
    [Range(0f, 1f)]
    [SerializeField] private float colorSelectionThreshold = 0.1f;

    [Tooltip("List of available palettes to swap to.")]
    [SerializeField] private List<PaletteDefinition> palettes = new List<PaletteDefinition>();

    [SerializeField] private int currentPaletteIndex = 0;

    [System.Serializable]
    public class PaletteDefinition
    {
        public string Name;
        public Color[] Colors = new Color[4];
    }

    private void Awake()
    {
        if (Application.isPlaying)
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
    }

    private void Start()
    {
        if (sourcePalette == null || sourcePalette.Length < 4)
        {
            // Default GameBoy-ish source colors (Example defaults)
            sourcePalette = new Color[] {
                new Color(0.06f, 0.22f, 0.06f, 1f), // Darkest
                new Color(0.19f, 0.38f, 0.19f, 1f),
                new Color(0.55f, 0.67f, 0.06f, 1f),
                new Color(0.61f, 0.73f, 0.06f, 1f)  // Lightest
            };
        }

        // Apply initial palette
        UpdateShaderGlobals();
    }

    private void OnValidate()
    {
        UpdateShaderGlobals();
    }

    private void UpdateShaderGlobals()
    {
        if (palettes != null && palettes.Count > 0)
        {
            // Clamp index safely
            int safeIndex = Mathf.Clamp(currentPaletteIndex, 0, palettes.Count - 1);
            ApplyPalette(palettes[safeIndex]);
        }
        else
        {
             // If no palette key is set, maybe just apply source colors mapping to themselves or do nothing?
             // But for safety, let's at least update Source Colors globally so shader works
             ApplySourceColorsOnly();
        }
    }

    public void SetPalette(int index)
    {
        if (palettes == null || palettes.Count == 0) return;

        currentPaletteIndex = Mathf.Clamp(index, 0, palettes.Count - 1);
        ApplyPalette(palettes[currentPaletteIndex]);
    }

    public void SetPalette(PaletteDefinition palette)
    {
        ApplyPalette(palette);
    }

    private void ApplyPalette(PaletteDefinition palette)
    {
        if (palette.Colors.Length < 4) return;

        // Set Source Colors
        ApplySourceColorsOnly();

        // Set Target Colors (Convert to Linear)
        Shader.SetGlobalColor("_TargetColor0", palette.Colors[0].linear);
        Shader.SetGlobalColor("_TargetColor1", palette.Colors[1].linear);
        Shader.SetGlobalColor("_TargetColor2", palette.Colors[2].linear);
        Shader.SetGlobalColor("_TargetColor3", palette.Colors[3].linear);
    }

    private void ApplySourceColorsOnly()
    {
        Shader.SetGlobalFloat("_ColorThreshold", colorSelectionThreshold);

        if (sourcePalette == null || sourcePalette.Length < 4) return;

        // Convert to Linear to match the Scene Color in Linear Project Settings
        Shader.SetGlobalColor("_SourceColor0", sourcePalette[0].linear);
        Shader.SetGlobalColor("_SourceColor1", sourcePalette[1].linear);
        Shader.SetGlobalColor("_SourceColor2", sourcePalette[2].linear);
        Shader.SetGlobalColor("_SourceColor3", sourcePalette[3].linear);
    }

    // Debug method to cycle palettes
    public void NextPalette()
    {
        int next = (currentPaletteIndex + 1) % palettes.Count;
        SetPalette(next);
    }
}
