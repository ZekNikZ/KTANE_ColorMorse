using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Rnd = UnityEngine.Random;

public class ColorMorseModule : MonoBehaviour
{
    public KMBombInfo BombInfo;
    public KMBombModule BombModule;
    public KMAudio Audio;
    public MeshRenderer[] IndicatorMeshes;
    public Material[] ColorMaterials;
    public string[] ColorNames;
    public Material BlackMaterial;
    public TextMesh[] Labels;
    public KMSelectable[] Buttons;
    public TextMesh SolutionScreenText;

    private int[] Colors;
    private int[] Numbers;
    private int[] SolNumbers;
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

        indicatorCount = IndicatorMeshes.Length;
        Colors = new int[indicatorCount];
        Numbers = new int[indicatorCount];
        Flashes = new bool[indicatorCount][];
        Pointers = new int[indicatorCount];
        Operators = new int[2];
        SolNumbers = new int[indicatorCount];

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

        // This changes the value of PAREN_POS if one of the colors is green
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
        solutionNum = (int) Math.Abs(sol);

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

        for (int i = 0; i < indicatorCount; i++)
            Debug.LogFormat("[Color Morse #{0}] LED {1} is a {2} {3} ({4})", _moduleId, i + 1, ColorNames[Colors[i]], Numbers[i], SYMBOLS[Numbers[i]]);
        Debug.LogFormat("[Color Morse #{0}] Parentheses location: {1}", _moduleId, origParenPos == 0 ? "LEFT" : "RIGHT");
        Debug.LogFormat("[Color Morse #{0}] Operators: {1} and {2}", _moduleId, OPERATION_SYMBOLS[Operators[0]], OPERATION_SYMBOLS[Operators[1]]);
        for (int i = 0; i < indicatorCount; i++)
            Debug.LogFormat("[Color Morse #{0}] Number {1} after color operation is {2}", _moduleId, i + 1, SolNumbers[i]);
        if (origParenPos != PAREN_POS)
            Debug.LogFormat("[Color Morse #{0}] Parentheses locations are imaginarily swapped because of green.", _moduleId);
        Debug.LogFormat("[Color Morse #{0}] Solution: {1}{2} ({3})", _moduleId, sign == -1 ? "-" : "", solutionNum, Solution);

        BombModule.OnActivate += Activate;
    }

    void Activate()
    {
        flashingEnabled = true;
    }

    private int DoColorOperation(int color, int number)
    {
        switch (color)
        {
            case 0: // Blue
                int num = number * 3;
                while (num > 9)
                {
                    num = num.ToString().ToCharArray().Sum(x => x - '0');
                }
                return num;
            case 1: // Green
                PAREN_POS = (PAREN_POS + 1) % 2;
                return number;
            case 2: // Orange
                return number % 3 == 0 ? number / 3 : number + Colors.Count(x => x == 0 || x == 4 || x == 5);
            case 3: // Purple
                return 10 - number;
            case 4: // Red
                return number % 2 == 1 ? number * 2 : number / 2;
            case 5: // Yellow
                return number * number;
            default: // White
                return number;
        }
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
