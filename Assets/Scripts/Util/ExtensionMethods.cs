using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public static class ExtensionMethods
{
    /// <summary>
    /// Sets a joint's targetRotation to match a given local rotation.
    /// The joint transform's local rotation must be cached on Start and passed into this method.
    /// </summary>
    public static void SetTargetRotationLocal(this ConfigurableJoint joint, Quaternion targetLocalRotation, Quaternion startLocalRotation)
    {
        if (joint.configuredInWorldSpace)
        {
            Debug.LogError("SetTargetRotationLocal should not be used with joints that are configured in world space. For world space joints, use SetTargetRotation.", joint);
        }
        SetTargetRotationInternal(joint, targetLocalRotation, startLocalRotation, Space.Self);
    }

    /// <summary>
    /// Sets a joint's targetRotation to match a given world rotation.
    /// The joint transform's world rotation must be cached on Start and passed into this method.
    /// </summary>
    public static void SetTargetRotation(this ConfigurableJoint joint, Quaternion targetWorldRotation, Quaternion startWorldRotation)
    {
        if (!joint.configuredInWorldSpace)
        {
            Debug.LogError("SetTargetRotation must be used with joints that are configured in world space. For local space joints, use SetTargetRotationLocal.", joint);
        }
        SetTargetRotationInternal(joint, targetWorldRotation, startWorldRotation, Space.World);
    }

    public static void SetTargetRotationInternal(ConfigurableJoint joint, Quaternion targetRotation, Quaternion startRotation, Space space)
    {
        // Calculate the rotation expressed by the joint's axis and secondary axis
        var right = joint.axis;
        var forward = Vector3.Cross(joint.axis, joint.secondaryAxis).normalized;
        var up = Vector3.Cross(forward, right).normalized;
        Quaternion worldToJointSpace = Quaternion.LookRotation(forward, up);

        // Transform into world space
        Quaternion resultRotation = Quaternion.Inverse(worldToJointSpace);

        // Counter-rotate and apply the new local rotation.
        // Joint space is the inverse of world space, so we need to invert our value
        if (space == Space.World)
        {
            resultRotation *= startRotation * Quaternion.Inverse(targetRotation);
        }
        else
        {
            resultRotation *= Quaternion.Inverse(targetRotation) * startRotation;
        }

        // Transform back into joint space
        resultRotation *= worldToJointSpace;

        // Set target rotation to our newly calculated rotation
        joint.targetRotation = resultRotation;
    }
    public static void SimulateRotateAround(this Transform transform, Vector3 center, Vector3 axis, float angle, out Vector3 pos, out Quaternion rot)
    {
        pos = transform.position;
        rot = Quaternion.AngleAxis(angle, axis); // get the desired rotation
        Vector3 dir = pos - center; // find current direction relative to center
        dir = rot * dir; // rotate the direction
        pos = center + dir; // define new position
        //rot = transform.rotation * rot; // rotate object to keep looking at the center
        rot = rot * transform.rotation; // rotate object to keep looking at the center
    }
    public static string ReplaceAfter(this string str, char oldChar, char newChar, int startIdx)
    {
        char[] chars = str.ToCharArray();
        for(int i = startIdx; i < chars.Length; i++)
        {
            if (chars[i] == oldChar)
                chars[i] = newChar;
        }
        return new string(chars);
    }
    /// <summary>
    /// Gets the rotation that the object will have the next frame, based on angular velocity
    /// TODO test me
    /// </summary>
    /// <param name="previousRotation"></param>
    /// <param name="currentRotation"></param>
    /// <param name="dt"></param>
    /// <returns></returns>
    public static Quaternion ApplyAngularVelocity(Quaternion previousRotation, Vector3 angularVelocity, float dt)
    {
        Quaternion velRot = Quaternion.AngleAxis(angularVelocity.magnitude * Mathf.Rad2Deg * dt, angularVelocity.normalized);
        return velRot * previousRotation;
    }
    /// <summary>
    /// Get the angular velocity based on two quaternions
    /// https://forum.unity.com/threads/manually-calculate-angular-velocity-of-gameobject.289462/
    /// TODO test me
    /// </summary>
    /// <param name="previousRotation"></param>
    /// <param name="currentRotation"></param>
    /// <param name="dt"></param>
    /// <returns></returns>
    public static Vector3 GetAngularVelocity(Quaternion previousRotation, Quaternion currentRotation, float dt)
    {
        var q = currentRotation * Quaternion.Inverse(previousRotation);
        // We use double to better handle small rotations
        double qw = q.w;
        double gain;
        // handle negatives, we could just flip it but this is faster
        if (q.w < 0.0f)
        {
            var angle = Math.Acos(-qw);
            gain = -2.0 * angle / (Math.Sin(angle) * dt);
        }
        else
        {
            var angle = Math.Acos(qw);
            gain = 2.0 * angle / (Math.Sin(angle) * dt);
        }
        
        if(double.IsNaN(gain))
            return new Vector3(0, 0, 0);
        return new Vector3((float)(q.x * gain), (float)(q.y * gain), (float)(q.z * gain));
    }
    public static float CosLerp(float a, float b, float dist)
    {
        dist = Mathf.Clamp01(dist);
        float amountA = (0.5f + Mathf.Cos(dist * Mathf.PI) / 2);
        float amountB = 1 - amountA;
        return amountA * a + amountB * b;
    }
    // Breadth first
    public static Transform FindDeepChild_Breadth(this Transform aParent, string aName)
    {
        Queue<Transform> queue = new Queue<Transform>();
        queue.Enqueue(aParent);
        while (queue.Count > 0)
        {
            var c = queue.Dequeue();
            if (c.name == aName)
                return c;
            foreach (Transform t in c)
                queue.Enqueue(t);
        }
        return null;
    }
     //Depth-first search
     public static Transform FindDeepChild_DepthFirst(this Transform aParent, string aName)
     {
         foreach(Transform child in aParent)
         {
             if(child.name == aName )
                 return child;
             var result = child.FindDeepChild_DepthFirst(aName);
             if (result != null)
                 return result;
         }
         return null;
     }
    public static SceneObject GetSceneObjectFromTransform(this Transform obj)
    {
        // TODO we should be able to use Transform root, when not in play mode
        // and not using the DynamicObjectContainer
        // scene objects are always on the default layer
        while(obj != null)
        {
            if (obj.CompareTag(GLLayers.SceneObjectTag))
                return obj.GetComponent<SceneObject>();

            obj = obj.transform.parent;
        }
        return null;
    }
    public static NetworkObject GetNetworkObjectFromParent(this GameObject obj)
    {
        // We already know that the hit object isn't in the default layer
        Transform parent = obj.transform.parent;
        if (parent == null)
            return null;
        obj = parent.gameObject;
        // TODO we should be able to use Transform root, when not in play mode
        // and not using the DynamicObjectContainer
        // network objects and scene objects are always on the default layer
        while(obj != null)
        {
            if (obj.CompareTag(GLLayers.SceneObjectTag))
                return obj.GetComponent<NetworkObject>();

            obj = obj.transform.parent.gameObject;
        }
        return null;
    }
    public static bool IntersectsHighQuality(this BoxCollider A, BoxCollider B)
    {
        // The Unity Bounds Intersect method uses
        // AABB to determine intersection, which
        // is very performant, but also frequently
        // wrong when the bounds have rotations
        //return boundsA.Intersects(boundsB);
        //return boundsA.Contains(boundsB.ClosestPoint(boundsA.center));
        //return A.bounds.Contains(B.bounds.ClosestPoint(A.center));
        //return true;

        Vector3 dir;
        float dist;
        return Physics.ComputePenetration(A, A.transform.position, A.transform.rotation,
            B, B.transform.position, B.transform.rotation, out dir, out dist);
    }
    public static bool AlmostEquals(this Vector3 vec1, Vector3 vec2)
    {
        return (vec1 - vec2).sqrMagnitude < 0.1f;
    }
    public static int NumInstancesOf(this string str, char charToCount)
    {
        int count = 0;
        foreach (char c in str)
        {
            if (charToCount == '/')
                count++;
        }
        return count;
    }
    public static void WriteASCII(this byte[] ray, string str, ref int offset)
    {
        for (int j = 0; j < str.Length; j++)
            ray[offset++] = (byte)str[j];
    }
    public static string ReadASCII(this byte[] ray, ref int offset, int len)
    {
        char[] chars = new char[len];
        for (int i = 0; i < len; i++)
            chars[i] = (char)ray[offset++];
        return new string(chars);
    }
    // TODO settings for how much to compress it
    public static void Serialize(this Vector3 vec, DarkRift.DarkRiftWriter writer)
    {
        writer.Write(vec.x);
        writer.Write(vec.y);
        writer.Write(vec.z);
    }
    public static DarkRift.Vec3 ToVec3(this Vector3 vec, DarkRift.Vec3 intoVec = null)
    {
        if(intoVec == null)
            return new DarkRift.Vec3(vec.x, vec.y, vec.z);
        intoVec.X = vec.x;
        intoVec.Y = vec.y;
        intoVec.Z = vec.z;
        return intoVec;
    }
    public static Vec2 ToVec2(this Vector2 vec, Vec2 intoVec = null)
    {
        if(intoVec == null)
            return new Vec2(vec.x, vec.y);
        intoVec.X = vec.x;
        intoVec.Y = vec.y;
        return intoVec;
    }
    public static Vector2 ToVector2(this Vec2 vec)
    {
        return new Vector2(vec.X, vec.Y);
    }
    public static DarkRift.Quat ToQuat(this Quaternion quaternion, DarkRift.Quat intoRot=null)
    {
        if(intoRot == null)
            return new DarkRift.Quat(quaternion.x, quaternion.y, quaternion.z, quaternion.w);
        intoRot.X = quaternion.x;
        intoRot.Y = quaternion.y;
        intoRot.Z = quaternion.z;
        intoRot.W = quaternion.w;
        return intoRot;
    }
    public static string ToPrettyString(this Vector3 vec)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("(");
        sb.Append(vec.x);
        sb.Append(", ");
        sb.Append(vec.y);
        sb.Append(", ");
        sb.Append(vec.z);
        sb.Append(")");
        return sb.ToString();
    }
    public static string ToPrettyString(this Vector2 vec)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("(");
        sb.Append(vec.x);
        sb.Append(", ");
        sb.Append(vec.y);
        sb.Append(")");
        return sb.ToString();
    }
    public static string ToPrettyString(this Quaternion vec)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("(");
        sb.Append(vec.x);
        sb.Append(", ");
        sb.Append(vec.y);
        sb.Append(", ");
        sb.Append(vec.z);
        sb.Append(", ");
        sb.Append(vec.w);
        sb.Append(")");
        return sb.ToString();
    }
    public static StringBuilder ToPrettyString(this List<int> vec)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("[");
        for(int i = 0; i < vec.Count; i++)
        {
            sb.Append(i);
            if(i != vec.Count - 1)
                sb.Append(",");
            else
                sb.Append("]");
        }
        return sb;
    }
    public static byte[] ToByteArray(this ArraySegment<byte> segment)
    {
        byte[] newArray = new byte[segment.Count];

        int srcIndex = segment.Offset;
        int dstIndex = 0;
        while (dstIndex < newArray.Length)
            newArray[dstIndex++] = segment.Array[srcIndex++];
        return newArray;
    }
    public static void SetLayerRecursively(this GameObject go, List<int> layers, bool includeParent=true)
    {
        int i = 0;
        if (includeParent)
            go.layer = layers[i++];
        foreach (Transform trans in go.GetComponentsInChildren<Transform>(true))
        {
            trans.gameObject.layer = layers[i++];
        }
    }
    public static void SetLayerRecursively(this GameObject go, int layerNumber, List<int> prevLayers = null, bool includeParent=true)
    {
        if (includeParent)
        {
            if(prevLayers != null)
                prevLayers.Add(go.layer);
            go.layer = layerNumber;
        }
        foreach (Transform trans in go.GetComponentsInChildren<Transform>(true))
        {
            if(prevLayers != null)
                prevLayers.Add(trans.gameObject.layer);
            trans.gameObject.layer = layerNumber;
        }
    }
    public static void SetTagRecursively(this GameObject go, string tag, List<string> prevTag = null, bool includeParent=true)
    {
        if (includeParent)
        {
            if(prevTag != null)
                prevTag.Add(go.tag);
            go.tag = tag;
        }
        foreach (Transform trans in go.GetComponentsInChildren<Transform>(true))
        {
            if(prevTag != null)
                prevTag.Add(trans.gameObject.tag);
            trans.gameObject.tag = tag;
        }
    }
    /// <summary>
    /// Returns an escaped string that's ready to be in a Firebase value
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static string FirebaseEscapeValue(this string str)
    {
        // TODO we can probably make something faster
        return str.Replace("\\","\\\\");
    }
    /// <summary>
    /// Returns an escaped string that's ready to be in a Firebase value
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static StringBuilder FirebaseEscapeValue(this StringBuilder str)
    {
        // TODO we can probably make something faster
        return str.Replace("\\","\\\\");
    }
    public static bool IsNumber(char c)
    {
        return c == '1'
            || c == '2'
            || c == '3'
            || c == '4'
            || c == '5'
            || c == '6'
            || c == '7'
            || c == '8'
            || c == '9'
            || c == '0';
    }
    public static bool IsInt(this string myString)
    {
        if (string.IsNullOrEmpty(myString))
            return false;

        for (int i = 0; i < myString.Length; i++)
        {
            if (!IsNumber(myString[i]))
                return false;
        }
        return true;
    }
    const char split_char = '|';
    public static void SerializeToString(this Vector3 vec, StringBuilder sb)
    {
        sb.Append(vec.x);
        sb.Append(split_char);
        sb.Append(vec.y);
        sb.Append(split_char);
        sb.Append(vec.z);
        //sb.Append(split_char); // We end with a split char for easier deserialization
    }
    public static void SerializeToString(this Vector2 vec, StringBuilder sb)
    {
        sb.Append(vec.x);
        sb.Append(split_char);
        sb.Append(vec.y);
        //sb.Append(split_char); // We end with a split char for easier deserialization
    }
    public static string SerializeToString(this Vector2 vec)
    {
        StringBuilder sb = new StringBuilder();
        vec.SerializeToString(sb);
        return sb.ToString();
    }
    public static string SerializeToString(this Vector3 vec)
    {
        StringBuilder sb = new StringBuilder();
        vec.SerializeToString(sb);
        return sb.ToString();
    }
    public static void SerializeToString(this Quaternion quat, StringBuilder sb)
    {
        sb.Append(quat.x);
        sb.Append(split_char);
        sb.Append(quat.y);
        sb.Append(split_char);
        sb.Append(quat.z);
        sb.Append(split_char);
        sb.Append(quat.w);
        //sb.Append(split_char); // We end with a split char for easier deserialization
    }
    public static string SerializeToString(this Quaternion quat)
    {
        StringBuilder sb = new StringBuilder();
        quat.SerializeToString(sb);
        return sb.ToString();
    }
    public static void SerializeToString(this List<int> ray, StringBuilder sb)
    {
        int i = 0;
        for (; i < ray.Count - 1; i++)
        {
            sb.Append(ray[i]);
            sb.Append(split_char);
        }
        // Append the last character, but not followed by split_char
        if (i >= 0 && ray.Count > 0)
            sb.Append(ray[i]);
        //sb.Append(split_char); // We end with a split char for easier deserialization
    }
    public static Vector2 DeSerializeVec2FromString(this string serialized, ref int offset)
    {
        Vector2 vec = new Vector2();
        // Most optimized method I could come up with
        // sure wish they added a start/stop index overload
        // for float parsing
        StringBuilder sb = new StringBuilder();
        int vec_idx = 0;
        for (; offset < serialized.Length; offset++)
        {
            // Keep pulling out a string, until we hit a stop
            // char (,) or the end
            char c = serialized[offset];
            if (!IsNumber(c) && c != '.' && c != '-' && c != 'E')
            {
                // We've hit the end of this float
                string float_str = sb.ToString();
                float parsed;
                if (!float.TryParse(float_str, out parsed))
                {
                    // Failed to parse!
                    Debug.LogError("Failed to parse vec3! " + float_str);
                    return vec;
                }
                vec[vec_idx++] = parsed;
                sb.Clear();
                // break if we're done
                if (vec_idx == 3)
                    break;
                continue;
            }

            sb.Append(c);
        }
        // We hit the end of the string
        // parse the last bit if we need to
        if (vec_idx < 2)
        {
            string float_str = sb.ToString();
            float parsed;
            if (!float.TryParse(float_str, out parsed))
            {
                // Failed to parse!
                Debug.LogError("Failed to parse vec3! " + float_str);
                return vec;
            }
            vec[vec_idx++] = parsed;
        }
        return vec;
    }
    public static string RemoveEndNumbers(this string str)
    {
        // Doesn't have a number at the end, just return the initial one
        if (!IsNumber(str[str.Length - 1]))
            return str;
        // Check from the back to the forward for
        // numbers
        for(int i = str.Length - 2; i >=0; i--)
        {
            if (!IsNumber(str[i]))
                return str.Substring(0, i + 1);
        }
        // They're all numbers
        return string.Empty;
    }
    public static Vector3 DeSerializeVec3FromString(this string serialized, ref int offset)
    {
        Vector3 vec = new Vector3();
        // Most optimized method I could come up with
        // sure wish they added a start/stop index overload
        // for float parsing
        StringBuilder sb = new StringBuilder();
        int vec_idx = 0;
        for (; offset < serialized.Length; offset++)
        {
            // Keep pulling out a string, until we hit a stop
            // char (,) or the end
            char c = serialized[offset];
            if (!IsNumber(c) && c != '.' && c != '-' && c != 'E')
            {
                // We've hit the end of this float
                string float_str = sb.ToString();
                float parsed;
                if (!float.TryParse(float_str, out parsed))
                {
                    // Failed to parse!
                    Debug.LogError("Failed to parse vec3! " + float_str);
                    return vec;
                }
                vec[vec_idx++] = parsed;
                sb.Clear();
                // break if we're done
                if (vec_idx == 3)
                    break;
                continue;
            }

            sb.Append(c);
        }
        // We hit the end of the string
        // parse the last bit if we need to
        if (vec_idx < 3)
        {
            string float_str = sb.ToString();
            float parsed;
            if (!float.TryParse(float_str, out parsed))
            {
                // Failed to parse!
                Debug.LogError("Failed to parse vec3! " + float_str);
                return vec;
            }
            vec[vec_idx++] = parsed;
        }
        return vec;
    }
    public static Quaternion DeSerializeQuaternionFromString(this string serialized, ref int offset)
    {
        Quaternion quat = new Quaternion();
        // Most optimized method I could come up with
        // sure wish they added a start/stop index overload
        // for float parsing
        StringBuilder sb = new StringBuilder();
        int quat_idx = 0;
        for (; offset < serialized.Length; offset++)
        {
            // Keep pulling out a string, until we hit a stop
            // char (,) or the end
            char c = serialized[offset];
            if (!IsNumber(c) && c != '.' && c != '-' && c != 'E')
            {
                // We've hit the end of this float
                string float_str = sb.ToString();
                float parsed;
                if (!float.TryParse(float_str, out parsed))
                {
                    // Failed to parse!
                    Debug.LogError("Failed to parse quaternion! " + float_str);
                    return quat;
                }
                quat[quat_idx++] = parsed;
                sb.Clear();
                if (quat_idx == 4)
                    break;
                continue;
            }

            sb.Append(c);
        }
        // We hit the end of the string
        // parse the last bit if we need to
        if (quat_idx < 4)
        {
            string float_str = sb.ToString();
            float parsed;
            if (!float.TryParse(float_str, out parsed))
            {
                // Failed to parse!
                Debug.LogError("Failed to parse quaternion! " + float_str);
                return quat;
            }
            quat[quat_idx++] = parsed;
        }
        return quat;
    }
    public static int DeSerializeIntFromString(this string serialized, ref int offset)
    {
        int num = -1;

        if (offset >= serialized.Length)
            return num;
        // Keep parsing out ints until we hit the end of the string
        StringBuilder sb = new StringBuilder();
        // allow the first character to be negative
        char firstChar = serialized[offset++];
        if (!IsNumber(firstChar) && firstChar != '-')
            return num;
        sb.Append(firstChar);

        for (; offset < serialized.Length; offset++)
        {
            // Keep pulling out a string, until we hit a stop
            // char (,) or the end
            char c = serialized[offset];
            if (!IsNumber(c))
                break;
            sb.Append(c);
        }
        // We've hit the end of this float
        string val_str = sb.ToString();
        if (!int.TryParse(val_str, out num))
        {
            // Failed to parse!
            Debug.LogError("Failed to parse int! " + val_str);
            return -1;
        }
        return num;
    }
    public static void DeSerializeIntListFromString(this string serialized, ref int offset, List<int> list)
    {
        if (list == null)
        {
            Debug.LogError("Please provide a int list!");
            return;
        }
        if (offset >= serialized.Length)
            return;
        // Keep parsing out ints until we hit the end of the string
        StringBuilder sb = new StringBuilder();
        // allow the first character to be negative
        char firstChar = serialized[offset++];
        if (!IsNumber(firstChar) && firstChar != '-')
            return;
        sb.Append(firstChar);

        for (; offset < serialized.Length; offset++)
        {
            // Keep pulling out a string, until we hit a stop
            // char (,) or the end
            char c = serialized[offset];
            if (!IsNumber(c))
            {
                string int_str = sb.ToString();
                int parsed;
                if (!int.TryParse(int_str, out parsed))
                {
                    Debug.LogError("Failed to parse out int! " + parsed);
                    return;
                }
                list.Add(parsed);
                sb.Clear();
                continue;
            }
            sb.Append(c);
        }
        // We've hit the end of this string,
        // parse what we have left
        string val_str = sb.ToString();
        if (!string.IsNullOrEmpty(val_str))
        {
            int parsed;
            if (!int.TryParse(val_str, out parsed))
            {
                // Failed to parse!
                Debug.LogError("Failed to parse int! " + val_str);
                return;
            }
            list.Add(parsed);
        }
    }
    public static void SetAlpha(this Image img, float a)
    {
        Color col = img.color;
        col.a = a;
        img.color = col;
    }
    /// <summary>
    /// Gets the magnitude difference between two uints
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <param name="isAGreater">Is A greater than or equal to B</param>
    /// <returns></returns>
    public static uint SafeDifference(uint a, uint b, out bool isAGreater)
    {
        if (a >= b)
        {
            isAGreater = true;
            return (a - b);
        }
        isAGreater = false;
        return b - a;
    }
    /// <summary>
    /// Gets the difference between two uints, with proper clamping
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static int SafeDifferenceInt(uint a, uint b)
    {
        uint del;
        if (a >= b)
        {
            del = a - b;
            if (del >= int.MaxValue)
                return int.MaxValue;
            return (int)del;
        }
        del = b - a;
        if (del >= int.MaxValue)
            return int.MinValue;
        return -(int)del;
    }
    // O(1) 
    public static void RemoveBySwap<T>(this List<T> list, int index)
    {
        list[index] = list[list.Count - 1];
        list.RemoveAt(list.Count - 1);
    }

    // O(n)
    public static bool RemoveBySwap<T>(this List<T> list, T item)
    {
        int index = list.IndexOf(item);
        if (index == -1)
            return false;
        RemoveBySwap(list, index);
        return true;
    }

    // O(n)
    public static void RemoveBySwap<T>(this List<T> list, Predicate<T> predicate)
    {
        int index = list.FindIndex(predicate);
        RemoveBySwap(list, index);
    }
    /// <summary>
    /// Adds two uints, clamping to the max value
    /// when needed
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static uint ClampedAdd(uint a, uint b)
    {
        uint res = a + b;
        if (res <= a && res <= b)
            return uint.MaxValue;
        return res;
    }
}
