using UnityEngine;
using System.Collections;

public class DEBUG_FLASH : MonoBehaviour {

    public Material MAT_YELLOW;
    public Material MAT_BLACK;

    private const string SYMBOLS = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private string[] MORSE_SYMBOLS = {
        "-----", ".----", "..---", "...--", "....-",
        ".....", "-....", "--...", "---..", "----.",
        ".-", "-...", "-.-.", "-..",  ".",   "..-.", "--.", "....", "..",   ".---", "-.-",  ".-..", "--",
        "-.", "---",  ".--.", "--.-", ".-.", "...",  "-",   "..-",  "...-", ".--",  "-..-", "-.--", "--.."
    };

	// Use this for initialization
	void Start () {
        debug_seq = MorseToBoolArray(MORSE_SYMBOLS[SYMBOLS.IndexOf("D")]);
	}

    public const float DOT_LENGTH = 0.2f;
    public float timer = DOT_LENGTH;
    private bool[] debug_seq;
    private int pointer = 0;
	
	// Update is called once per frame
	void Update () {
        timer -= Time.deltaTime;
        if (timer < 0) {
            if(debug_seq[pointer]) {
                GetComponent<MeshRenderer>().sharedMaterial = MAT_YELLOW;
            } else {
                GetComponent<MeshRenderer>().sharedMaterial = MAT_BLACK;
            }
            pointer = (pointer + 1) % debug_seq.Length;
            timer = DOT_LENGTH;
        }
	}

    bool[] MorseToBoolArray (string morse) {
        int length = 0;
        foreach(char c in morse) {
            if (c == '.') {
                length += 2;
            } else {
                length += 4;
            }
        }
        bool[] result = new bool[length + 3];
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
}
