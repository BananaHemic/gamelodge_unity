using DarkRift;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// The user's current mouth orientation.
/// This is only used on the client side, the server simply passes voice packets around
/// without deserializing into a mouth pose
/// </summary>
public class DRMouthPose : IDarkRiftSerializable
{
    public float Sil { get { return PhonemeWeights[0]; } set { PhonemeWeights[0] = value; } }
    public float PP { get { return PhonemeWeights[1]; } set { PhonemeWeights[1] = value; } }
    public float FF { get { return PhonemeWeights[2]; } set { PhonemeWeights[2] = value; } }
    public float TH { get { return PhonemeWeights[3]; } set { PhonemeWeights[3] = value; } }
    public float DD { get { return PhonemeWeights[4]; } set { PhonemeWeights[4] = value; } }
    public float KK { get { return PhonemeWeights[5]; } set { PhonemeWeights[5] = value; } }
    public float CH { get { return PhonemeWeights[6]; } set { PhonemeWeights[6] = value; } }
    public float SS { get { return PhonemeWeights[7]; } set { PhonemeWeights[7] = value; } }
    public float NN { get { return PhonemeWeights[8]; } set { PhonemeWeights[8] = value; } }
    public float RR { get { return PhonemeWeights[9]; } set { PhonemeWeights[9] = value; } }
    public float AA { get { return PhonemeWeights[10]; } set { PhonemeWeights[10] = value; } }
    public float EE { get { return PhonemeWeights[11]; } set { PhonemeWeights[11] = value; } }
    public float IH { get { return PhonemeWeights[12]; } set { PhonemeWeights[12] = value; } }
    public float OH { get { return PhonemeWeights[13]; } set { PhonemeWeights[13] = value; } }
    public float OU { get { return PhonemeWeights[14]; } set { PhonemeWeights[14] = value; } }
    public float HA { get { return PhonemeWeights[15]; } set { PhonemeWeights[15] = value; } }

    public readonly static int NumPhonemes = 16;
    public readonly float[] PhonemeWeights = new float[NumPhonemes];

    public DRMouthPose() { }
    // Copy constructor
    public DRMouthPose(DRMouthPose mouthPose)
    {
        CopyFrom(mouthPose);
    }
    public void CopyFrom(DRMouthPose mouthPose)
    {
        Array.Copy(mouthPose.PhonemeWeights, PhonemeWeights, NumPhonemes);
    }
    public void Zero()
    {
        Array.Clear(PhonemeWeights, 0, NumPhonemes);
    }
    public void Serialize(SerializeEvent e)
    {
        Serialize(e.Writer);
    }
    public void Serialize(DarkRiftWriter writer)
    {
        // Get the largest and second largest values
        float largestVal = float.MinValue;
        int largestIndex = -1;
        float secondLargestVal = float.MinValue;
        int secondLargestIndex = -1;

        for (int i = 0; i < NumPhonemes; i++)
        {
            float val = PhonemeWeights[i];
            if (val > largestVal)
            {
                // move the current largest over
                secondLargestVal = largestVal;
                secondLargestIndex = largestIndex;
                // record the largest
                largestVal = val;
                largestIndex = i;
            }
            else if (val > secondLargestVal)
            {
                // update the second largest
                secondLargestVal = val;
                secondLargestIndex = i;
            }
        }
        // Serialize the phoneme indexes
        byte phonemeIndexes = (byte)((largestIndex << 4) | secondLargestIndex);
        // Serialize the weights
        byte largestWeight = (byte)DRCompat.RoundToInt(((1 << 4) - 1) * (largestVal / 100f));
        byte secondLargestWeight = (byte)DRCompat.RoundToInt(((1 << 4) - 1) * (secondLargestVal / 100f));
        //DRCompat.Log("Serialized the largest weight to " + largestWeight + " And the second largest to: " + secondLargestWeight);
        byte phonemeWeights = (byte)((largestWeight << 4) | secondLargestWeight);
        writer.Write(phonemeIndexes);
        writer.Write(phonemeWeights);
    }
    public void Deserialize(DeserializeEvent e)
    {
        Deserialize(e.Reader);
    }
    public void Deserialize(DarkRiftReader reader)
    {
        // First byte contains the two phonemes
        // with the highest values
        byte phonemes = reader.ReadByte();
        int highestPhoneme = phonemes >> 4;
        int nextHighestPhoneme = phonemes & ((1 << 4) - 1);
        // Second byte contains the amounts
        byte weights = reader.ReadByte();
        int highestWeight = weights >> 4;
        int nextHighestWeight = weights & ((1 << 4) - 1);
        //DRCompat.Log("Highest phoneme is #" + highestPhoneme + " next highest is " + nextHighestPhoneme + " largest val " + highestWeight + " next val " + nextHighestWeight);
        float highestWeightNormalized = 100f * highestWeight / ((1 << 4) - 1);
        float nextHighestWeightNormalized = 100f * nextHighestWeight / ((1 << 4) - 1);
        // Save the values, and 0 out all others
        Array.Clear(PhonemeWeights, 0, NumPhonemes);
        PhonemeWeights[highestPhoneme] = highestWeightNormalized;
        PhonemeWeights[nextHighestPhoneme] = nextHighestWeightNormalized;
    }
    public void Clear()
    {
        Array.Clear(PhonemeWeights, 0, NumPhonemes);
    }
}
