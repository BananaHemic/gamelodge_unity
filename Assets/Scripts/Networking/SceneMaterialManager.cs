using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class SceneMaterialManager : GenericSingleton<SceneMaterialManager>
{
    private struct MaterialPendingServer
    {
        public readonly MaterialInfo MatInfo;
        public readonly SceneObject RequestingSceneObject;
        public readonly int MaterialIndexWithinRenderer;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="matInfo"></param>
        /// <param name="sceneObject"></param>
        /// <param name="materialIndexWithinRenderer">The index of this material in the list of materials for the scene object. NOT the materialIndex</param>
        public MaterialPendingServer(MaterialInfo matInfo, SceneObject sceneObject, int materialIndexWithinRenderer)
        {
            MatInfo = matInfo;
            RequestingSceneObject = sceneObject;
            MaterialIndexWithinRenderer = materialIndexWithinRenderer;
        }
    }

    private readonly Dictionary<ushort, SceneMaterial> _id2SceneMaterial = new Dictionary<ushort, SceneMaterial>();
    private readonly List<SceneMaterial> _sceneMaterials = new List<SceneMaterial>();
    private readonly Dictionary<MaterialInfo, SceneMaterial> _matInfo2SceneMat = new Dictionary<MaterialInfo, SceneMaterial>();
    /// <summary>
    /// DRMaterials that have been received from the network, but do not yet have a corresponding SceneMaterial
    /// </summary>
    private readonly List<DRMaterial> _drMaterialsPendingSceneMaterial = new List<DRMaterial>();
    /// <summary>
    /// The materials that can't yet be fully created, because they are waiting on a DRMaterial
    /// </summary>
    private readonly List<MaterialPendingServer> _materialsPendingServer = new List<MaterialPendingServer>();

    public List<SceneMaterial> GetAllSceneMaterials()
    {
        return _sceneMaterials;
    }
    public List<DRMaterial> GetAllDRMaterialsPendingSceneMaterial()
    {
        return _drMaterialsPendingSceneMaterial;
    }
    public void LocallyClearAllSceneMaterials()
    {
        _sceneMaterials.Clear();
        _id2SceneMaterial.Clear();
        _matInfo2SceneMat.Clear();
        _drMaterialsPendingSceneMaterial.Clear();
        _materialsPendingServer.Clear();
    }
    /// <summary>
    /// Get's the material info for a scene object
    /// And, if we created the object and there is no current network representation
    /// of the material, we create such a network representation
    /// </summary>
    /// <param name="sceneObject"></param>
    /// <param name="didWeCreate"></param>
    /// <param name="onDone"></param>
    public void GetOrCreateSceneMaterialsFor(SceneObject sceneObject, bool didWeCreate)
    {
        // If we created the object, we should first create a network representation of the material
        if (didWeCreate)
        {
            // We first need to get the local materialInfo
            BundleManager.Instance.GetMaterialInfosFromBundle(sceneObject, OnMaterialInfosLoadedCreateDRMaterial);
            return;
        }

        // Get the material infos for the model, then we'll see if we can make a SceneMaterial
        BundleManager.Instance.GetMaterialInfosFromBundle(sceneObject, OnMaterialInfosLoadedGetNetworkData);
    }
    /// <summary>
    /// Called once the material infos for a model have been retrieved.
    /// We then create network representations of the materials as needed
    /// </summary>
    /// <param name="materialInfos"></param>
    private void OnMaterialInfosLoadedCreateDRMaterial(SceneObject sceneObject, MaterialInfo[] materialInfos)
    {
        //Debug.Log("Got material infos for object ID#" + sceneObject.GetObjectID());
        // Now we need to go from MaterialInfo that exists just in the bundle
        // to SceneMaterial which has an actual network representation
        SceneMaterial[] sceneMats = new SceneMaterial[materialInfos.Length];
        for(int i = 0; i < materialInfos.Length; i++)
        {
            MaterialInfo materialInfo = materialInfos[i];
            SceneMaterial sceneMaterial;
            // First see if we've already gotten a material for this mat
            if(_matInfo2SceneMat.TryGetValue(materialInfo, out sceneMaterial))
            {
                sceneMats[i] = sceneMaterial;
                continue;
            }
            // Check pending mats
            DRMaterial drMat = null;
            for(int j = 0; j < _drMaterialsPendingSceneMaterial.Count; j++)
            {
                DRMaterial potentialDRMat = _drMaterialsPendingSceneMaterial[j];
                if(potentialDRMat.MaterialIndex == materialInfo.Index
                    && potentialDRMat.BundleID == materialInfo.BundleID)
                {
                    Debug.Log("Found pending DRMat for this mat");
                    drMat = potentialDRMat;
                    _drMaterialsPendingSceneMaterial.RemoveBySwap(j);
                    break;
                }
            }

            // Otherwise, we need to fully create this SceneMaterial
            if(drMat == null)
            {
                drMat = DarkRiftConnection.Instance.CreateDRMaterial(sceneObject.BundleID, materialInfo.Index, materialInfo.Name);
                if(drMat == null)
                {
                    Debug.LogError("Failed to create material!");
                    continue;
                }
                Debug.Log("Creating material, temp ID #" + drMat.GetID());
            }
            sceneMaterial = new SceneMaterial(materialInfo, drMat);
            _sceneMaterials.Add(sceneMaterial);
            _id2SceneMaterial.Add(drMat.GetID(), sceneMaterial);
            _matInfo2SceneMat.Add(materialInfo, sceneMaterial);
            sceneMats[i] = sceneMaterial;
        }
        sceneObject.OnSceneMaterialsLoaded(sceneMats);
    }
    /// <summary>
    /// Called once the material infos for a model have been retrieved.
    /// If a network representation is available, we use that to create a SceneMaterial
    /// Otherwise, we once the DRMaterial arrives, then we'll make the SceneMaterial
    /// </summary>
    /// <param name="materialInfos"></param>
    private void OnMaterialInfosLoadedGetNetworkData(SceneObject sceneObject, MaterialInfo[] materialInfos)
    {
        // Now we need to go from MaterialInfo that exists just in the bundle
        // to SceneMaterial which has an actual network representation
        SceneMaterial[] sceneMats = new SceneMaterial[materialInfos.Length];
        for(int i = 0; i < materialInfos.Length; i++)
        {
            MaterialInfo materialInfo = materialInfos[i];
            SceneMaterial sceneMaterial;
            // First see if we've already gotten a material for this mat
            if(_matInfo2SceneMat.TryGetValue(materialInfo, out sceneMaterial))
            {
                sceneMats[i] = sceneMaterial;
                continue;
            }

            bool foundFromNetwork = false;
            // Then we see if there's a DRMaterial present that is not yet connected to a SceneMaterial
            // Otherwise, we need to fully create this SceneMaterial
            // Go in reverse order so we can remove while processing
            for(int j = _drMaterialsPendingSceneMaterial.Count - 1; j >= 0; j--)
            {
                DRMaterial potential = _drMaterialsPendingSceneMaterial[j];
                if (potential.MaterialIndex != materialInfo.Index || potential.BundleID != materialInfo.BundleID)
                    continue;
                //Debug.Log("Found DRMaterial that was pending from network");
                // Create a new SceneMaterial for this DRMaterial
                sceneMaterial = new SceneMaterial(materialInfo, potential);
                sceneMats[i] = sceneMaterial;
                _sceneMaterials.Add(sceneMaterial);
                _id2SceneMaterial.Add(potential.GetID(), sceneMaterial);
                _matInfo2SceneMat.Add(materialInfo, sceneMaterial);
                // Remove the DRMaterial from the list of pendings
                _drMaterialsPendingSceneMaterial.RemoveBySwap(j);
                foundFromNetwork = true;
                break;
            }

            // If we don't have a SceneMaterial or a DRMaterial for this MaterialInfo, we put it in a buffer
            // and wait until we receive a DRMaterial
            if (!foundFromNetwork)
            {
                Debug.Log("Waiting for server for mat ID: " + materialInfo.BundleID + " #" + materialInfo.Index);
                MaterialPendingServer materialPendingServer = new MaterialPendingServer(materialInfo, sceneObject, i);
                _materialsPendingServer.Add(materialPendingServer);
            }
        }
        sceneObject.OnSceneMaterialsLoaded(sceneMats);
    }
    public void OnServerAddedMaterial(DRMaterial newMaterial)
    {
        //Debug.Log("Server added mat: " + newMaterial.BundleID + " #" + newMaterial.MaterialIndex + " ID " + newMaterial.GetID());
        // First check if there are MaterialInfo/SceneObjects waiting for this DRMaterial
        //Stack<int> elementsToRemove = new Stack<int>();
        SceneMaterial sceneMaterial = null;

        for(int i = _materialsPendingServer.Count - 1; i >= 0; i--)
        {
            MaterialPendingServer materialPendingServer = _materialsPendingServer[i];
            if (materialPendingServer.MatInfo.Index != newMaterial.MaterialIndex || materialPendingServer.MatInfo.BundleID != newMaterial.BundleID)
                continue;
            // Make a SceneMaterial and call this 
            if (sceneMaterial == null)
                sceneMaterial = new SceneMaterial(materialPendingServer.MatInfo, newMaterial);
            _sceneMaterials.Add(sceneMaterial);
            _id2SceneMaterial.Add(sceneMaterial.GetID(), sceneMaterial);
            _matInfo2SceneMat.Add(materialPendingServer.MatInfo, sceneMaterial);

            materialPendingServer.RequestingSceneObject.OnSceneMaterialLoaded(sceneMaterial, materialPendingServer.MaterialIndexWithinRenderer);
            _materialsPendingServer.RemoveBySwap(i);
        }

        if(sceneMaterial == null)
        {
            //Debug.Log("Recv material from server before local loaded ID:" + newMaterial.BundleID + " #" + newMaterial.MaterialIndex);
            // Otherwise, we queue this DRMaterial until we have a corresponding MaterialInfo
            _drMaterialsPendingSceneMaterial.Add(newMaterial);
        }
    }
    public void OnServerUpdateMaterialColor(ushort materialID, int propertyIndex, Color color, bool didWeInitiate)
    {
        SceneMaterial sceneMaterial;
        if(!_id2SceneMaterial.TryGetValue(materialID, out sceneMaterial)) {
            Debug.LogError("Failed to update color for " + materialID + " not found!");
            return;
        }

        sceneMaterial.SetColor(propertyIndex, color, didWeInitiate, true);
    }
    public void UpdateMaterialID(ushort oldTempID, ushort newObjectID)
    {
        SceneMaterial sceneMaterial;
        if(!_id2SceneMaterial.TryGetValue(oldTempID, out sceneMaterial)) {
            Debug.LogError("Failed to update material objectID for " + oldTempID + " not found!");
            return;
        }
        _id2SceneMaterial.Remove(oldTempID);
        sceneMaterial.SetObjectID(newObjectID);
        _id2SceneMaterial.Add(newObjectID, sceneMaterial);
    }
    public void ReplaceRedundantDRMaterialWithCorrect(ushort oldTempID, ushort correctMaterialID)
    {
        DRMaterial correctMaterial = null;
        for(int i = 0; i < _drMaterialsPendingSceneMaterial.Count; i++)
        {
            DRMaterial potential = _drMaterialsPendingSceneMaterial[i];
            if(potential.GetID() == correctMaterialID)
            {
                correctMaterial = potential;
                _drMaterialsPendingSceneMaterial.RemoveBySwap(i);
                break;
            }
        }

        if(correctMaterial == null)
        {
            Debug.LogWarning("Can't replace redundant material, none found for #" + correctMaterialID);
            return;
        }

        SceneMaterial sceneMaterial;
        if(!_id2SceneMaterial.TryGetValue(oldTempID, out sceneMaterial)) {
            Debug.LogError("Failed to replace redundant material for #" + oldTempID + " SceneMaterial not found!");
            return;
        }
        // Remove the old DRMaterial
        _id2SceneMaterial.Remove(oldTempID);
        sceneMaterial.ReplaceDRMaterial(correctMaterial);
        _id2SceneMaterial.Add(correctMaterialID, sceneMaterial);
    }
    public Coroutine StartCoroutineSceneMaterial(IEnumerator enumerator)
    {
        return StartCoroutine(enumerator);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
            Debug.Log("We currently have " + _sceneMaterials.Count + " materials");
    }
}
