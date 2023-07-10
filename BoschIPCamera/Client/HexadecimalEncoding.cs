// Copyright 2023 Keyfactor
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Linq;

namespace Keyfactor.Extensions.Orchestrator.BoschIPCamera.Client
{
    public class HexadecimalEncoding
    {

        //for each letter in the string passed in, convert to hex and left pad with 2 zeros. Return combined hex values
        public static string ToHexWithPadding(string myString)
        {
            var padding = "00";
            var returnHex = "";
            var values = myString.ToCharArray();
            foreach (var letter in values)
            {
                // Get the integral value of the character.
                var value = Convert.ToInt32(letter);
                // Convert the integer value to a hexadecimal value in string form.
                var hexValue = padding + value.ToString("X");
                returnHex = returnHex + hexValue;
            }
            return returnHex;
        }

        //for each letter in the string passed in, convert to hex. Return combined hex values
        public static string ToHexNoPadding(string myString)
        {
            var myHex = "";
            var values = myString.ToCharArray();
            foreach (var letter in values)
            {
                // Get the integral value of the character.
                var value = Convert.ToInt32(letter);
                // Convert the integer value to a hexadecimal value in string form.
                var hexValue = value.ToString("X");
                myHex = myHex + hexValue;
            }
            return myHex;
        }

        //take the passed in string's length + 4 --> convert to hex left pad with padChar with provided padValue --> prefix with 0x
        public static string ToHexWithPrefix(string myString, int padValue, char padChar)
        {
            //get the length and add 4
            var stringLength = myString.Length + 4;
            var hexValue = $"{stringLength,1:X}";
            return "0x" + hexValue.PadLeft(padValue, padChar);
        }

        //take the passed in string's length * 2 + 4 --> convert to hex left pad with padChar with provided padValue
        public static string ToHexStringLengthWithPadding(string myString, int padValue, char padChar)
        {
            //get the length of the string and multiply it by 2 and add 4
            var stringLength = myString.Length * 2 + 4;
            var hexValue = $"{stringLength,1:X}";
            return hexValue.PadLeft(padValue, padChar);
        }

        //take the passed in string's length + 4 --> convert to hex left pad with padChar with provided padValue
        public static string ToHex(string myString, int padValue, char padChar)
        {
            //get the length and add 4
            var stringLength = myString.Length + 4;
            var hexValue = $"{stringLength,1:X}";
            return hexValue.PadLeft(padValue, padChar);
        }

        // Split input hex into pairs each representing one byte, convert that into a character and concatenate.
        public static string FromHex(string hex)
        {
            return new String(
                Enumerable.Range(0, hex.Length / 2)
                .Select(i => (char)Convert.ToInt32($"{hex[2 * i]}{hex[2 * i + 1]}",16))
                .ToArray());
        }
    }
}