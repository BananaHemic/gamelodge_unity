using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Miniscript;

public class ValUser : ValCustom
{
    public readonly DRUser User;

    private static readonly List<ExposedFunction> _exposedFunctions = new List<ExposedFunction>();
    private static bool _hasLoadedIntrinsics = false;
    private static Intrinsic _getRealControllerPosFunc;

    // Variable names
    const string IdName = "ID";
    const string IsLocalName = "IsLocal";
    // Function names
    const string GetRealPositionRotationName = "GetRealControllerPosRot";

    public ValUser(DRUser user) : base(false)
    {
        User = user;
    }
    public override bool Resolve(string identifier, out Value val)
    {
        switch (identifier)
        {
            case IdName:
                val = ValNumber.Create(User.ID);
                return true;
            case IsLocalName:
                val = ValNumber.Truth(User.ID == DarkRiftConnection.Instance.OurID);
                return true;
            case GetRealPositionRotationName:
                val = _getRealControllerPosFunc.GetFunc();
                return true;
        }
        val = ValNull.instance;
        return false;
        //TODO name function
    }
    public static void InitIntrinsics()
    {
        if (_hasLoadedIntrinsics)
            return;
        _hasLoadedIntrinsics = true;

        _getRealControllerPosFunc = Intrinsic.Create(GetRealPositionRotationName, false);
        _getRealControllerPosFunc.AddParam("bodyPart", ValString.empty);
        _getRealControllerPosFunc.code = (context, partialResult) => {
            ValUser valUser = context.GetVar(ValString.selfStr) as ValUser;
            if(valUser == null)
            {
                UserScriptManager.LogToCode(context, "Can't GetRealPosition, no user used as object!", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }

            DRUser user = valUser.User;
            Value recvValue = context.GetVar("bodyPart");
            ValString recvStr = recvValue as ValString;
            string bodyPart = recvStr?.value;
            if (string.IsNullOrEmpty(bodyPart))
                bodyPart = "head";
            // TODO we should force-update the transform now, if this is for our user
            // just to get a bit less latency
            if (!UserManager.Instance.TryGetUserDisplay(user.ID, out UserDisplay userDisplay))
            {
                UserScriptManager.LogToCode(context, "No player #" + user.ID + " the user may have disconnected", UserScriptManager.CodeLogType.Warning);
                return Intrinsic.Result.Null;
            }
            ValMap result = ValMap.Create();
            if (bodyPart == "head")
            {
                result[ValString.positionStr] = new ValVector3(userDisplay.PoseDisplay.HeadTransform.position);
                result[ValString.rotationStr] = new ValQuaternion(userDisplay.PoseDisplay.HeadTransform.rotation);
            }
            else if (bodyPart == "handL")
            {
                result[ValString.positionStr] = new ValVector3(userDisplay.PoseDisplay.LHandTransform.position);
                result[ValString.rotationStr] = new ValQuaternion(userDisplay.PoseDisplay.LHandTransform.rotation);
            }
            else if (bodyPart == "handR")
            {
                result[ValString.positionStr] = new ValVector3(userDisplay.PoseDisplay.RHandTransform.position);
                result[ValString.rotationStr] = new ValQuaternion(userDisplay.PoseDisplay.RHandTransform.rotation);
            }
            else
            {
                result.Unref();
                UserScriptManager.LogToCode(context, "Unknown body part \"" + bodyPart + "\"", UserScriptManager.CodeLogType.Error);
                return Intrinsic.Result.Null;
            }
            return new Intrinsic.Result(result);
		};
    }
    public override double Equality(Value rhs, int recursionDepth = 16)
    {
        return rhs is ValUser && ((ValUser)rhs).User == User ? 1 : 0;
    }
    public override int Hash(int recursionDepth = 16)
    {
        return User.GetHashCode();
    }
    public override string ToString(Machine vm)
    {
        return "User#" + User.ID;
    }
    protected override void ResetState()
    {
    }
    protected override void ReturnToPool()
    {
    }

}
