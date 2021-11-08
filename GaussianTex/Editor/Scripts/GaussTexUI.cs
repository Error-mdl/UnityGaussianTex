using UnityEngine;
using UnityEditor;
using GaussianTexture;
using System.IO;
public class GaussTexUI : EditorWindow
{

  public ComputeShader TexturePreprocessor;
  public ComputeShader BitonicSort;
  public ComputeShader CumulativeDistribution;

  public Texture2D InputTex;
  public Material TestMaterial;
  public ColorspaceObj colorSpace;
  public bool EigenColors = true;
  public bool CompressionCorrection = false;
  public FileType fileType = FileType.png;


  public string axis0Property = "_CX";
  public string axis1Property = "_CY";
  public string axis2Property = "_CZ";
  public string centerProperty = "_CsCenter";

  private int LUTWidth = 16;
  private int LUTWidthPow2 = 4;
  private int LUTHeight = 16;
  private int LUTHeightPow2 = 4;

  private const int WINDOW_BORDER = 4;
  private const int WINDOW_MIN_W = 300;
  private const int WINDOW_MIN_H = 128;
  private const int SCROLL_WIDTH = 16;

  private bool GaussFoldout = true;
  private bool CSFoldout = false;
  private bool MatAssignFoldout = true;
  private bool MaterialPropertyFoldout = false;

  private Vector2 ScrollPos;
  private GUIStyle titleStyle;
  private GUIContent GaussLabel;
  private GUIContent ColorspaceLabel;


  private TexToGaussian ImageConverter;

  [MenuItem("Window/Convert Texture To Gaussian")]
  public static void ShowWindow()
  {
    GaussTexUI GaussUI = GetWindow<GaussTexUI>(false, "  Tex2Gaussian", true);
    GaussUI.minSize = new Vector2(WINDOW_MIN_W, WINDOW_MIN_H);
  }


  public void Awake()
  {
    //titleStyle = new GUIStyle(EditorStyles.foldout);
    GaussLabel = new GUIContent();
    GaussLabel.text = "    Convert Texture to Gaussian";
    GaussLabel.image = EditorGUIUtility.ObjectContent(null, typeof(RenderTexture)).image;
    ColorspaceLabel = new GUIContent();
    ColorspaceLabel.text = "    Copy Colorspace Settings To Material";
    ColorspaceLabel.image = EditorGUIUtility.ObjectContent(null, typeof(Transform)).image;

    ImageConverter = ScriptableObject.CreateInstance<TexToGaussian>();
    int foundComputeShaders = ImageConverter.AssignComputeShaders();
    if (foundComputeShaders == 0)
    {
      TexturePreprocessor = ImageConverter.TexturePreprocessor;
      BitonicSort = ImageConverter.BitonicSort;
      CumulativeDistribution = ImageConverter.CumulativeDistribution;
    }
    else
    {
      Debug.LogWarning("Could not find compute shader dependencies");
    }
    DestroyImmediate(ImageConverter);
  }

  void OnGUI()
  {
    titleStyle = GUI.skin.GetStyle("IN Title");
    ScrollPos = GUILayout.BeginScrollView(ScrollPos, false, true, GUIStyle.none, GUI.skin.verticalScrollbar);

    GaussFoldout = DrawTitle(GaussLabel, GaussFoldout);

    if (GaussFoldout)
    {
      EditorGUI.indentLevel++;
      InputTex = EditorGUILayout.ObjectField("Texture to Convert", InputTex, typeof(Texture2D), false, GUILayout.Width(EditorGUIUtility.currentViewWidth - SCROLL_WIDTH)) as Texture2D;

      fileType = (FileType)EditorGUILayout.EnumPopup("Save as: ", fileType, GUILayout.Width(EditorGUIUtility.currentViewWidth - SCROLL_WIDTH));
      if (fileType == FileType.jpg)
      {
        TextureImporter texImp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(InputTex));
        if (texImp?.textureType == TextureImporterType.NormalMap)
        {
          EditorGUILayout.HelpBox("Jpg format cannot store normal maps as they use an alpha channel", MessageType.Error, true);
        }
        else
        {
          EditorGUILayout.HelpBox("Jpg format cannot store an alpha channel, only use this for textures with no alpha", MessageType.Warning, true);
        }
      }

      EditorGUILayout.BeginHorizontal(GUILayout.Width(EditorGUIUtility.currentViewWidth - SCROLL_WIDTH));
      EditorGUILayout.LabelField(new GUIContent("Decorrolate Colorspace (Albedo, Specular only)","Prevents colors from appearing that aren't in the input image, but may slightly" +
        " reduce color accuracy. Only use with images that contain color information, not for images which contain independent information in each channel like metallic-smoothness maps"),
        GUILayout.Width(300));
      GUILayout.FlexibleSpace();
      EigenColors = EditorGUILayout.Toggle(EigenColors, GUILayout.Width(64 + SCROLL_WIDTH));
      EditorGUILayout.EndHorizontal();

      using (new EditorGUI.DisabledScope(EigenColors == false))
      {
        EditorGUILayout.BeginHorizontal(GUILayout.Width(EditorGUIUtility.currentViewWidth - SCROLL_WIDTH));
        EditorGUILayout.LabelField(new GUIContent("Compression Correction", "If Decorrolate Colorspace is enabled, attempt to correct for DXT compression." +
        "Seems to cause bad artifacts in many cases"),
        GUILayout.Width(300));
        GUILayout.FlexibleSpace();
        CompressionCorrection = EditorGUILayout.Toggle(CompressionCorrection, GUILayout.Width(64 + SCROLL_WIDTH));
        EditorGUILayout.EndHorizontal();
      }

      EditorGUILayout.BeginHorizontal(GUILayout.Width(EditorGUIUtility.currentViewWidth - SCROLL_WIDTH));
      EditorGUILayout.LabelField("Lookup Table Dimensions:");
      GUILayout.FlexibleSpace();
      GUIContent LUTLabel = new GUIContent();
      LUTLabel.text = string.Format("{0}x{1}, {2} Values", LUTWidth, LUTHeight, LUTWidth * LUTHeight);
      EditorGUILayout.LabelField(LUTLabel, EditorStyles.boldLabel, GUILayout.Width(EditorStyles.label.CalcSize(LUTLabel).x + 2*SCROLL_WIDTH));
      EditorGUILayout.EndHorizontal();

      EditorGUI.indentLevel++;
      LUTWidthPow2 = DrawSliderSteps("Width Power of 2", LUTWidthPow2, 1, 5, ref LUTWidth);
      LUTHeightPow2 = DrawSliderSteps("Height Power of 2", LUTHeightPow2, 1, 5, ref LUTHeight);
      EditorGUI.indentLevel--;

      CSFoldout = EditorGUILayout.Foldout(CSFoldout, "Compute Shaders");
      if (CSFoldout)
      {
        EditorGUI.indentLevel++;
        TexturePreprocessor = EditorGUILayout.ObjectField("Texture Preprocessor", TexturePreprocessor, typeof(ComputeShader), false,
          GUILayout.Width(EditorGUIUtility.currentViewWidth - SCROLL_WIDTH)) as ComputeShader;
        BitonicSort = EditorGUILayout.ObjectField("Bitonic Merge Sort", BitonicSort, typeof(ComputeShader), false,
          GUILayout.Width(EditorGUIUtility.currentViewWidth - SCROLL_WIDTH)) as ComputeShader;
        CumulativeDistribution = EditorGUILayout.ObjectField("Gaussian Conversion", CumulativeDistribution, typeof(ComputeShader), false,
          GUILayout.Width(EditorGUIUtility.currentViewWidth - SCROLL_WIDTH)) as ComputeShader;
        EditorGUI.indentLevel--;
      }

      if (GUILayout.Button("Create Gaussian Texture and Lookup Table", GUILayout.Width(EditorGUIUtility.currentViewWidth - SCROLL_WIDTH), GUILayout.Height(30)))
      {
        ImageConverter = ScriptableObject.CreateInstance<TexToGaussian>();
        if (TexturePreprocessor != null && BitonicSort != null && CumulativeDistribution != null)
        {
          ImageConverter.TexturePreprocessor = TexturePreprocessor;
          ImageConverter.BitonicSort = BitonicSort;
          ImageConverter.CumulativeDistribution = CumulativeDistribution;
          ImageConverter.CreateGaussianTexture(InputTex, LUTWidthPow2, LUTHeightPow2, CompressionCorrection, EigenColors, fileType);

          string inputNameAndPath = AssetDatabase.GetAssetPath(InputTex);
          string inputName = Path.GetFileNameWithoutExtension(inputNameAndPath);
          string inputPath = Path.GetDirectoryName(inputNameAndPath);

          string outputImagePath = Path.Combine(inputPath, inputName + "_gauss" + TexToGaussian.FileTypeToString(fileType));
          string outputLUTPath = Path.Combine(inputPath, inputName + "_lut.asset");
          string outputColorspacePath = Path.Combine(inputPath, inputName + "_Colorspace.asset");
          EditorGUIUtility.PingObject(AssetDatabase.LoadMainAssetAtPath(outputImagePath));
          EditorUtility.DisplayDialog("Textures Successfully Created", 
            string.Format("Created:\n{0}\n{1}\n{2}", outputImagePath, outputLUTPath, outputColorspacePath), "Ok");
        }
        else
        {
          EditorUtility.DisplayDialog("Compute Shaders Not Found", "One or more compute shader dependencies are missing. Make sure all 3 shaders are assigned before" +
            "attempting to run this script!", "Ok");
        }
        DestroyImmediate(ImageConverter);
      }
      EditorGUILayout.Space(10);
      EditorGUI.indentLevel--;
    }

    MatAssignFoldout = DrawTitle(ColorspaceLabel, MatAssignFoldout);
    
    if (MatAssignFoldout)
    {
      EditorGUI.indentLevel++;
      TestMaterial = EditorGUILayout.ObjectField(TestMaterial, typeof(Material), false, GUILayout.Width(EditorGUIUtility.currentViewWidth - SCROLL_WIDTH)) as Material;
      colorSpace = EditorGUILayout.ObjectField(colorSpace, typeof(ColorspaceObj), false, GUILayout.Width(EditorGUIUtility.currentViewWidth - SCROLL_WIDTH)) as ColorspaceObj;
      MaterialPropertyFoldout = EditorGUILayout.Foldout(MaterialPropertyFoldout, "Material Property Names");
      if (MaterialPropertyFoldout)
      {
        EditorGUI.indentLevel++;
        axis0Property = EditorGUILayout.TextField("Axis 0", axis0Property, GUILayout.Width(EditorGUIUtility.currentViewWidth - SCROLL_WIDTH));
        axis1Property = EditorGUILayout.TextField("Axis 1", axis1Property, GUILayout.Width(EditorGUIUtility.currentViewWidth - SCROLL_WIDTH));
        axis2Property = EditorGUILayout.TextField("Axis 2", axis2Property, GUILayout.Width(EditorGUIUtility.currentViewWidth - SCROLL_WIDTH));
        centerProperty = EditorGUILayout.TextField("Center", centerProperty, GUILayout.Width(EditorGUIUtility.currentViewWidth - SCROLL_WIDTH));
        EditorGUI.indentLevel--;
      }

      if (GUILayout.Button("Assign Colorspace values", GUILayout.Width(EditorGUIUtility.currentViewWidth - SCROLL_WIDTH), GUILayout.Height(30)))
      {
        TexToGaussian.CopyColorspaceToMat(TestMaterial, colorSpace, axis0Property, axis1Property, axis2Property, centerProperty);
      }
      EditorGUI.indentLevel--;
    }
    EditorGUILayout.EndScrollView();
  
  }

  private void OnDestroy()
  {
    DestroyImmediate(ImageConverter);
  }

  private bool DrawTitle(GUIContent content, bool toggleName)
  {
    EditorGUILayout.BeginVertical();
    EditorGUILayout.LabelField(content, titleStyle, GUILayout.Width(EditorGUIUtility.currentViewWidth), GUILayout.Height(titleStyle.fixedHeight));
    EditorGUILayout.Space(-6);
    GUILayout.Box("",  GUILayout.Width(EditorGUIUtility.currentViewWidth), GUILayout.Height(1));
    EditorGUILayout.Space(-28);
    EditorGUILayout.BeginHorizontal();
    EditorGUILayout.Space(6);
    toggleName = EditorGUILayout.Foldout(toggleName, "");
    GUILayout.FlexibleSpace();
    EditorGUILayout.EndHorizontal();
    EditorGUILayout.Space(6);
    EditorGUILayout.EndVertical();
    return (toggleName);
  }
  private int DrawSliderSteps(string title, int value, int min, int max, ref int displayValue)
  {
    EditorGUILayout.BeginHorizontal(GUILayout.Width(EditorGUIUtility.currentViewWidth - SCROLL_WIDTH));
    EditorGUILayout.LabelField(title);
    GUILayout.FlexibleSpace();
    value = EditorGUILayout.IntSlider(value, min, max, GUILayout.Width(EditorGUIUtility.currentViewWidth / 2));

    EditorGUILayout.EndHorizontal();

    displayValue = 1 << value;

    return value;
  }
}
