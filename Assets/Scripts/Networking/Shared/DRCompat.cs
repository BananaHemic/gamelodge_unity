using DarkRift;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
#if UNITY
using UnityEngine;
#else
#endif

/// <summary>
/// Compatibility layer between the game server and the executable
/// </summary>
public static class DRCompat
{
    private static readonly char[] _workingCharArray = new char[byte.MaxValue];
    public static string ReadStringSmallerThan255(DarkRiftReader reader)
    {
        byte strLen = reader.ReadByte();
        if (strLen == 0)
            return string.Empty;

        lock (_workingCharArray)// Technically, this lock isn't needed at present
        {
			reader.ReadRawASCIICharsInto(_workingCharArray, 0, strLen);
			//TODO we should check if there is already a string with these characters,
			// and use that instead, to further reduce GC
            return new string(_workingCharArray, 0, strLen);
        }
    }
    public static void WriteStringSmallerThen255(DarkRiftWriter writer, string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            writer.Write((byte)0);
            return;
        }

        if (str.Length > byte.MaxValue)
            throw new Exception("String too large! Length " + str.Length);

        writer.Write((byte)str.Length);
		writer.WriteRawASCII(str, 0, str.Length);
    }
    /// <summary>
    /// Reads a large string into a StringBuilder
    /// pass a null StringBuilder to clear the reader
    /// </summary>
    /// <param name="reader"></param>
    /// <param name="sb"></param>
    /// <returns></returns>
    public static int ReadLargeString(DarkRiftReader reader, StringBuilder sb)
    {
        int strLen = reader.ReadInt32();
        if (sb != null)
            sb.Clear();
        if (strLen == 0)
            return strLen;
        if (sb != null)
            sb.EnsureCapacity(strLen);

        if (sb != null)
			reader.ReadRawASCIICharsInto(sb, strLen);
        else
			reader.DropData(strLen);
        return strLen;
    }
    public static void WriteLargeString(DarkRiftWriter writer, StringBuilder sb, int codeLen)
    {
        writer.Write(codeLen);

		writer.WriteRawASCII(sb, 0, codeLen);
        //for (int i = 0; i < codeLen; i++)
            //writer.Write((byte)sb[i]);
    }
	/// <summary>
	/// Used when compressing float values, where the decimal portion of the floating point value
	/// is multiplied by this number prior to storing the result in an Int16. Doing this allows 
	/// us to retain five decimal places, which for many purposes is more than adequate.
	/// </summary>
	// We tried two different compression schemes, one from https://gist.github.com/StagPoint/bb7edf61c2e97ce54e3e4561627f6582
	// that had an average error of 0.0026 degrees with 7 bytes / rotation. We then changed the compression to
	// instead compress the rotation down to 5 bytes, which has an average error of 0.016 degrees
	//const float FLOAT_PRECISION_MULT = (float)short.MaxValue;
	const float FLOAT_PRECISION_MULT_AB = (float)((1 << 12) - 1);
	const float FLOAT_PRECISION_MULT_C = (float)((1 << 11) - 1);
	/// <summary>
	/// Writes a compressed Quaternion value to the network stream. This function uses the "smallest three"
	/// method, which is well summarized here: http://gafferongames.com/networked-physics/snapshot-compression/
	/// </summary>
	/// <param name="writer">The stream to write the compressed rotation to.</param>
	/// <param name="rotation">The rotation value to be written to the stream.</param>
	public static void WriteCompressedRotation(this DarkRiftWriter writer, Quaternion rotation)
	{
		byte maxIndex = 0;
		float maxValue = float.MinValue;
		float sign = 1f;

		// Determine the index of the largest (absolute value) element in the Quaternion.
		// We will transmit only the three smallest elements, and reconstruct the largest
		// element during decoding. 
		for (byte i = 0; i < 4; i++)
		{
			var element = rotation[i];
			var abs = Abs(element);
			if (abs > maxValue)
			{
				// We don't need to explicitly transmit the sign bit of the omitted element because you 
				// can make the omitted element always positive by negating the entire quaternion if 
				// the omitted element is negative (in quaternion space (x,y,z,w) and (-x,-y,-z,-w) 
				// represent the same rotation.), but we need to keep track of the sign for use below.
				sign = (element < 0) ? -1f : 1f;

				// Keep track of the index of the largest element
				maxIndex = i;
				maxValue = abs;
			}
		}

		// If the maximum value is approximately 1f (such as Quaternion.identity [0,0,0,1]), then we can 
		// reduce storage even further due to the fact that all other fields must be 0f by definition, so 
		// we only need to send the index of the largest field.
		//if (Mathf.Approximately(maxValue, 1f))
		//{
		//	writer.Write((byte)(maxIndex + 4));
		//	return;
		//}

		int a, b, c;
		// We multiply the value of each element by QUAT_PRECISION_MULT before converting to 16-bit integer 
		// in order to maintain precision. This is necessary since by definition each of the three smallest 
		// elements are less than 1.0, and the conversion to 16-bit integer would otherwise truncate everything 
		// to the right of the decimal place. This allows us to keep five decimal places.
		if (maxIndex == 0)
		{
			a = RoundToInt(rotation.y * sign * FLOAT_PRECISION_MULT_AB);
			b = RoundToInt(rotation.z * sign * FLOAT_PRECISION_MULT_AB);
			c = RoundToInt(rotation.w * sign * FLOAT_PRECISION_MULT_C);
		}
		else if (maxIndex == 1)
		{
			a = RoundToInt(rotation.x * sign * FLOAT_PRECISION_MULT_AB);
			b = RoundToInt(rotation.z * sign * FLOAT_PRECISION_MULT_AB);
			c = RoundToInt(rotation.w * sign * FLOAT_PRECISION_MULT_C);
		}
		else if (maxIndex == 2)
		{
			a = RoundToInt(rotation.x * sign * FLOAT_PRECISION_MULT_AB);
			b = RoundToInt(rotation.y * sign * FLOAT_PRECISION_MULT_AB);
			c = RoundToInt(rotation.w * sign * FLOAT_PRECISION_MULT_C);
		}
		else
		{
			a = RoundToInt(rotation.x * sign * FLOAT_PRECISION_MULT_AB);
			b = RoundToInt(rotation.y * sign * FLOAT_PRECISION_MULT_AB);
			c = RoundToInt(rotation.z * sign * FLOAT_PRECISION_MULT_C);
		}

		//Debug.Log("max index " + maxIndex + " rot " + rotation.ToPrettyString());
		//Debug.Log("a int " + a);
		//Debug.Log("b int " + b);
		//Debug.Log("c int " + c);
		bool a_neg = a < 0;
		bool b_neg = b < 0;
		bool c_neg = c < 0;
		if (a_neg)
			a = -a;
		if (b_neg)
			b = -b;
		if (c_neg)
			c = -c;

		// Put this into just five bytes
		// first two bits for the index
		byte firstByte = (byte)(maxIndex << 6);
        // We need first 6 bits, a is 13 bits long, so move over 7
		firstByte |= (byte)(a >> 7);
		// Add the a sign bit
		if (a_neg)
			firstByte |= 1 << 5;
		// Load the next 7 bits from a
		byte secondByte = (byte)((a & ((1 << 7) - 1)) << 1);
		// Add the b sign bit to the end of the second byte
		if (b_neg)
			secondByte |= 1;
		// Load the next 8 bits from b
		byte thirdByte = (byte)((b & ~(1 << 12)) >> 4);
		// Load the last 4 bits from b
		byte forthByte = (byte)((b & (1 << 4) - 1) << 4);
		// Load the first 4 bits from c (which is only 12 bits long)
		forthByte |= (byte)(c >> 8);
		// Add the c sign bit
		if (c_neg)
			forthByte |= 1 << 3;
		// Load the last 8 bits from c
		byte fifthByte = (byte)(c & ((1 << 8) - 1));

		writer.Write(firstByte);
		writer.Write(secondByte);
		writer.Write(thirdByte);
		writer.Write(forthByte);
		writer.Write(fifthByte);

		//writer.Write(maxIndex);
		//writer.Write(a);
		//writer.Write(b);
		//writer.Write(c);
	}
	/// <summary>
	/// Reads a compressed rotation value from the network stream. This value must have been previously written
	/// with WriteCompressedRotation() in order to be properly decompressed.
	/// </summary>
	/// <param name="reader">The network stream to read the compressed rotation value from.</param>
	/// <returns>Returns the uncompressed rotation value as a Quaternion.</returns>
	public static Quaternion ReadCompressedRotation(this DarkRiftReader reader)
	{
		// Read the index of the omitted field from the stream.
		//var maxIndex = reader.ReadByte();
		//// Values greater than 4 indicate that only the index of the single field whose value is 1f was
		//// sent, and (maxIndex - 4) is the correct index for that field.
		//if (maxIndex >= 4)
		//{
		//	if (maxIndex == 4)
		//		return new Quaternion(1, 0, 0, 0);
		//	if (maxIndex == 5)
		//		return new Quaternion(0, 1, 0, 0);
		//	if (maxIndex == 6)
		//		return new Quaternion(0, 0, 1, 0);
		//	if (maxIndex == 7)
		//		return new Quaternion(0, 0, 0, 1);
		//	//Debug.LogError("Failed to properly read compressed rotation, bad max index")
		//}
		// Read the other three fields and derive the value of the omitted field
		//float a = reader.ReadInt16() / FLOAT_PRECISION_MULT_AB;
		//float b = reader.ReadInt16() / FLOAT_PRECISION_MULT_AB;
		//float c = reader.ReadInt16() / FLOAT_PRECISION_MULT_AB;
		//float d = Mathf.Sqrt(1f - (a * a + b * b + c * c));

		byte firstByte  = reader.ReadByte();
		byte secondByte = reader.ReadByte();
		byte thirdByte  = reader.ReadByte();
		byte forthByte  = reader.ReadByte();
		byte fifthByte  = reader.ReadByte();

		// Index is the first two bits
		int maxIndex = firstByte >> 6;
		// Get the a sign bit
		bool a_neg = (firstByte & (1 << 5)) != 0;
		// a is last 6 bits from first, and 7 bits from second
		int a_int = (firstByte & ((1 << 5) - 1)) << 7;
		a_int |= secondByte >> 1;
		// Get the b sign bit from the end of the second byte
		bool b_neg = (secondByte & 1) != 0;
		// Next 8 bits from the third byte
		int b_int = thirdByte << 4;
		// Last 4 bits of b from the forth byte
		b_int |= (forthByte >> 4);
		// Get the c sign bit
		bool c_neg = (forthByte & (1 << 3)) != 0;
		// Take the last 3 bits from the forth into c
		int c_int = (forthByte & ((1 << 3) - 1)) << 8;
		// Take all the 8 bits from the fifth into the end of c
		c_int |= fifthByte;
		//Debug.Log("max index " + maxIndex);
		if (a_neg)
			a_int = -a_int;
		if (b_neg)
			b_int = -b_int;
		if (c_neg)
			c_int = -c_int;
		//Debug.Log("a(int): " + a_int);
		//Debug.Log("b(int): " + b_int);
		//Debug.Log("c(int): " + c_int);

		float a = a_int / FLOAT_PRECISION_MULT_AB;
		float b = b_int / FLOAT_PRECISION_MULT_AB;
		float c = c_int / FLOAT_PRECISION_MULT_C;
		float d = Sqrt(1f - (a * a + b * b + c * c));

		if (maxIndex == 0)
			return new Quaternion(d, a, b, c);
		else if (maxIndex == 1)
			return new Quaternion(a, d, b, c);
		else if (maxIndex == 2)
			return new Quaternion(a, b, d, c);
		return new Quaternion(a, b, c, d);
	}
	public static float Abs(float f)
    {
#if UNITY
        return Mathf.Abs(f);
#else
		return Math.Abs(f);
#endif
	}
	public static int RoundToInt(float f)
    {
#if UNITY
        return Mathf.RoundToInt(f);
#else
		return (int)Math.Round(f);
#endif
	}
	public static float Sqrt(float f)
    {
#if UNITY
        return Mathf.Sqrt(f);
#else
		return (float)Math.Sqrt(f);
#endif
    }
    public static float Clamp(float val, float min, float max)
    {
        if (val < min)
            return min;
        if (val > max)
            return max;
        return val;
    }
    public static void Log(string message)
    {
#if UNITY
        Debug.Log(message);
#else
        Console.WriteLine(message);
#endif
    }
    public static void LogWarning(string message)
    {
#if UNITY
        Debug.LogWarning(message);
#else
        Console.WriteLine("WRN: " + message);
#endif
    }
    public static void LogError(string message)
    {
#if UNITY
        Debug.LogError(message);
#else
        Console.Error.WriteLine(message);
#endif
    }

}