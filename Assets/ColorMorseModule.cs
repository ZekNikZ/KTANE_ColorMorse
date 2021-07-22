using ColorMorse;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using KModkit;
using UnityEngine;
using Rnd = UnityEngine.Random;

public class ColorMorseModule : MonoBehaviour
{
    public KMBombInfo BombInfo;
    public KMBombModule BombModule;
    public KMAudio Audio;
    public KMRuleSeedable RuleSeedable;

    public MeshRenderer[] IndicatorMeshes;
    public Material[] ColorMaterials;
    public string[] ColorNames;
    public Material BlackMaterial;
    public TextMesh[] Labels;
    public KMSelectable[] Buttons;
    public TextMesh SolutionScreenText;

    private int[] Colors;
    private int[] Numbers;
    private double[] SolNumbers;
    private bool[][] Flashes;
    private int[] Pointers;
    private int[] Operators;
    private string Solution;
    private string SubmittedSolution = "";

    private int ledCount;
    private bool flashingEnabled = false;
    private int PAREN_POS;
    private int dayStarted;
    private int monthStarted;
    private int yearStarted;

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private bool _isSolved;

    private const string SYMBOLS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static readonly string[] MORSE_SYMBOLS = {
        "-----", ".----", "..---", "...--", "....-",
        ".....", "-....", "--...", "---..", "----.",
        ".-", "-...", "-.-.", "-..",  ".",   "..-.", "--.", "....", "..",   ".---", "-.-",  ".-..", "--",
        "-.", "---",  ".--.", "--.-", ".-.", "...",  "-",   "..-",  "...-", ".--",  "-..-", "-.--", "--.."
    };
    private const string OPERATION_SYMBOLS = "+-×/";

    private static readonly int[] brailleCount =
    {
        3, 1, 2, 2, 3, 2, 3, 4, 3, 2,
        1, 2, 2, 3, 2, 3, 4, 3, 2, 3,
        2, 3, 3, 4, 3, 4, 5, 4, 3, 4,
        3, 4, 4, 4, 5, 4
    };

    private static readonly int[] maritimeCount =
    {
        6, 3, 3, 3, 5, 5, 7, 3, 3, 3,
        2, 1, 5, 3, 2, 5, 6, 2, 2, 3,
        2, 4, 5, 16, 2, 2, 1, 5, 2, 3,
        4, 5, 3, 5, 10, 4
    };

    private static readonly int[] fourteenSegmentCount =
    {
        8, 3, 6, 6, 5, 5, 3, 8, 8, 7,
        7, 7, 4, 6, 6, 5, 6, 6, 4, 4,
        5, 3, 6, 6, 6, 6, 7, 7, 6, 3,
        5, 4, 6, 4, 3, 4
    };

    private static readonly int[] zoniCount =
    {
        1, 2, 3, 4, 4, 5, 5, 6, 6, 7,
        4, 4, 2, 3, 4, 3, 5, 5, 2, 3,
        4, 4, 4, 4, 1, 4, 2, 4, 2, 4,
        4, 4, 4, 5, 2, 4
    };

    private static readonly int[] pigpenCount =
    {
        0, 0, 0, 0, 0, 0, 0, 0, 0, 0,   // digits — not used
        6, 9, 6, 9, 12, 9, 6, 9, 6, 7, 10, 7, 10, 13, 10, 7, 10, 7, 6, 6, 6, 6, 7, 7, 7, 7  // letters
    };

    // Base-36 values that are still valid Morse digits/letters when reversed
    private static readonly int[] validReverseMorse =
    {
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
        10, 11, 13, 14, 15, 16, 17, 18,
        20, 21, 22, 23, 24, 25, 26, 27,
        28, 29, 30, 31, 32, 33, 34
    };

    private static readonly int[] changedReverseMorse =
    {
        0, 9, 8, 7, 6, 5, 4, 3, 2, 1,
        23, 31, 0, 30, 14, 21, 33, 17, 18, 0,
        20, 15, 22, 10, 24, 25, 34, 27,
        28, 29, 13, 11, 16, 33, 26, 0
    };

    private static readonly int[] scrabbleScore =
    {
        0, 1, 2, 3, 4, 5, 6, 7, 8, 9,   // digits
        1, 3, 3, 2, 1, 4, 2, 4, 1, 8, 5, 1, 3, 1, 1, 3, 10, 1, 1, 1, 1, 4, 4, 8, 4, 10  // letters
    };

    private static readonly int[] extendedTapCode =
    {
        65, 16, 26, 36, 46, 56, 61, 62, 63, 64, // digits
        11, 12, 13, 14, 15, 21, 22, 23, 24, 25, 66, 31, 32, 33, 34, 35, 41, 42, 43, 44, 45, 51, 52, 53, 54, 55  // letters
    };

    private static readonly string[] colorNames = { "RED", "ORANGE", "YELLOW", "GREEN", "BLUE", "PURPLE", "WHITE" };


    // SEED 1 DEFAULT RULES
    private static readonly Func<int, ColorMorseModule, double>[] _seed1Rules = Ut.NewArray<Func<int, ColorMorseModule, double>>(
        // If the number is odd, double it. Otherwise, halve it.
        (n, module) => n % 2 == 1 ? n * 2 : n / 2,

        // If the number is divisible by 3, divide by 3. Otherwise, add the number of lights that flash either red, yellow, or blue.
        (n, module) => n % 3 == 0 ? n / 3 : n + module.Colors.Count(x => x == 0 || x == 2 || x == 4),

        // Square the number.
        (n, module) => n * n,

        // Swap the position of the parentheses to be around the 2nd and 3rd light if they are around the 1st and 2nd light, or vice versa.
        (n, module) =>
        {
            module.PAREN_POS = (module.PAREN_POS + 1) % 2;
            return n;
        },

        // Triple the number and take the digital root until the number is a single digit.
        (n, module) => (n * 3 - 1) % 9 + 1,

        // Subtract the number from 10.
        (n, module) => 10 - n);


    // RULE-SEEDED RULES
    private static readonly Func<ColorMorseModule, MonoRandom, Func<int, double>>[] _rules = Ut.NewArray<Func<ColorMorseModule, MonoRandom, Func<int, double>>>(
        // If the number is divisible by <m>, divide by it; otherwise, multiply it.
        (module, rnd) =>
        {
            var m = rnd.Next(2, 6);
            return n => n % m == 0 ? n / m : n * m;
        },

        // If the number is divisible by <m>, divide by it. Otherwise, add the number of lights that flash either <c1>, <c2>, or <c3>.
        (module, rnd) =>
        {
            var multiplier = rnd.Next(2, 6);
            var colors = rnd.ShuffleFisherYates(Enumerable.Range(0, 7).ToArray()).Subarray(0, 3);
            return m => m % multiplier == 0 ? m / multiplier : m + module.Colors.Count(colors.Contains);
        },

        // Square the number.
        (module, rnd) => n => n * n,

        // Swap the position of the parentheses to be around the 2nd and 3rd light if they are around the 1st and 2nd light, or vice versa.
        (module, rnd) => n =>
        {
            module.PAREN_POS = (module.PAREN_POS + 1) % 2;
            return n;
        },

        // Replace <+’s with −’s (and vice versa) and ×’s with /’s (and vice versa)|+’s with ×’s (and vice versa) and −’s with /’s (and vice versa)>.
        (module, rnd) =>
        {
            var which = rnd.Next(1, 3);
            return n =>
            {
                for (var i = 0; i < module.Operators.Length; i++)
                    module.Operators[i] ^= which;
                return n;
            };
        },

        // Multiply the number by <m> and take the digital root.
        (module, rnd) =>
        {
            var m = rnd.Next(1, 9);
            return n => (n * m - 1) % 9 + 1;
        },

        // Subtract the number from <m>.
        (module, rnd) =>
        {
            var m = rnd.Next(10, 36);
            return n => m - n;
        },

        // Multiply the displayed number by n+1, where n is equal to the number of ports that contain this letter or number.
        (module, rnd) =>
        {
            var ports = module.BombInfo.GetPorts().Select(p => p.ToUpperInvariant()).ToArray();
            return n => n * (1 + ports.Count(p => p.Contains(SYMBOLS[n])));
        },

        // Take the displayed number modulo <m>, and use its factorial.
        (module, rnd) =>
        {
            var m = rnd.Next(5, 11);
            var factorials = new int[m];
            factorials[0] = 1;
            for (var i = 1; i < m; i++)
                factorials[i] = i * factorials[i - 1];
            return n => factorials[n % m];
        },

        // Replace the displayed number by the number of <lit|unlit|> indicators that contain this character.
        (module, rnd) =>
        {
            var indMode = rnd.Next(0, 3);   // 0 = unlit only; 1 = lit only; 2 = both
            var eligibleIndicators = (
                indMode == 0 ? module.BombInfo.GetOffIndicators() :
                indMode == 1 ? module.BombInfo.GetOnIndicators() : module.BombInfo.GetIndicators()).ToArray();
            return n => eligibleIndicators.Count(ind => ind.Contains(SYMBOLS[n]));
        },

        // Multiply the displayed number by n, where n is the number of distinct serial number characters.
        (module, rnd) =>
        {
            var numUniqueChars = module.BombInfo.GetSerialNumber().Distinct().Count();
            return n => n * numUniqueChars;
        },

        // Replace the displayed number with the number of 1’s that appear in this number’s binary conversion.
        (module, rnd) => n =>
        {
            var numBits = 0;
            while (n > 0)
            {
                numBits++;
                n &= n - 1;
            }
            return numBits;
        },

        // Replace the displayed number with the number of <1’s|2’s|1’s and 2’s> that appear in this number’s ternary conversion.
        (module, rnd) =>
        {
            var whichDigits = rnd.Next(0, 3);   // 0 = both; 1 = 1’s, 2 = 2’s
            return n =>
            {
                var numDigits = 0;
                while (n > 0)
                {
                    if ((whichDigits == 0 && (n % 3 != 0)) ||
                        (whichDigits != 0 && (n % 3 == whichDigits)))
                        numDigits++;
                    n /= 3;
                }
                return numDigits;
            };
        },

        // <Multiply the displayed number by 1 +|Add> the number of <port plates|distinct port types|battery holders> on the bomb.
        (module, rnd) =>
        {
            var op = rnd.Next(0, 2);    // 0 = multiply; 1 = add
            var whichEdgework = rnd.Next(0, 3);
            var value =
                whichEdgework == 0 ? module.BombInfo.GetPortPlateCount() :
                whichEdgework == 1 ? module.BombInfo.CountUniquePorts() : module.BombInfo.GetBatteryHolderCount();
            return n => (op == 1) ? n + value : n * (1 + value);
        },

        // Replace the displayed number with the number of dots that appear in this characters’s braille conversion. (1..9 = A..H, 0 = J)
        (module, rnd) => n => brailleCount[n],

        // Replace the displayed number with the number of colored regions within the maritime signalling flag for the character.
        (module, rnd) => n => maritimeCount[n],

        // Replace the displayed number with the number of shapes that appear in the character’s Zoni symbol.
        (module, rnd) => n => zoniCount[n],

        // Replace the displayed number with d + 3×s, where d is the number of dots and s the number of dashes in the character’s Morse code representation.
        (module, rnd) => n =>
        {
            int dotCount = MORSE_SYMBOLS[n].Count(f => f == '.');
            int dashCount = 3 * MORSE_SYMBOLS[n].Count(f => f == '-');
            return dotCount + dashCount;
        },

        // Replace the displayed number with the number of segments that would be <on|off> in the 14-segment display for the character.
        (module, rnd) =>
        {
            var useOn = rnd.Next(0, 2) != 0;
            return n => useOn ? fourteenSegmentCount[n] : 14 - fourteenSegmentCount[n];
        },

        // <Append|Prepend> the <3rd|6th> character of the serial number to the displayed number.
        (module, rnd) =>
        {
            var append = rnd.Next(0, 2) != 0;
            var sn = module.BombInfo.GetSerialNumber();
            var snValue = sn[rnd.Next(0, 2) != 0 ? 2 : 5] - '0';
            return n => double.Parse(string.Format(append ? "{0}{1}" : "{1}{0}", n, snValue));
        },

        // Add the displayed number to the total number of letters across all displayed colors.
        (module, rnd) => n => n + module.Colors.Sum(c => colorNames[c].Length),

        // If the displayed character appears in the serial number, divide it by t+1,
        // where t is the number of times it appears in the serial number. Otherwise, multiply by <m>.
        (module, rnd) =>
        {
            var sn = module.BombInfo.GetSerialNumber();
            var m = rnd.Next(2, 8);
            return n =>
            {
                int serialNumberCount = sn.Count(ch => ch == SYMBOLS[n]);
                return serialNumberCount == 0 ? n * m : (double) n / (serialNumberCount + 1);
            };
        },

        // If the displayed character in Morse Code reversed is a number from 0-9 or a letter from A-Z, use that number instead.
        // Otherwise, use the displayed number plus the number of <ports|port plates|batteries|battery holders|indicators|lit indicators|unlit indicators>.
        (module, rnd) =>
        {
            var which = rnd.Next(0, 7);
            var value =
                which == 0 ? module.BombInfo.GetPortCount() :
                which == 1 ? module.BombInfo.GetPortPlateCount() :
                which == 2 ? module.BombInfo.GetBatteryCount() :
                which == 3 ? module.BombInfo.GetBatteryHolderCount() :
                which == 4 ? module.BombInfo.GetIndicators().Count() :
                which == 5 ? module.BombInfo.GetOnIndicators().Count() : module.BombInfo.GetOffIndicators().Count();
            return n => validReverseMorse.Contains(n) ? changedReverseMorse[n] : n + value;
        },

        // If the displayed character is a digit, add the number of <ports|port plates|batteries|battery holders|indicators|lit indicators|unlit indicators>.
        // Otherwise, replace the number with d + 3×s, where d is the number of dots and s the number of lines in the Pigpen representation of the letter.
        (module, rnd) =>
        {
            var which = rnd.Next(0, 7);
            var value =
                which == 0 ? module.BombInfo.GetPortCount() :
                which == 1 ? module.BombInfo.GetPortPlateCount() :
                which == 2 ? module.BombInfo.GetBatteryCount() :
                which == 3 ? module.BombInfo.GetBatteryHolderCount() :
                which == 4 ? module.BombInfo.GetIndicators().Count() :
                which == 5 ? module.BombInfo.GetOnIndicators().Count() : module.BombInfo.GetOffIndicators().Count();
            return n => n < 10 ? n + value : pigpenCount[n];
        },

        // Multiply the displayed number by the number of <digits|letters> in the serial number.
        (module, rnd) =>
        {
            var letters = rnd.Next(0, 2) != 0;
            var value = letters ? module.BombInfo.GetSerialNumberLetters().Count() : module.BombInfo.GetSerialNumberNumbers().Count();
            return n => n * value;
        },

        // Add the last digit of the rule seed.
        (module, rnd) =>
        {
            var value = rnd.Seed % 10;
            return n => n + value;
        },

        // Replace the value with 1 + the number of times the character is contained in the names of the colors of all LEDs.
        (module, rnd) => n => 1 + module.Colors.Sum(c => colorNames[c].Count(ch => ch == SYMBOLS[n])),

        // Add the current <day|month|year> when the bomb activated.
        (module, rnd) =>
        {
            var which = rnd.Next(0, 3);
            return n => n + (which == 0 ? module.dayStarted : which == 1 ? module.monthStarted : module.yearStarted);
        },

        // Replace the value with the index of the character’s first occurrence within the text “0 THE 1 QUICK 2 BROWN 3 FOX 4 JUMPS 5 OVER 6 THE 7 LAZY 8 DOG 9” (not counting spaces).
        (module, rnd) => n => "0THE1QUICK2BROWN3FOX4JUMPS5OVER6THE7LAZY8DOG9".IndexOf(SYMBOLS[n]) + 1,

        // If it’s a letter, replace it with its score in English Scrabble.
        (module, rnd) => n => scrabbleScore[n],

        // Replace the displayed value with the extended tap code for the character, read as a two-digit number.
        (module, rnd) => n => extendedTapCode[n],

        // Replace the displayed value with the number of modules on the bomb whose name contains the character.
        (module, rnd) =>
        {
            var modules = module.BombInfo.GetSolvableModuleNames().Select(s => s.ToUpperInvariant()).ToArray();
            return n => modules.Count(m => m.Contains(SYMBOLS[n]));
        }
     );

    void Start()
    {
        _moduleId = _moduleIdCounter++;

        var now = DateTime.Now;
        dayStarted = now.Day;
        monthStarted = now.Month;
        yearStarted = now.Year;

        // RULE SEED

        var rnd = RuleSeedable.GetRNG();
        var rules = rnd.Seed == 1
            ? _seed1Rules.Select(rule => new Func<int, double>(n => rule(n, this))).ToArray()
            : rnd.ShuffleFisherYates(_rules.Select(rule => rule(this, rnd)).ToArray()).Subarray(0, 6);

        Debug.LogFormat("[Color Morse #{0}] Using ruleseed: {1}", _moduleId, rnd.Seed);

        // END RULE SEED

        ledCount = IndicatorMeshes.Length;
        Colors = new int[ledCount];
        Numbers = new int[ledCount];
        Flashes = new bool[ledCount][];
        Pointers = new int[ledCount];
        Operators = new int[2];
        SolNumbers = new double[ledCount];

        for (int i = 0; i < ledCount; i++)
            Buttons[i].OnInteract += HandlePress(i);

        reset:
        var origParenPos = PAREN_POS = Rnd.Range(0, 2);
        Operators[0] = Rnd.Range(0, 4);
        Operators[1] = Rnd.Range(0, 4);
        var origOperators = Operators.ToArray();

        for (int i = 0; i < ledCount; i++)
        {
            Colors[i] = Rnd.Range(0, 7);
            Numbers[i] = Rnd.Range(1, 36);
            Flashes[i] = MorseToBoolArray(MORSE_SYMBOLS[Numbers[i]]);
        }

        // This may change the values of PAREN_POS or Operators (e.g. GREEN in Rule Seed 1)
        int solutionNum;
        for (int i = 0; i < ledCount; i++)
            // white does nothing
            SolNumbers[i] = Colors[i] == 6 ? Numbers[i] : rules[Colors[i]](Numbers[i]);

        Solution = "";
        double sign, sol;
        try
        {
            // Special case: if the formula is A / (B / C) the calculation may run into float-point rounding errors.
            // For this reason, calculate the result as A * C / B instead.
            if (PAREN_POS == 1 && Operators[0] == 3 && Operators[1] == 3)
                sol = SolNumbers[0] * SolNumbers[2] / SolNumbers[1];
            else
                sol = PAREN_POS == 0
                    ? Op(Operators[1], Op(Operators[0], SolNumbers[0], SolNumbers[1]), SolNumbers[2])
                    : Op(Operators[0], SolNumbers[0], Op(Operators[1], SolNumbers[1], SolNumbers[2]));
        }
        catch (DivideByZeroException)
        {
            goto reset;
        }
        if (double.IsInfinity(sol) || double.IsNaN(sol))
            goto reset;
        sign = Math.Sign(sol);
        solutionNum = (int) Math.Abs(sol);

        if (sign == -1 && solutionNum != 0) Solution = "-....- ";

        foreach (char c in (solutionNum % 1000).ToString())
            Solution += MORSE_SYMBOLS[SYMBOLS.IndexOf(c)] + " ";
        Solution = Solution.Trim();

        if (origParenPos == 0)
        {
            Labels[0].text = "(";
            Labels[3].gameObject.SetActive(false);
            Labels[1].text = "" + OPERATION_SYMBOLS[origOperators[0]];
            Labels[2].text = ")" + OPERATION_SYMBOLS[origOperators[1]];
        }
        else
        {
            Labels[0].gameObject.SetActive(false);
            Labels[3].text = ")";
            Labels[1].text = OPERATION_SYMBOLS[origOperators[0]] + "(";
            Labels[2].text = "" + OPERATION_SYMBOLS[origOperators[1]];
        }

        Debug.LogFormat("[Color Morse #{15}] Equation shown on module: {11}{0} {2} ({1}) {3} {12}{4} {6} ({5}){13} {7} {8} {10} ({9}){14}",
            ColorNames[Colors[0]], Numbers[0], SYMBOLS[Numbers[0]],
            OPERATION_SYMBOLS[origOperators[0]],
            ColorNames[Colors[1]], Numbers[1], SYMBOLS[Numbers[1]],
            OPERATION_SYMBOLS[origOperators[1]],
            ColorNames[Colors[2]], Numbers[2], SYMBOLS[Numbers[2]],
            origParenPos == 0 ? "(" : "",
            origParenPos == 1 ? "(" : "",
            origParenPos == 0 ? ")" : "",
            origParenPos == 1 ? ")" : "",
            _moduleId);

        Debug.LogFormat("[Color Morse #{11}] Transformed equation: {7}{0} {1} {8}{2}{9} {3} {4}{10}. Solution: {5}{6} ({12})",
            SolNumbers[0], OPERATION_SYMBOLS[Operators[0]],
            SolNumbers[1], OPERATION_SYMBOLS[Operators[1]],
            SolNumbers[2],
            sign == -1 ? "-" : "", solutionNum % 1000,
            PAREN_POS == 0 ? "(" : "",
            PAREN_POS == 1 ? "(" : "",
            PAREN_POS == 0 ? ")" : "",
            PAREN_POS == 1 ? ")" : "",
            _moduleId, Solution);

        BombModule.OnActivate += Activate;
    }

    void Activate()
    {
        flashingEnabled = true;
    }

    private KMSelectable.OnInteractHandler HandlePress(int button)
    {
        return delegate
        {
            Audio.PlaySoundAtTransform("ColorMorseButtonPress", transform);
            Buttons[button].AddInteractionPunch();
            if (_isSolved)
                return false;
            char nextChar;
            switch (button)
            {
                case 0:
                    nextChar = '.';
                    break;
                case 1:
                    nextChar = '-';
                    break;
                case 2:
                    nextChar = ' ';
                    break;
                default:
                    nextChar = '.';
                    break;
            }
            if (Solution.StartsWith(SubmittedSolution + nextChar))
            {
                SubmittedSolution += nextChar;
                if (nextChar == ' ')
                    Debug.LogFormat("[Color Morse #{0}] Character submitted correctly. Current Submission: “{1}”", _moduleId, SubmittedSolution);
                if (Solution.Length == SubmittedSolution.Length)
                {
                    BombModule.HandlePass();
                    _isSolved = true;
                    Debug.LogFormat("[Color Morse #{0}] Full solution submitted.", _moduleId);
                    flashingEnabled = false;
                    for (int i = 0; i < ledCount; i++)
                        IndicatorMeshes[i].sharedMaterial = BlackMaterial;
                    SolutionScreenText.text = Solution.Replace('.', '•');
                    return false;
                }
            }
            else
            {
                BombModule.HandleStrike();
                Debug.LogFormat("[Color Morse #{0}] Submitted “{1}” incorrectly. Current submission: “{2}”. Resetting submission.", _moduleId, nextChar, SubmittedSolution);
                SubmittedSolution = "";
            }
            SolutionScreenText.text = SubmittedSolution.Replace('.', '•') + "<color=#8f8>|</color>";
            return false;
        };
    }

    private const float DOT_LENGTH = 0.2f;
    private float timer = DOT_LENGTH;

    void Update()
    {
        if (flashingEnabled)
        {
            timer -= Time.deltaTime;
            if (timer < 0)
            {
                for (int i = 0; i < ledCount; i++)
                {
                    IndicatorMeshes[i].sharedMaterial = Flashes[i][Pointers[i]] ? ColorMaterials[Colors[i]] : BlackMaterial;
                    Pointers[i] = (Pointers[i] + 1) % Flashes[i].Length;
                }
                timer = DOT_LENGTH;
            }
        }
    }

    bool[] MorseToBoolArray(string morse)
    {
        int length = 0;
        foreach (char c in morse)
        {
            if (c == '.')
            {
                length += 2;
            }
            else
            {
                length += 4;
            }
        }
        bool[] result = new bool[length + 4];
        int pointer = 0;
        foreach (char c in morse)
        {
            if (c == '.')
            {
                result[pointer] = true;
                pointer += 2;
            }
            else
            {
                result[pointer] = true;
                result[pointer + 1] = true;
                result[pointer + 2] = true;
                pointer += 4;
            }
        }
        return result;
    }

    double Op(int op, double x, double y)
    {
        switch (op)
        {
            case 0:
                return x + y;
            case 1:
                return x - y;
            case 2:
                return x * y;
            case 3:
                return x / y;
            default:
                return 0;
        }
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = "!{0} transmit ....- --...";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        var commands = command.ToLowerInvariant().Trim().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (commands.Length < 2 || (commands[0] != "transmit" && commands[0] != "submit" && commands[0] != "trans" && commands[0] != "tx" && commands[0] != "xmit"))
            yield break;

        var buttons = new List<KMSelectable>();
        foreach (string morse in commands.Skip(1))
        {
            foreach (char character in morse)
            {
                switch (character)
                {
                    case '.':
                        buttons.Add(Buttons[0]);
                        break;
                    case '-':
                        buttons.Add(Buttons[1]);
                        break;
                    default:
                        yield break;
                }
            }
            buttons.Add(Buttons[2]);
        }

        yield return null;
        yield return buttons;
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        char[] btnTexts = { '.', '-', ' ' };
        while (SubmittedSolution.Length < Solution.Length)
        {
            Buttons[Array.IndexOf(btnTexts, Solution[SubmittedSolution.Length])].OnInteract();
            yield return new WaitForSeconds(.1f);
        }
    }
}
