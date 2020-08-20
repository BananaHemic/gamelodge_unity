using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class ColorUtils
{
    //reference white
    const float refX = 95.047f; // Observer= 2°, Illuminant= D65
    const float refY = 100.000f;
    const float refZ = 108.883f;

    /// <summary>
    /// Takes a float for linear space in the range [0,1]
    /// Return the equivalent gamma value in range [0,1]
    /// </summary>
    /// <param name="linear"></param>
    /// <returns></returns>
    public static float LinearToGammaNormalized(float linear)
    {
        float gamma;
        // linear -> sRGB
        linear = Mathf.Clamp01(linear) * 100f;
        //if (linear > 0.0031308f)
        gamma = 1.055f * Mathf.Pow(linear, 1 / 2.4f) - 0.055f;
        //else
        //gamma = 12.92f * linear;
        // sRGB -> normalized
        return gamma / 7.13263f;
    }

    public static Vector3 RGB2XYZ(Color rgb)
    {
        Vector3 linearRGB = RGB2LINEAR(rgb);

        float red = linearRGB[0];
        float green = linearRGB[1];
        float blue = linearRGB[2];
        float x = (0.4124f * red) + (0.3576f * green) + (0.1805f * blue);
        float y = (0.2126f * red) + (0.7152f * green) + (0.0722f * blue);
        float z = (0.0193f * red) + (0.1192f * green) + (0.9505f * blue);

        return new Vector3(x, y, z);
    }
    public static Vector3 XYZ2RGB_LINEAR(Vector3 xyz)
    {
        float x = xyz[0];
        float y = xyz[1];
        float z = xyz[2];

        float r = x * 3.2406f + y * -1.5372f + z * -0.4986f;
        float g = x * -0.9689f + y * 1.8758f + z * 0.0415f;
        float b = x * 0.0557f + y * -0.2040f + z * 1.0570f;

        return new Vector3(r, g, b);
    }
    // Takes an XYZ from [0,100] and outputs LAB
    public static Vector3 XYZ2LAB(Vector3 xyz)
    {

        //adjust LAB to reference white
        float x = xyz[0] / refX;
        float y = xyz[1] / refY;
        float z = xyz[2] / refZ;

        //LAB Conversion
        if (x > 0.008856f)
            x = Mathf.Pow(x, 1.0f / 3.0f);
        else
            x = (7.787f * x) + (16.0f / 116.0f);

        if (y > 0.008856f)
            y = Mathf.Pow(y, 1.0f / 3.0f);
        else
            y = (7.787f * y) + (16.0f / 116.0f);

        if (z > 0.008856f)
            z = Mathf.Pow(z, 1.0f / 3.0f);
        else
            z = (7.787f * z) + (16.0f / 116.0f);

        //adjust LAB scale
        float cieL = (116.0f * y) - 16.0f;
        float cieA = 500.0f * (x - y);
        float cieB = 200.0f * (y - z);
        return new Vector3(cieL, cieA, cieB);
    }
    // Returns XYZ from [0,100]
    public static Vector3 LAB2XYZ(Vector3 lab)
    {
        float y = (lab[0] + 16) / 116.0f;
        float x = lab[1] / 500.0f + y;
        float z = y - lab[2] / 200.0f;

        x = refX * ((x * x * x > 0.008856f) ? x * x * x : (x - 16.0f / 116.0f) / 7.787f);
        y = refY * ((y * y * y > 0.008856f) ? y * y * y : (y - 16.0f / 116.0f) / 7.787f);
        z = refZ * ((z * z * z > 0.008856f) ? z * z * z : (z - 16.0f / 116.0f) / 7.787f);

        return new Vector3(x, y, z);
    }
    public static Vector3 RGB2LAB(Color rgb)
    {
        return XYZ2LAB(RGB2XYZ(rgb));
    }
    public static Color LAB2RGB(Vector3 lab)
    {
        return LINEAR2RGB(LAB2RGB_LINEAR(lab));
    }
    public static Vector3 LAB2RGB_LINEAR(Vector3 lab)
    {
        return XYZ2RGB_LINEAR(LAB2XYZ(lab));
    }
    public static float GAMMA_2_LINEAR(byte gamma)
    {
        float input = (float)gamma / 255.0f;

        if (input > 0.04045f)
            input = Mathf.Pow((input + 0.055f) / 1.055f, 2.4f);
        else
            input = input / 12.92f;

        return input * 100.0f;
    }
    public static byte LINEAR_2_GAMMA(float linear)
    {
        float gamma = linear / 100.0f;
        gamma = (gamma > 0.0031308f) ? (1.055f * Mathf.Pow(gamma, 1 / 2.4f) - 0.055f) : 12.92f * gamma;

        return (byte)Mathf.Clamp(255.0f * gamma, 0.0f, 255.0f);
    }
    // Returns the un-gamma'd linear rgb between [0,100]
    public static Vector3 RGB2LINEAR(Color rgb)
    {
        float red = (float)(rgb[0] / 255.0f);
        float green = (float)(rgb[1] / 255.0f);
        float blue = (float)(rgb[2] / 255.0f);
        //adjust gamma on image 1
        if (red > 0.04045f)
            red = Mathf.Pow((red + 0.055f) / 1.055f, 2.4f);
        else
            red = red / 12.92f;
        if (green > 0.04045f)
            green = Mathf.Pow((green + 0.055f) / 1.055f, 2.4f);
        else
            green = green / 12.92f;
        if (blue > 0.04045f)
            blue = Mathf.Pow((blue + 0.055f) / 1.055f, 2.4f);
        else
            blue = blue / 12.92f;
        red *= 100.0f;
        green *= 100.0f;
        blue *= 100.0f;
        return new Vector3(red, green, blue);
    }
    //https://github.com/antimatter15/rgb-lab/blob/master/color.js
    // Takes a linear RGB from [0,100] and outputs sRGB [0,1]
    public static Color LINEAR2RGB(Vector3 linear)
    {
        float r = linear[0] / 100.0f;
        float g = linear[1] / 100.0f;
        float b = linear[2] / 100.0f;
        // https://en.wikipedia.org/wiki/SRGB#The_sRGB_transfer_function_(%22gamma%22)
        r = (r > 0.0031308f) ? (1.055f * Mathf.Pow(r, 1 / 2.4f) - 0.055f) : 12.92f * r;
        g = (g > 0.0031308f) ? (1.055f * Mathf.Pow(g, 1 / 2.4f) - 0.055f) : 12.92f * g;
        b = (b > 0.0031308f) ? (1.055f * Mathf.Pow(b, 1 / 2.4f) - 0.055f) : 12.92f * b;

        r = Mathf.Clamp01(r);
        g = Mathf.Clamp01(g);
        b = Mathf.Clamp01(b);

        return new Color(r, g, b);
    }
}