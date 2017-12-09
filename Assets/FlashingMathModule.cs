using UnityEngine;
using System.Linq;

public class FlashingMathModule : MonoBehaviour {

    public KMBombInfo BombInfo;
    public KMBombModule BombModule;
    public KMAudio Audio;
    public MeshRenderer[] IndicatorMeshes;
    public Material[] ColorMaterials;
    public string[] ColorNames;
    public Material BlackMaterial;
    public TextMesh[] Labels;
    public KMSelectable[] Buttons;

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

    public static int loggingID = 1;
    public int thisLoggingID;

    private const string SYMBOLS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private string[] MORSE_SYMBOLS = {
        "-----", ".----", "..---", "...--", "....-",
        ".....", "-....", "--...", "---..", "----.",
        ".-", "-...", "-.-.", "-..",  ".",   "..-.", "--.", "....", "..",   ".---", "-.-",  ".-..", "--",
        "-.", "---",  ".--.", "--.-", ".-.", "...",  "-",   "..-",  "...-", ".--",  "-..-", "-.--", "--.."
    };
    //private const string OPERATION_SYMBOLS = "+-×÷";
    private const string OPERATION_SYMBOLS = "+-×/";

    void Start() {
        thisLoggingID = loggingID++;

        indicatorCount = IndicatorMeshes.Length;
        Colors = new int[indicatorCount];
        Numbers = new int[indicatorCount];
        Flashes = new bool[indicatorCount][];
        Pointers = new int[indicatorCount];
        Operators = new int[2];
        SolNumbers = new int[indicatorCount];

        reset:
        PAREN_POS = Random.Range(0, 2);
        Operators[0] = Random.Range(0, 4);
        Operators[1] = Random.Range(0, 4);

        for (int i = 0; i < indicatorCount; i++) {
            var j = i;
            Buttons[j].OnInteract += delegate { HandlePress(j); return false; };
            Colors[i] = Random.Range(0, 7);
            Numbers[i] = Random.Range(1, 36);
            Flashes[i] = MorseToBoolArray(MORSE_SYMBOLS[Numbers[i]]);
            Debug.LogFormat("[ColorMorse #{0}] Number {1} is a {2} {3} ({4})", thisLoggingID, i, ColorNames[Colors[i]], Numbers[i], SYMBOLS[Numbers[i]]);
        }

        Debug.LogFormat("[ColorMorse #{0}] Parentheses Location: {1}", thisLoggingID, PAREN_POS == 0 ? "LEFT" : "RIGHT");
        Debug.LogFormat("[ColorMorse #{0}] Operators: {1} and {2}", thisLoggingID, OPERATION_SYMBOLS[Operators[0]], OPERATION_SYMBOLS[Operators[1]]);

        
        int solutionNum;
        if (PAREN_POS == 0) {
            Labels[0].text = "(";
            Labels[3].text = "";
            Labels[1].text = "" + OPERATION_SYMBOLS[Operators[0]];
            Labels[2].text = ")" + OPERATION_SYMBOLS[Operators[1]];
        } else {
            Labels[0].text = "";
            Labels[3].text = ")";
            Labels[1].text = OPERATION_SYMBOLS[Operators[0]] + "(";
            Labels[2].text = "" + OPERATION_SYMBOLS[Operators[1]];
        }

        for (int i = 0; i < indicatorCount; i++) {
            SolNumbers[i] = DoColorOperation(Colors[i], Numbers[i]);
            Debug.LogFormat("[ColorMorse #{0}] Number {1} after color operation is {2}", thisLoggingID, i, SolNumbers[i]);
        }

        Solution = "";
        float sign, sol;
        if (PAREN_POS == 0) {
            sol = Op2(Op1(SolNumbers[0], SolNumbers[1]), SolNumbers[2]);
        } else {
            sol = Op1(SolNumbers[0], Op2(SolNumbers[1], SolNumbers[2]));
        }
        if (float.IsInfinity(sol) || float.IsNaN(sol)) {
            goto reset;
        }
        sign = Mathf.Sign(sol);
        solutionNum = Mathf.FloorToInt(Mathf.Abs(sol));

        if (sign == -1 && solutionNum != 0) Solution = "-....- ";

        foreach (char c in (solutionNum % 1000).ToString()) {
            Solution += MORSE_SYMBOLS[SYMBOLS.IndexOf(c)] + " ";
        }
        Solution = Solution.Trim();
        Debug.LogFormat("[ColorMorse #{0}] Solution: {1}{2} ({3})", thisLoggingID, sign==-1?"-":"", solutionNum, Solution);
        BombModule.OnActivate += Activate;
    }

    void Activate() {
        flashingEnabled = true;
    }

    private int DoColorOperation(int color, int number) {
        switch (color) {
            case 0:
                int num = number * 3;
                while (num > 9) {
                    num = num.ToString().ToCharArray().Sum(x => x - '0');
                }
                return num;
            case 1:
                PAREN_POS = (PAREN_POS + 1) % 2;
                Debug.LogFormat("[ColorMorse #{0}] Parentheses locations are imaginarily swapped.", thisLoggingID);
                return number;
            case 2:
                if (number % 3 == 0) {
                    return number / 3;
                } else {
                    return number + Colors.Count(x => x == 0 || x == 4 || x == 5);
                }
            case 3:
                return 10 - number;
            case 4:
                if (number % 2 == 1) {
                    return number * 2;
                } else {
                    return number / 2;
                }
            case 5:
                return number * number;
            default:
                return number;
        }
    }

    private bool HandlePress(int button) {
        Audio.PlaySoundAtTransform("ColorMorseButtonPress", this.transform);
        Buttons[button].AddInteractionPunch();
        char nextChar;
        switch (button) {
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
        if (Solution.StartsWith(SubmittedSolution + nextChar)) {
            SubmittedSolution += nextChar;
            if (nextChar == ' ') Debug.LogFormat("[ColorMorse #{0}] Character submitted correctly. Current Submission: \"{1}\"", thisLoggingID, SubmittedSolution);
            if (Solution.Length == SubmittedSolution.Length) {
                BombModule.HandlePass();
                Debug.LogFormat("[ColorMorse #{0}] Full solution submitted.", thisLoggingID);
                flashingEnabled = false;
                for (int i = 0; i < indicatorCount; i++) {
                    IndicatorMeshes[i].sharedMaterial = BlackMaterial;
                }
            }
        } else {
            BombModule.HandleStrike();
            Debug.LogFormat("[ColorMorse #{0}] Submitted '{1}' incorrectly. Current Submission: \"{2}\". Reseting submission.", thisLoggingID, nextChar, SubmittedSolution);
            SubmittedSolution = "";
        }
        return false;
    }

    private const float DOT_LENGTH = 0.2f;
    private float timer = DOT_LENGTH;
    private bool[] debug_seq;

    void Update() {
        if (flashingEnabled) {
            timer -= Time.deltaTime;
            if (timer < 0) {
                for (int i = 0; i < indicatorCount; i++) {
                    if (Flashes[i][Pointers[i]]) {
                        IndicatorMeshes[i].sharedMaterial = ColorMaterials[Colors[i]];
                    } else {
                        IndicatorMeshes[i].sharedMaterial = BlackMaterial;
                    }
                    Pointers[i] = (Pointers[i] + 1) % Flashes[i].Length;
                }
                timer = DOT_LENGTH;
            }
        }
    }

    bool[] MorseToBoolArray(string morse) {
        int length = 0;
        foreach (char c in morse) {
            if (c == '.') {
                length += 2;
            } else {
                length += 4;
            }
        }
        bool[] result = new bool[length + 4];
        int pointer = 0;
        foreach (char c in morse) {
            if (c == '.') {
                result[pointer] = true;
                pointer += 2;
            } else {
                result[pointer] = true;
                result[pointer + 1] = true;
                result[pointer + 2] = true;
                pointer += 4;
            }
        }
        return result;
    }

    float Op1(float x, float y) {
        switch (Operators[0]) {
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

    float Op2(float x, float y) {
        switch (Operators[1]) {
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

}
