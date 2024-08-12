using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class AnimationTextureBaker : MonoBehaviour
{
    [Header("Path Setting")]
    public string NameFolder = "BakedAnimationTex";
    [Header("Components")]
    public ComputeShader infoTexGen;
    public Shader PlayShader;

    private List<VertInfo> _vertices = new List<VertInfo>();

    private BoundsAnimation _bounds;

   

    [System.Serializable]
    public class BoundsAnimation
    {
        public Vector3 Min;
        public Vector3 Max;

        public BoundsAnimation()
        {
            Min = new Vector3(100, 100, 100);

            Max = new Vector3(-100, -100, -100);
        }

        public void Copy(BoundsAnimation values)
        {
            Min = values.Min;

            Max = values.Max;
        }
    }

    [System.Serializable]
    public struct VertInfo
    {
        public Vector4 position;
        public Vector2 normal;
        public Vector3 tangent;
    }


    private void Start()
    {
        _bounds = new BoundsAnimation();

        var animation = GetComponent<Animation>();

        var skin = GetComponentInChildren<SkinnedMeshRenderer>();

        var vCount = skin.sharedMesh.vertexCount;

        var texWidth = Mathf.NextPowerOfTwo(vCount);

        var mesh = new Mesh();

        foreach (AnimationState state in animation)
        {
            animation.Play(state.name);
            var frames = Mathf.NextPowerOfTwo((int)(state.length / 0.05f));
            var dt = state.length / frames;
            var time = 0f;
            var infoList = new List<VertInfo>();

            var pRt = new RenderTexture(texWidth, frames / 2, 0, RenderTextureFormat.ARGB32);
            pRt.name = string.Format("{0}.{1}.posTex", name, state.name);
            var nRt = new RenderTexture(texWidth, frames / 2, 0, RenderTextureFormat.RG16);
            nRt.name = string.Format("{0}.{1}.normTex", name, state.name);
            var tRt = new RenderTexture(texWidth, frames / 2, 0, RenderTextureFormat.ARGBHalf);
            tRt.name = string.Format("{0}.{1}.tangentTex", name, state.name);


            foreach (var rt in new[] { pRt, nRt, tRt })
            {
                rt.enableRandomWrite = true;
                rt.Create();
                RenderTexture.active = rt;
                GL.Clear(true, true, Color.clear);
            }

            var temp = new VertInfo();

            for (var i = 0; i < frames; i++)
            {
                if (i % 2 != 0)
                {
                    time += dt;
                    continue;
                }

                state.time = time;
                animation.Sample();
                skin.BakeMesh(mesh);

                for (int vertex = 0; vertex < vCount; vertex++)
                {
                    ChangeMinMax(temp.position);

                    temp.position = Combine(mesh.vertices[vertex], mesh.normals[vertex].z);

                    temp.normal = ConvertToVector2(mesh.normals[vertex]);

                    temp.tangent = mesh.tangents[vertex];

                    _vertices.Add(temp);
                }

                time += dt;
            }

            for (int count = 0; count < _vertices.Count; count++)
            {
                infoList.Add(new VertInfo()
                {
                    position = NormalizePosition(_vertices[count].position),
                    normal = _vertices[count].normal,
                    tangent = _vertices[count].tangent
                }
                );
            }

            GenerateAnimationTextures(vCount, frames, infoList, pRt, nRt, tRt);
#if UNITY_EDITOR
            string folderPath = CreateFolder("Assets",NameFolder);

            var subFolder = name;

            var subFolderPath = CreateFolder(folderPath, subFolder);

            Texture2D posTex = RenderTextureToTexture(pRt);
            Texture2D normTex = RenderTextureToTexture(nRt);
            Texture2D tanTex = RenderTextureToTexture(tRt);

            Material mat = InitializeShaderMaterial(skin, state, posTex, normTex);

            var go = new GameObject(name + "." + state.name);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            go.AddComponent<MeshFilter>().sharedMesh = skin.sharedMesh;

            AssetDatabase.CreateAsset(posTex, Path.Combine(subFolderPath, pRt.name + ".asset"));
            AssetDatabase.CreateAsset(normTex, Path.Combine(subFolderPath, nRt.name + ".asset"));
            AssetDatabase.CreateAsset(tanTex, Path.Combine(subFolderPath, tRt.name + ".asset"));
            AssetDatabase.CreateAsset(mat, Path.Combine(subFolderPath, string.Format("{0}.{1}.animTex.asset", name, state.name)));
            PrefabUtility.CreatePrefab(Path.Combine(subFolderPath, go.name + ".prefab").Replace("\\", "/"), go);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
#endif
        }
    }

    private string CreateFolder(string folderPath, string subFolder)
    {
        var subFolderPath = Path.Combine(folderPath, subFolder);
        if (!AssetDatabase.IsValidFolder(subFolderPath))
            AssetDatabase.CreateFolder(folderPath, subFolder);

        return subFolderPath;
    }

    private Texture2D RenderTextureToTexture(RenderTexture renderTexture)
    {
        Texture2D texture = RenderTextureToTexture2D.Convert(renderTexture);
        Graphics.CopyTexture(renderTexture, texture);
        return texture;
    }

    private Material InitializeShaderMaterial(SkinnedMeshRenderer skin, AnimationState state, Texture2D posTex, Texture2D normTex)
    {
        var mat = new Material(PlayShader);
        mat.SetVector("_Min", _bounds.Min);
        mat.SetVector("_Max", _bounds.Max);
        mat.SetTexture("_MainTex", skin.sharedMaterial.mainTexture);
        mat.SetTexture("_PosTex", posTex);
        mat.SetTexture("_NmlTex", normTex);
        mat.SetFloat("_Length", state.length);
        mat.SetFloat("_Scale", 0.3f);
        mat.enableInstancing = true;
        return mat;
    }

    private void GenerateAnimationTextures(int vCount, int frames, List<VertInfo> infoList, RenderTexture pRt, RenderTexture nRt, RenderTexture tRt)
    {
        var buffer = new ComputeBuffer(infoList.Count, System.Runtime.InteropServices.Marshal.SizeOf(typeof(VertInfo)));
        buffer.SetData(infoList.ToArray());

        var kernel = infoTexGen.FindKernel("CSMain");
        uint x, y, z;
        infoTexGen.GetKernelThreadGroupSizes(kernel, out x, out y, out z);
        infoTexGen.SetInt("VertCount", vCount);
        infoTexGen.SetBuffer(kernel, "Info", buffer);
        infoTexGen.SetTexture(kernel, "OutPosition", pRt);
        infoTexGen.SetTexture(kernel, "OutNormal", nRt);
        infoTexGen.SetTexture(kernel, "OutTangent", tRt);
        infoTexGen.Dispatch(kernel, vCount / (int)x + 1, frames / (int)y + 1, 1);

        buffer.Release();
    }

    private void ChangeMinMax(Vector3 position)
    {
        if (position.x > _bounds.Max.x) _bounds.Max.x = position.x;
        if (position.y > _bounds.Max.y) _bounds.Max.y = position.y;
        if (position.z > _bounds.Max.z) _bounds.Max.z = position.z;

        if (position.x < _bounds.Min.x) _bounds.Min.x = position.x;
        if (position.y < _bounds.Min.y) _bounds.Min.y = position.y;
        if (position.z < _bounds.Min.z) _bounds.Min.z = position.z;
    }


    private Vector4 NormalizePosition(Vector4 position)
    {
        return new Vector4(
            NormalizeValue(position.x, _bounds.Min.x, _bounds.Max.x),
            NormalizeValue(position.y, _bounds.Min.y, _bounds.Max.y),
            NormalizeValue(position.z, _bounds.Min.z, _bounds.Max.z),
            position.w);
    }

    public float NormalizeValue(float value, float min, float max)
    {
        return (value - min) / (max - min);
    }

    private Vector4 Combine(Vector3 vector, float value) =>
      new Vector4(vector.x, vector.y, vector.z, value);

    private Vector2 ConvertToVector2(Vector3 vector) =>
        new Vector2(vector.x, vector.y);

}
