
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using Rnd = UnityEngine.Random;
using System.Text;

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

    private List<Func<double, double>> rules;

    private int[] Colors;
    private int[] Numbers;
    private double[] SolNumbers;
    private bool[][] Flashes;
    private int[] Pointers;
    private int[] Operators;
    private string Solution;
    private string SubmittedSolution = "";

    private int indicatorCount;
    private bool flashingEnabled = false;
    private int PAREN_POS;

    private static int _moduleIdCounter = 1;
    private int _moduleId;
    private bool _isSolved;

    private const string SYMBOLS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private readonly string[] MORSE_SYMBOLS = {
        "-----", ".----", "..---", "...--", "....-",
        ".....", "-....", "--...", "---..", "----.",
        ".-", "-...", "-.-.", "-..",  ".",   "..-.", "--.", "....", "..",   ".---", "-.-",  ".-..", "--",
        "-.", "---",  ".--.", "--.-", ".-.", "...",  "-",   "..-",  "...-", ".--",  "-..-", "-.--", "--.."
    };
    private const string OPERATION_SYMBOLS = "+-×/";

    void Start()
    {
        _moduleId = _moduleIdCounter++;

        // RULE SEED

        var rnd = RuleSeedable.GetRNG();

        List<string> portNames = new List<string>();

        int parallelPortCount = BombInfo.GetPortCount(Port.Parallel);
        int serialPortCount = BombInfo.GetPortCount(Port.Serial);
        int rjPortCount = BombInfo.GetPortCount(Port.RJ45);
        int dviPortCount = BombInfo.GetPortCount(Port.DVI);
        int rcaPortCount = BombInfo.GetPortCount(Port.StereoRCA);
        int ps2PortCount = BombInfo.GetPortCount(Port.PS2);

        for (int i = 0; i < parallelPortCount; i++)
            portNames.Add("PARLE");
        for (int i = 0; i < serialPortCount; i++)
            portNames.Add("SERIAL");
        for (int i = 0; i < rjPortCount; i++)
            portNames.Add("RJ45");
        for (int i = 0; i < dviPortCount; i++)
            portNames.Add("DVI");
        for (int i = 0; i < rcaPortCount; i++)
            portNames.Add("STEROCA");
        for (int i = 0; i < ps2PortCount; i++)
            portNames.Add("PS2");

        string[] indicatorNames = { "SND", "CLR", "CAR", "IND", "FRQ", "SIG", "NSA", "MSA", "TRN", "BOB", "FRK" };

        int[] binaryCount = {
            0, 1, 1, 2, 1, 2, 2, 4, 1, 2,
            2, 3, 2, 3, 3, 4, 1, 2, 2, 3,
            2, 3, 3, 4, 2, 3, 3, 4, 3, 4,
            4, 5, 1, 2, 2, 3
        };

        int[] brailleCount = {
            3, 1, 2, 2, 3, 2, 3, 4, 3, 2,
            1, 2, 2, 3, 2, 3, 4, 3, 2, 3,
            2, 3, 3, 4, 3, 4, 5, 4, 3, 4,
            3, 4, 4, 4, 5, 4
        };

        int[] maritimeCount = {
            6, 3, 3, 3, 5, 5, 7, 3, 3, 3,
            2, 1, 5, 3, 2, 5, 6, 2, 2, 3,
            2, 4, 5, 16, 2, 2, 1, 5, 2, 3,
            4, 5, 3, 5, 10, 4
        };

        int[] fourteenCount = {
            8, 3, 6, 6, 5, 5, 3, 8, 8, 7,
            7, 7, 4, 6, 6, 5, 6, 6, 4, 4,
            5, 3, 6, 6, 6, 6, 7, 7, 6, 3,
            5, 4, 6, 4, 3, 4
        };

        int[] zoniCount =
        {
            1, 2, 3, 4, 4, 5, 5, 6, 6, 7,
            4, 4, 2, 3, 4, 3, 5, 5, 2, 3,
            4, 4, 4, 4, 1, 4, 2, 4, 2, 4,
            4, 4, 4, 5, 2, 4
        };

        int[] pigpenCount =
        {
            1, 1, 2, 3, 4, 5, 6, 7, 8, 9,
            1, 2, 3, 4, 5, 6, 7, 8, 9, 1,
            2, 3, 4, 5, 6, 7, 8, 9, 1, 2,
            3, 4, 1, 2, 3, 4
        };

        int[] validReverseMorse =
        {
            0, 1, 2, 3, 4, 5, 6, 7, 8, 9,
            10, 11, 13, 14, 15, 16, 17, 18,
            20, 21, 22, 23, 24, 25, 26, 27,
            28, 29, 30, 31, 32, 33, 34
        };

        int[] changedReverseMorse =
        {
            0, 9, 8, 7, 6, 5, 4, 3, 2, 1,
            23, 31, 0, 30, 14, 21, 33, 17, 18, 0,
            20, 15, 22, 10, 24, 25, 34, 27,
            28, 29, 13, 11, 16, 33, 26, 0
        };

        int[] colorNamesLength =
        {
            3, 6, 6, 5, 4, 6, 5
        };

        bool canParenthesisChange = true;

        List<string> presentLitInds = new List<string>();
        List<string> presentUnitInds = new List<string>();

        for (int i = 0; i < indicatorNames.Length; i++)
        {
            if (BombInfo.IsIndicatorOn(indicatorNames[i]))
                presentLitInds.Add(indicatorNames[i]);
            else if (BombInfo.IsIndicatorOff(indicatorNames[i]))
                presentUnitInds.Add(indicatorNames[i]);
        }

        string serialNumber = BombInfo.GetSerialNumber();
        List<string> snChars = new List<string>();
        for (int i = 0; i < 6; i++)
        {
            if (!snChars.Contains(serialNumber.Substring(i, 1)))
            {
                snChars.Add(serialNumber.Substring(i, 1));
            }
        }

        rules = new List<Func<double, double>>();

        // 	If the number is odd, double it. Otherwise, halve it.
        rules.Add(number => number % 2 == 1 ? number * 2 : number / 2);

        // If the number is divisible by 3, divide by 3. Otherwise, add the number of lights that flash either red, yellow, or blue.
        rules.Add(number => number % 3 == 0 ? number / 3 : number + Colors.Count(x => x == 0 || x == 2 || x == 4));

        // Square the number.
        rules.Add(number => number * number);

        // Swap the position of the parentheses to be around the 2nd and 3rd light if they are around the 1st and 2nd light, or vice versa.
        rules.Add(number =>
        {
            if (canParenthesisChange)
                PAREN_POS = (PAREN_POS + 1) % 2;

            return number;

        });

        // Triple the number and take the digital root until the number is a single digit.
        rules.Add(number =>
        {
            var num = number * 3;
            while (num > 9)
            {
                num = num.ToString().ToCharArray().Sum(x => x - '0');
            }
            return num;
        });

        // Subtract the number from 10.
        rules.Add(number => 10 - number);

        // Multiply the displayed number by n+1, where n is equal to the number of vanilla ports that contain this letter or number.
        rules.Add(number =>
        {
            var portEqualsDisplay = 1;
            if (BombInfo.GetPortCount() != 0)
                for (int i = 0; i < portNames.Count(); i++)
                {
                    if (portNames[i].Contains(SYMBOLS.Substring((int)number, 1)))
                    {
                        portEqualsDisplay++;
                    }
                }
            return number * portEqualsDisplay;
        });

        // Take the displayed number modulo 10, and use its factorial.
        rules.Add(number =>
        {
            int factorial = (int)number % 10;
            for (int i = factorial - 1; i > 0; i--)
            {
                factorial *= i;
            }
            if (factorial == 0)
                return 1;
            else
                return factorial;
        });

        // Multiply the displayed number by itself n+1 times, where n is equal to the number of lit indicators that contain this letter.
        rules.Add(number =>
        {
            var equalsLitInd = 1;
            for (int i = 0; i < presentLitInds.Count(); i++)
            {
                if (presentLitInds[i].Contains(SYMBOLS.Substring((int)number, 1)))
                {
                    equalsLitInd++;
                }
            }
            return (int)Math.Pow(number, equalsLitInd);
        });

        // Multiply the displayed number by itself n+1 times, where n is equal to the number of unlit indicators that contain this letter.
        rules.Add(number =>
        {
            var equalsUnlitInd = 1;
            for (int i = 0; i < presentLitInds.Count(); i++)
            {
                if (presentLitInds[i].Contains(SYMBOLS.Substring((int)number, 1)))
                {
                    equalsUnlitInd++;
                }
            }

            return (int)Math.Pow(number, equalsUnlitInd);
        });

        // Multiply the displayed number by n, where n is the number of unique serial number characters.
        rules.Add(number =>
        {
            return number * snChars.Count;
        });

        // Use the base-35 Atbash cipher of the displayed number.
        rules.Add(number =>
        {
            return 35 - number;
        });

        // Multiply the displayed number by (m + n),
        // where m is the number of port plates and
        // n is the number of 1’s that appear in this digit’s binary conversion.
        rules.Add(number =>
        {
            return number * (binaryCount[(int)number] + BombInfo.GetPortPlateCount());
        });

        // Multiply n plus the displayed number by 6,
        // where n is the number of dots that appear in this digit’s braille conversion. (0 = J)'
        rules.Add(number =>
        {
            return (number + brailleCount[(int)number]) * 6;
        });

        // Multiply n plus the displayed number by 5, where n is equal to the colored region count on this number's 'Maritime Flags' flag.
        rules.Add(number =>
        {
            return 5 * (number + maritimeCount[(int)number]);
        });

        // Add the third and sixth digits of the serial number to the number.
        rules.Add(number =>
        {
            return number + (int)char.GetNumericValue(serialNumber[2]) + (int)char.GetNumericValue(serialNumber[5]);
        });

        // Multiply n plus the displayed number by 14.
        // If the third digit of the serial number is even, n is equal to the number of 14-segment displays required to show the number.
        // Otherwise, n is equal to the number of 14-segment displays required to show the inverse of the number.
        rules.Add(number =>
        {
            int fourteen = fourteenCount[(int)number];
            if ((int)char.GetNumericValue(serialNumber[2]) % 2 == 1)
                fourteen = 14 - fourteen;
            return (fourteen + number) * 14;
        });

        // Quadruple the displayed number,
        // then add it to the number of shapes that appear in this number's Zoni symbol (all dots, lines, circles)
        rules.Add(number =>
        {
            return (number * 4) + zoniCount[(int)number];
        });

        // Instead of using the displayed character, use d + (s * 3),
        // where d is the number of dots in this displayed number's Morse Code conversion and s is the number of dashes.
        rules.Add(number =>
        {
            char dot = '.';
            char dash = '-';
            int dotCount = MORSE_SYMBOLS[(int)number].Count(f => f == dot);
            int dashCount = 3 * (MORSE_SYMBOLS[(int)number].Count(f => f == dash));
            return dotCount + dashCount;
        });

        // Add the displayed number to the total number of letters across all displayed colors.
        rules.Add(number =>
        {
            int colorNamesNumber = 0;
            for (int i = 0; i < 3; i++)
            {
                colorNamesNumber += colorNamesLength[Colors[i]];
            }
            return number + colorNamesNumber;
        });

        // If the displayed character appears in the serial number, divide it by n+1,
        // where n is the number of times it appears in the serial number.
        // Otherwise, quintuple it.
        rules.Add(number =>
        {
            int serialNumberCount = 0;
            for (int i = 0; i < 6; i++)
            {
                if ((char.IsDigit(serialNumber[i]) && serialNumber[i] - '0' == number) || (char.IsLetter(serialNumber[i]) && serialNumber[i] - 'A' + 10 == number))
                    serialNumberCount++;
            }
            if (serialNumberCount == 0)
                return number * 5;
            else
                return number / (serialNumberCount + 1);
        });

        // If the displayed character in Morse Code reversed is a number from 0-9 or a letter from A-Z, use that number instead.
        // Otherwise, use the number added to the port count.
        rules.Add(number =>
        {
            if (validReverseMorse.Contains((int)number))
            {
                return changedReverseMorse[(int)number];
            }
            else
                return number + BombInfo.GetPortCount();
        });

        // Triple the displayed character, then add it to n times 2,
        // where n is equal to the position this character appears in its respective ’Pigpen Cipher’ table in reading order.
        // For numbers, use their position’s letter. (1=A, 2=B, ..., 0=J)
        rules.Add(number =>
        {
            return (3 * number) + (2 * pigpenCount[(int)number]);
        });

        // Add the last digit of the serial number to the displayed number, then multiply it by (m + n).
        // If the first character of the serial number is a letter, m is 1, otherwise it is 2.
        // If the second character of the serial number is a letter, n is 3, otherwise it is 4.
        rules.Add(number =>
        {
            int m;
            int n;
            if (char.IsLetter(serialNumber[0]))
                m = 1;
            else
                m = 2;
            if (char.IsLetter(serialNumber[1]))
                n = 3;
            else
                n = 4;
            return (number + (int)char.GetNumericValue(serialNumber[5])) * ((m + n));
        });

        // Don't change default
        if (rnd.Seed != 1)
            rnd.ShuffleFisherYates(rules);
        Debug.LogFormat("Using ruleseed {0}", rnd.Seed);

        // END RULE SEED

        indicatorCount = IndicatorMeshes.Length;
        Colors = new int[indicatorCount];
        Numbers = new int[indicatorCount];
        Flashes = new bool[indicatorCount][];
        Pointers = new int[indicatorCount];
        Operators = new int[2];
        SolNumbers = new double[indicatorCount];

        for (int i = 0; i < indicatorCount; i++)
            Buttons[i].OnInteract += HandlePress(i);

        reset:
        var origParenPos = PAREN_POS = Rnd.Range(0, 2);
        Operators[0] = Rnd.Range(0, 4);
        Operators[1] = Rnd.Range(0, 4);

        for (int i = 0; i < indicatorCount; i++)
        {
            Colors[i] = Rnd.Range(0, 7);
            Numbers[i] = Rnd.Range(1, 36);
            Flashes[i] = MorseToBoolArray(MORSE_SYMBOLS[Numbers[i]]);
        }

        // This changes the value of PAREN_POS if one of the colors is (ruleseed 1:green)
        int solutionNum;
        for (int i = 0; i < indicatorCount; i++)
            SolNumbers[i] = DoColorOperation(Colors[i], Numbers[i]);

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
        solutionNum = (int)Math.Abs(sol);

        if (sign == -1 && solutionNum != 0) Solution = "-....- ";

        foreach (char c in (solutionNum % 1000).ToString())
        {
            Solution += MORSE_SYMBOLS[SYMBOLS.IndexOf(c)] + " ";
        }
        Solution = Solution.Trim();

        if (origParenPos == 0)
        {
            Labels[0].text = "(";
            Labels[3].gameObject.SetActive(false);
            Labels[1].text = "" + OPERATION_SYMBOLS[Operators[0]];
            Labels[2].text = ")" + OPERATION_SYMBOLS[Operators[1]];
        }
        else
        {
            Labels[0].gameObject.SetActive(false);
            Labels[3].text = ")";
            Labels[1].text = OPERATION_SYMBOLS[Operators[0]] + "(";
            Labels[2].text = "" + OPERATION_SYMBOLS[Operators[1]];
        }

        if (origParenPos == 0)
            Debug.LogFormat("#! ({0} {1}[{2}] {3} {4} {5}[{6}]) {7} {8} {9}[{10}]. Solution: {11}{12}",
                ColorNames[Colors[0]], Numbers[0], SYMBOLS[Numbers[0]],
                OPERATION_SYMBOLS[Operators[0]],
                ColorNames[Colors[1]], Numbers[1], SYMBOLS[Numbers[1]],
                OPERATION_SYMBOLS[Operators[1]],
                ColorNames[Colors[2]], Numbers[2], SYMBOLS[Numbers[2]],
                sign == -1 ? "-" : "", solutionNum % 1000
                );
        else
            Debug.LogFormat("#! {0} {1}[{2}] {3} ({4} {5}[{6}] {7} {8} {9}[{10}]). Solution: {11}{12}",
                ColorNames[Colors[0]], Numbers[0], SYMBOLS[Numbers[0]],
                OPERATION_SYMBOLS[Operators[0]],
                ColorNames[Colors[1]], Numbers[1], SYMBOLS[Numbers[1]],
                OPERATION_SYMBOLS[Operators[1]],
                ColorNames[Colors[2]], Numbers[2], SYMBOLS[Numbers[2]],
                sign == -1 ? "-" : "", solutionNum % 1000
                );
        /*/
        for (int i = 0; i < indicatorCount; i++)
            Debug.LogFormat("[Color Morse #{0}] LED {1} is a {2} {3} ({4})", _moduleId, i + 1, ColorNames[Colors[i]], Numbers[i], SYMBOLS[Numbers[i]]);
        Debug.LogFormat("[Color Morse #{0}] Parentheses location: {1}", _moduleId, origParenPos == 0 ? "LEFT" : "RIGHT");
        Debug.LogFormat("[Color Morse #{0}] Operators: {1} and {2}", _moduleId, OPERATION_SYMBOLS[Operators[0]], OPERATION_SYMBOLS[Operators[1]]);
        for (int i = 0; i < indicatorCount; i++)
            Debug.LogFormat("[Color Morse #{0}] Number {1} after color operation is {2}", _moduleId, i + 1, SolNumbers[i]);
        if (origParenPos != PAREN_POS)
            Debug.LogFormat("[Color Morse #{0}] Parentheses locations are imaginarily swapped because of Ruleseed 1: green.", _moduleId);
        Debug.LogFormat("[Color Morse #{0}] Solution: {1}{2} ({3})", _moduleId, sign == -1 ? "-" : "", solutionNum, Solution);
        */

        BombModule.OnActivate += Activate;
    }

    void Activate()
    {
        flashingEnabled = true;
    }

    private double DoColorOperation(int color, double number)
    {
        return color == 6 ? number : rules[color](number);
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
                    for (int i = 0; i < indicatorCount; i++)
                        IndicatorMeshes[i].sharedMaterial = BlackMaterial;
                    SolutionScreenText.text = Solution.Replace('.', '•');
                    return false;
                }
            }
            else
            {
                BombModule.HandleStrike();
                Debug.LogFormat("[Color Morse #{0}] Submitted “{1}” incorrectly. Current submission: “{2}”. Resetting submission.", _moduleId, nextChar, SubmittedSolution);
                // SubmittedSolution = "";
                // clears answer
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
                for (int i = 0; i < indicatorCount; i++)
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
