using Miniscript;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthBehavior : BaseBehavior
{
    private static readonly List<ExposedEvent> _userEvents = new List<ExposedEvent>(2);
    private static readonly List<ExposedFunction> _userFunctions = new List<ExposedFunction>(2);
    private static bool _hasLoadedIntrinsics = false;
    public static readonly ValString OnKilledEventName = ValString.Create("OnKilled", false);
    public static readonly ValString OnDamageEventName = ValString.Create("OnDamageTaken", false);
    public static readonly ValString AmountValName = ValString.Create("amount", false);

    const int HealthKey = 1;
    public int Health = 100;

    protected override void ChildInit()
    {
    }
    public override void Destroy()
    {
        if (Orchestrator.Instance.IsAppClosing)
            return;
        Destroy(this);
    }
    public override void WriteCurrentValuesToSerializedBehavior()
    {
        //TODO use a zero-allocation int -> array function
        // Grab Type
        _serializedBehavior.LocallySetData(HealthKey, BitConverter.GetBytes(Health));
    }
    public override void UpdateParamsFromSerializedObject()
    {
        // Health
        byte[] healthArray;
        int prevHealth = Health;
        if (_serializedBehavior.TryReadProperty(HealthKey, out healthArray, out int _))
        {
            Health = BitConverter.ToInt32(healthArray, 0);
            // The health falling below 0 here will cause damage and possibly kill
            if(Health < prevHealth)
            {
                if(_sceneObject != null)
                {
                    _sceneObject.InvokeEventOnBehaviors(OnDamageEventName);
                    if (Health < 0 && prevHealth > 0)
                        OnKilled(false);
                }
            }
        }
    }
    public override List<ExposedEvent> GetEvents()
    {
        if(_userEvents.Count == 0)
        {
            _userEvents.Add(new ExposedEvent(OnKilledEventName, "Runs when the object is out of health", null));
            _userEvents.Add(new ExposedEvent(OnDamageEventName, "Runs when the object has taken damage", null));
            return _userEvents;
        }
        return _userEvents;
    }
    public static void LoadIntrinsics()
    {
        if (_hasLoadedIntrinsics)
            return;
        _hasLoadedIntrinsics = true;

        Intrinsic intrinsic;
        intrinsic = Intrinsic.Create("GetHealth");
        _userFunctions.Add(new ExposedFunction(intrinsic, "Retrieves the current health", "health"));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in GetHealth call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            HealthBehavior healthBehavior = sceneObject.GetBehaviorByType<HealthBehavior>();
            if(healthBehavior == null)
            {
                UserScriptManager.LogToCode(context, "No HealthBehavior in GetHealth call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            return new Intrinsic.Result(healthBehavior.Health);
		};

        intrinsic = Intrinsic.Create("Damage");
        intrinsic.AddParam(AmountValName.value);
        _userFunctions.Add(new ExposedFunction(intrinsic, "Reduces the health by some amount", null));
        intrinsic.code = (context, partialResult) => {
            SceneObject sceneObject = UserScriptManager.GetSceneObjectFromContext(context);
            if(sceneObject == null)
            {
                UserScriptManager.LogToCode(context, "No scene object in Damage call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            //Debug.Log("Damage taken #" + sceneObject.GetID());

            ValNumber num = context.GetVar(AmountValName) as ValNumber;
            if(num == null)
            {
                UserScriptManager.LogToCode(context, "No damage amount in Damage call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            HealthBehavior healthBehavior = sceneObject.GetBehaviorByType<HealthBehavior>();
            if(healthBehavior == null)
            {
                UserScriptManager.LogToCode(context, "No HealthBehavior in Damage call!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            healthBehavior.Damage(num.IntValue());
            return Intrinsic.Result.Null;
		};
    }
    private void Damage(int amount)
    {
        //Debug.Log("damaging " + amount);
        _sceneObject.InvokeEventOnBehaviors(OnDamageEventName);
        int prevHealth = Health;
        Health -= amount;
        if(Health <= 0 && prevHealth > 0)
            OnKilled(true);
        // Keep Health synchronized
        OnPropertiesChange(true, true);
    }
    private void OnKilled(bool didWeKill)
    {
        _sceneObject.InvokeEventOnBehaviors(OnKilledEventName);
        // TODO my spidey senses are telling me that there's a race condition
        // here somewhere...
        CharacterBehavior characterBehavior = _sceneObject.CharacterBehavior;
        if(characterBehavior != null)
            _sceneObject.RemoveBehavior(characterBehavior, didWeKill);
        if (_sceneObject.RagdollBehavior != null)
            _sceneObject.RagdollBehavior.LocalUnpin();
    }
    public override bool DoesRequirePosRotScaleSyncing()
    {
        return false;
    }
    public override bool DoesRequireCollider()
    {
        return false;
    }
    public override List<ExposedFunction> GetFunctions()
    {
        return _userFunctions;
    }
    public override List<ExposedVariable> GetVariables()
    {
        return null;
    }
    public override void RefreshProperties() { }
    public override bool DoesRequireRigidbody()
    {
        return false;
    }
}
