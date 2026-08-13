using System;
using UnityEngine;
using UnityEditor;

public class CreateAssetBundle
{
    [MenuItem("Assets/Create Assets Bundle")]
    private static void BuildAllAssetBundle()
    {
        string output = Application.dataPath + "/Amigo/Bundle";
        AssetBundleBuild[] bundleDefinitions = new AssetBundleBuild[1];

        bundleDefinitions[0].assetBundleName = "Amigo";
        bundleDefinitions[0].assetNames = new string[]
        {
                "Assets/Amigo.prefab",
                "Assets/AmigoButton.prefab",
                "Assets/AmigoRectButton.prefab",
                "Assets/AmigoResult.prefab",
                "Assets/Sprites/",
                "Assets/Resources/fonts & materials/Dangrek-Regular SDF1.asset",
                "Assets/Resources/fonts & materials/Metropolis-ExtraBold SDF.asset",
                "Assets/Resources/fonts & materials/Metropolis-Light SDF.asset",
                "Assets/Resources/fonts & materials/Metropolis-Bold SDF.asset"
        };

        try
        {
            BuildPipeline.BuildAssetBundles(output, bundleDefinitions, BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
            Debug.Log("Bundle finished");
        }
        catch(Exception e)
        {
            Debug.LogWarning(e);
        }
    }
}
