using UnityEditor;
using Unity;
using System.Threading;

public class CreateAssetBundles
{
    [MenuItem("Assets/Build AssetBundles")]
    static void BuildAllAssetBundles()
    {
        BuildPipeline.BuildAssetBundles("Assets/AssetBundles/Win64", BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
        BuildPipeline.BuildAssetBundles("Assets/AssetBundles/Linux64", BuildAssetBundleOptions.None, BuildTarget.StandaloneLinux64);
    }
}