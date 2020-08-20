using System.Collections;
using System.Collections.Generic;
using System;
using System.Text;
#if UNITY
using UnityEngine;
#endif

namespace Miniscript
{
    public struct SourceLine
    {
        private char[] Chars;
        public int StartIdx;
        public int Length;
        public char this[int idx]
        {
            get { return Chars[StartIdx + idx];}
            set { Chars[StartIdx + idx] = value; }
        }

        #region allocator
        [ThreadStatic]
        private static Stack<char[]> _availableCharArrays = new Stack<char[]>(64);
        public static char[] GetCharArray(int minSize)
        {
            if (_availableCharArrays == null)
                _availableCharArrays = new Stack<char[]>(); // Only happens for other threads, ThreadStatic only initializes once
            else if(_availableCharArrays.Count > 0)
            {
                char[] ray = _availableCharArrays.Pop();
                if (ray.Length < minSize)
                    Array.Resize(ref ray, minSize);
                return ray;
            }
            return new char[minSize];
        }
        public static void ReturnCharArray(char[] ray)
        {
            _availableCharArrays.Push(ray);
        }
        #endregion

        public SourceLine(char[] chars)
        {
            Chars = chars;
            StartIdx = 0;
            Length = 0;
        }
        public SourceLine(char[] chars, int startIdx, int len)
        {
            Chars = chars;
            StartIdx = startIdx;
            Length = len;
        }
        public SourceLine(int capacity)
        {
            Chars = new char[capacity];
            StartIdx = 0;
            Length = 0;
        }
        public void Reset()
        {
            StartIdx = 0;
            Length = 0;
        }
        public char[] GetBackingArray()
        {
            return Chars;
        }
        /// <summary>
        /// Returns the char at the specified index
        /// </summary>
        /// <param name="idx"></param>
        /// <returns></returns>
        public char AtIndex(int idx)
        {
            return Chars[StartIdx + idx];
        }
        private static int NextPowerOfTwo(int v)
        {
#if UNITY
            return Mathf.NextPowerOfTwo(v);
#else
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            return v;
#endif
        }
        public void Append(char c)
        {
            int finalLen = Length + 1;
            // Make sure the char array is large enough
            // using PoT resizing
            if(StartIdx + finalLen > Chars.Length)
                Array.Resize(ref Chars, NextPowerOfTwo(StartIdx + finalLen));
            Chars[StartIdx + Length] = c;

            Length = finalLen;
        }
        public void Append(string str)
        {
            int finalLen = Length + str.Length;
            // Make sure the char array is large enough
            // using PoT resizing
            if(StartIdx + finalLen > Chars.Length)
                Array.Resize(ref Chars, NextPowerOfTwo(StartIdx + finalLen));
            // Copy the string into Chars
            str.CopyTo(0, Chars, StartIdx + Length, str.Length);
            Length = finalLen;
        }
        public void Append(ref SourceLine other, int start, int len=-1)
        {
            if (len < 0)
                len = other.Length - start;
            int finalLen = Length + len;
            // Make sure the char array is large enough
            // using PoT resizing
            if(StartIdx + finalLen > Chars.Length)
                Array.Resize(ref Chars, NextPowerOfTwo(StartIdx + finalLen));
            // Copy the other SourceLine into Chars
            Array.Copy(other.Chars, other.StartIdx + start, Chars, StartIdx + Length, len);
            Length = finalLen;
        }
        public void Append(string str, int start, int len=-1)
        {
            if (len < 0)
                len = str.Length - start;
            int finalLen = Length + len;
            // Make sure the char array is large enough
            // using PoT resizing
            if(StartIdx + finalLen > Chars.Length)
                Array.Resize(ref Chars, NextPowerOfTwo(StartIdx + finalLen));
            // Copy the other SourceLine into Chars
            str.CopyTo(start, Chars, StartIdx + Length, len);
            Length = finalLen;
        }
        public void Prepend(char c, int numTimes)
        {
            int finalLen = Length + numTimes;
            int newStartIdx;
            if(StartIdx >= numTimes)
            {
                // If there's already a buffer with enough space
                // Then copy the chars directly onto the beginning
                newStartIdx = StartIdx - numTimes;
                for (int i = newStartIdx; i < numTimes; i++)
                    Chars[i] = c;
            }
            else
            {
                // Make sure the char array is large enough
                // using PoT resizing
                if(finalLen > Chars.Length)
                    Array.Resize(ref Chars, NextPowerOfTwo(finalLen));

                int numShift = numTimes - StartIdx;
                // Shift chars over
                Array.Copy(Chars, StartIdx, Chars, numShift, Length);
                newStartIdx = 0;

                // Add the chars
                for (int i = 0; i < numTimes; i++)
                    Chars[i] = c;
            }
            StartIdx = newStartIdx;
            Length = finalLen;
        }

        public void TrimStart(int numToTrim)
        {
            StartIdx += numToTrim;
            Length -= numToTrim;
        }
        public void TrimEnd(int numToTrim)
        {
            Length -= numToTrim;
        }
        /// <summary>
        /// Pull characters out of the middle of the string
        /// </summary>
        /// <param name="startIdx">Where to start the trim</param>
        /// <param name="endIdx">Where to end the trim</param>
        /// <returns>Updated SourceLine</returns>
        public void TrimMiddle(int startIdx, int endIdx)
        {
            int numToRemove = endIdx - startIdx;
            int finalLength = Length - numToRemove;

            // If we are removing everything, don't bother with a copy
            if (finalLength == 0)
            {
                StartIdx = 0;
                Length = 0;
                return;
            }
            // We either need to move the front characters
            // over to the right, or the end characters to
            // the left. We decide based on which is the smaller
            // copy
            int numAtEnd = Length - endIdx;
            if(startIdx <= numAtEnd)
            {
                // Move the start characters down
                Array.Copy(Chars, StartIdx, Chars, StartIdx + numToRemove, startIdx);
                StartIdx += numToRemove;
                Length = finalLength;
                return;
            }
            else if(numAtEnd == 0)
            {
                // There's no need to copy anything, we can just change the length
                Length = finalLength;
                return;
            }
            else
            {
                // Move the end characters up
                Array.Copy(Chars, StartIdx + endIdx, Chars, StartIdx + startIdx, numAtEnd);
                Length = finalLength;
            }
        }
        /// <summary>
        /// Inserts a string into the middle of this line
        /// and potentially remove existing characters.
        /// Ex: "abcd" InsertMiddle(1,3,"??")
        /// returns "a??d"
        /// </summary>
        /// <param name="startIdx"></param>
        /// <param name="endIdx"></param>
        /// <returns></returns>
        public void InsertMiddle(int startIdx, string str, int endIdx)
        {
            int numToRemove = endIdx - startIdx;
            int finalLen = Length - numToRemove + str.Length;
            // Make sure that the dst is large enough
            if (StartIdx + finalLen > Chars.Length)
                Array.Resize(ref Chars, NextPowerOfTwo(StartIdx + finalLen));

            // Move the end characters to the right as needed
            if(endIdx != Length)
            {
                int endSrc = StartIdx + endIdx;
                int endDst = StartIdx + startIdx + str.Length;
                if (endDst != endSrc)
                    Array.Copy(Chars, endSrc, Chars, endDst, Length - endIdx);
            }
            // Copy in the characters from string
            str.CopyTo(0, Chars, StartIdx + startIdx, str.Length);

            Length = finalLen;
        }
        public static void SubstringExisting(ref SourceLine dst, ref SourceLine src, int start, int len)
        {
            // Make sure that the dst is large enough
            if (len > dst.Chars.Length)
                Array.Resize(ref dst.Chars, NextPowerOfTwo(len));

            Array.Copy(src.Chars, src.StartIdx + start, dst.Chars, 0, len);
            dst.StartIdx = 0;
            dst.Length = len;
        }
        public static void MergeSourceLines(ref SourceLine dst, ref SourceLine lineA, int startA, int lenA, ref SourceLine lineB, int startB, int lenB)
        {
            int finalLen = lenA + lenB;
            // Make sure that the dst is large enough
            if (finalLen > dst.Chars.Length)
                Array.Resize(ref dst.Chars, NextPowerOfTwo(finalLen));

            // Copy the first part from lineA
            Array.Copy(lineA.Chars, lineA.StartIdx + startA, dst.Chars, 0, lenA);
            // Copy the second part from lineB
            Array.Copy(lineB.Chars, lineB.StartIdx + startB, dst.Chars, lenA, lenB);

            dst.StartIdx = 0;
            dst.Length = finalLen;
        }
        private static char Num2Char(int num)
        {
            // TODO we can do a clever addition + cast here
            switch (num)
            {
                case 0:
                    return '0';
                case 1:
                    return '1';
                case 2:
                    return '2';
                case 3:
                    return '3';
                case 4:
                    return '4';
                case 5:
                    return '5';
                case 6:
                    return '6';
                case 7:
                    return '7';
                case 8:
                    return '8';
                case 9:
                    return '9';
            }
            return '?';
        }
        public void AppendNumber(int num)
        {
            bool isNegative = num < 0;
            if (isNegative)
                num = -num;
            int numChars = num == 0 ? 1 : 1 + (int)Math.Log10(num);
            int finalLen = Length + numChars;
            if (isNegative)
                finalLen++;
            // Make sure that the dst is large enough
            if (StartIdx + finalLen > Chars.Length)
                Array.Resize(ref Chars, NextPowerOfTwo(StartIdx + finalLen));

            int s = (int)Math.Pow(10, numChars - 1);
            int idx = StartIdx + Length;
            char c;
            int rem = num;
            if (isNegative)
                Chars[idx++] = '-';

            while(s >= 10)
            {
                int n = rem / s;
                rem = rem % s;
                c = Num2Char(n);
                Chars[idx++] = c;
                s = s / 10;
            }

            rem = num % 10;
            c = Num2Char(rem);
            Chars[idx++] = c;

            Length = finalLen;
        }
        public string GetString(int start, int len)
        {
            return new string(Chars, StartIdx + start, len);
        }
        public string GetString(int start)
        {
            return new string(Chars, StartIdx + start, Length - start);
        }
        public string GetString()
        {
            return new string(Chars, StartIdx, Length);
        }
        public void AppendToStringBuilder(StringBuilder sb, int start, int len)
        {
            sb.Append(Chars, StartIdx + start, len);
        }
        public void AppendToStringBuilder(StringBuilder sb, int start)
        {
            sb.Append(Chars, StartIdx + start, Length - start);
        }
        public void AppendToStringBuilder(StringBuilder sb)
        {
            sb.Append(Chars, StartIdx, Length);
        }
    }
}
