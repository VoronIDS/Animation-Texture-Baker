using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AnimationTextureBaker))]
public class AnimationTextureBakerEditor : Editor
{
    private const string COMPUTE_SHADER_TEXTURE = "TextureWritter";

    private const string SHADER_TEXTURE = "TextureRead";

    private AnimationTextureBaker _script;
    private void OnEnable()
    {
        _script = (AnimationTextureBaker)target;

        SearchComponents();

        EditorUtility.SetDirty(_script);
    }

    private void SearchComponents()
    {
        _script.infoTexGen = SearchComputeShader(COMPUTE_SHADER_TEXTURE);

        _script.PlayShader = SearchShader(SHADER_TEXTURE);
    }

    public static ComputeShader SearchComputeShader(string name)
    {
        string[] guids = AssetDatabase.FindAssets(name + " t:ComputeShader");

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);

        return AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
    }

    public static Shader SearchShader(string name)
    {
        string[] guids = AssetDatabase.FindAssets(name + " t:Shader");

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);

        return AssetDatabase.LoadAssetAtPath<Shader>(path);
    }


}
