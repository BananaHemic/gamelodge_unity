using Miniscript;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LineRendererBehavior : BaseBehavior
{
    public Material LineMaterial;
    private LineRenderer _addedLineRenderer;
    private readonly SerializedBundleItemReference _lineMaterialReference = new SerializedBundleItemReference(nameof(LineMaterial));
    private static readonly List<ExposedFunction> _userFunctions = new List<ExposedFunction>();
    private static readonly ValString WidthValStr = ValString.Create("width", false);
    const int LineMaterialKey = 0;

    private bool _waitingOnMaterialLoad = false;
    private int _currentlyLoadingID;
    private string _lineMaterialBundleID;
    private ushort _lineMaterialBundleIndex;
    protected override void ChildInit()
    {
        _addedLineRenderer = _sceneObject.gameObject.AddComponent<LineRenderer>();
        base.AddBundleItemReference(_lineMaterialReference);
        RefreshProperties();
    }
    public override void UpdateParamsFromSerializedObject()
    {
        // Material
        byte[] materialArray;
        if (_serializedBehavior.TryReadProperty(LineMaterialKey, out materialArray, out int _))
            _lineMaterialReference.UpdateFrom(materialArray);
    }
    public override void WriteCurrentValuesToSerializedBehavior()
    {
        // Material
        _serializedBehavior.LocallySetData(LineMaterialKey, _lineMaterialReference.GetSerialized());
    }
    public override void RefreshProperties()
    {
        if (string.IsNullOrEmpty(_lineMaterialReference.BundleID))
        {
            LineMaterial = null;
            _addedLineRenderer.material = null;
        }
        else
        {
            if(_lineMaterialBundleID != _lineMaterialReference.BundleID
                || _lineMaterialBundleIndex != _lineMaterialReference.BundleIndex)
            {
                _lineMaterialBundleID = _lineMaterialReference.BundleID;
                _lineMaterialBundleIndex = _lineMaterialReference.BundleIndex;

                int loadID = ++_currentlyLoadingID;
                _waitingOnMaterialLoad = true;
                BundleManager.Instance.LoadItemFromBundle<Material>(_lineMaterialReference.BundleID, _lineMaterialReference.BundleIndex, loadID, OnMaterialLoaded);
            }
        }
    }
    void OnMaterialLoaded(int loadID, Material mat)
    {
        //Debug.Log("Material loaded");
        if(_currentlyLoadingID != loadID)
        {
            Debug.LogWarning("Dropping audio load, was load ID #" + loadID + " expected " + _currentlyLoadingID);
            return;
        }
        if (!_waitingOnMaterialLoad)
            Debug.LogWarning("Material loaded, but loading flag not set");
        _waitingOnMaterialLoad = false;

        if (LineMaterial == mat)
        {
            //Debug.Log("Already have that clip selected");
            return;
        }

        LineMaterial = mat;
        _addedLineRenderer.material = mat;
    }
    public override void Destroy()
    {
        if(_addedLineRenderer != null)
        {
            Destroy(_addedLineRenderer);
            _addedLineRenderer = null;
        }
    }
    public override bool DoesRequireCollider() { return false; }
    public override bool DoesRequirePosRotScaleSyncing() { return false; }
    public override bool DoesRequireRigidbody() { return false; }
    public override List<ExposedEvent> GetEvents()
    {
        return null;
    }
    public override List<ExposedFunction> GetFunctions()
    {
        return _userFunctions;
    }
    public override List<ExposedVariable> GetVariables()
    {
        return null;
    }
    public static void LoadIntrinsics()
    {
        Intrinsic intrinsic;
        intrinsic = Intrinsic.Create("SetLinePositions");
        intrinsic.AddParam(ValString.positionStr.value);
        _userFunctions.Add(new ExposedFunction(intrinsic, "Sets the positions used for the line", null));
        intrinsic.code = (context, partialResult) => {
            ValList posVal = context.GetVar(ValString.positionStr) as ValList;
            if(posVal == null)
            {
                UserScriptManager.LogToCode(context, "No positions for SetPositions", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetPositions call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            LineRendererBehavior lineBehavior = sceneObject.GetBehaviorByType<LineRendererBehavior>();
            if(lineBehavior == null)
            {
                UserScriptManager.LogToCode(context, "No LineRenderer in SetPositions call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            lineBehavior._addedLineRenderer.positionCount = posVal.Count;
            for(int i = 0; i < posVal.Count; i++)
            {
                ValVector3 valVec = posVal[i] as ValVector3;
                if(valVec == null)
                {
                    UserScriptManager.LogToCode(context, "LineRenderer SetPositions requires ValVector3s!", UserScriptManager.CodeLogType.Error);
                    return Intrinsic.Result.Null;
                }

                lineBehavior._addedLineRenderer.SetPosition(i, valVec.Vector3);
            }

            return Intrinsic.Result.True;
		};

        intrinsic = Intrinsic.Create("SetLineWidth");
        intrinsic.AddParam(WidthValStr.value);
        _userFunctions.Add(new ExposedFunction(intrinsic, "Sets the width of the line", null));
        intrinsic.code = (context, partialResult) => {
            ValNumber widthVal = context.GetVar(WidthValStr) as ValNumber;
            if(widthVal == null)
            {
                UserScriptManager.LogToCode(context, "No width for SetWidth", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in SetWidth call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            LineRendererBehavior lineBehavior = sceneObject.GetBehaviorByType<LineRendererBehavior>();
            if(lineBehavior == null)
            {
                UserScriptManager.LogToCode(context, "No LineRenderer in SetWidth call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            lineBehavior._addedLineRenderer.startWidth = (float)widthVal.value;
            lineBehavior._addedLineRenderer.endWidth = (float)widthVal.value;

            return Intrinsic.Result.True;
		};
    }
}
