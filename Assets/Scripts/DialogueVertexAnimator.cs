using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

public class DialogueVertexAnimator {
    public bool textAnimating = false;
    private bool stopAnimating = false;

    private TMP_Text textBox;
    private float textAnimationScale;
    private AudioSourceGroup audioSourceGroup;
    public DialogueVertexAnimator(TMP_Text _textBox, AudioSourceGroup _audioSourceGroup) {
        textBox = _textBox;
        audioSourceGroup = _audioSourceGroup;

        textAnimationScale = textBox.fontSize;
    }

    // grab the remainder of the text until ">" or end of string
    private const string REMAINDER_REGEX = "(?<color>.*?((?=>)|(/|$)))";
    private const string PAUSE_REGEX_STRING = "<p:(?<pause>" + REMAINDER_REGEX + ")>";
    private static readonly Regex pauseRegex = new Regex(PAUSE_REGEX_STRING);
    private const string SPEED_REGEX_STRING = "<sp:(?<speed>" + REMAINDER_REGEX + ")>";
    private static readonly Regex speedRegex = new Regex(SPEED_REGEX_STRING);
    private const string ANIM_START_REGEX_STRING = "<anim:(?<anim>" + REMAINDER_REGEX + ")>";
    private static readonly Regex animStartRegex = new Regex(ANIM_START_REGEX_STRING);
    private const string ANIM_END_REGEX_STRING = "</anim>";
    private static readonly Regex animEndRegex = new Regex(ANIM_END_REGEX_STRING);

    public List<DialogueCommand> ProcessInputString(string message, out string processedMessage) {
        List<DialogueCommand> result = new List<DialogueCommand>();
        processedMessage = message;

        MatchCollection pauseMatches = pauseRegex.Matches(processedMessage);
        foreach (Match match in pauseMatches) {
            string val = match.Groups["pause"].Value;
            string pauseName = val;
            Debug.Assert(pauseDictionary.ContainsKey(pauseName), "no pause registered for '" + pauseName + "'");
            result.Add(new DialogueCommand {
                position = VisibleCharactersUpToIndex(processedMessage, match.Index),
                type = DialogueCommandType.Pause,
                floatValue = pauseDictionary[pauseName]
            });
        }
        processedMessage = Regex.Replace(processedMessage, PAUSE_REGEX_STRING, "");

        MatchCollection speedMatches = speedRegex.Matches(processedMessage);
        foreach (Match match in speedMatches) {
            string stringVal = match.Groups["speed"].Value;
            float val;
            if (!float.TryParse(stringVal, out val)) {
                val = 150f;
            }
            result.Add(new DialogueCommand {
                position = VisibleCharactersUpToIndex(processedMessage, match.Index),
                type = DialogueCommandType.TextSpeedChange,
                floatValue = val
            });
        }
        processedMessage = Regex.Replace(processedMessage, SPEED_REGEX_STRING, "");

        MatchCollection animStartMatches = animStartRegex.Matches(processedMessage);
        foreach (Match match in animStartMatches) {
            string stringVal = match.Groups["anim"].Value;
            result.Add(new DialogueCommand {
                position = VisibleCharactersUpToIndex(processedMessage, match.Index),
                type = DialogueCommandType.AnimStart,
                textAnimValue = GetTextAnimationType(stringVal)
            });
        }
        processedMessage = Regex.Replace(processedMessage, ANIM_START_REGEX_STRING, "");

        MatchCollection animEndMatches = animEndRegex.Matches(processedMessage);
        foreach (Match match in animEndMatches) {
            result.Add(new DialogueCommand {
                position = VisibleCharactersUpToIndex(processedMessage, match.Index),
                type = DialogueCommandType.AnimEnd,
            });
        }
        processedMessage = Regex.Replace(processedMessage, ANIM_END_REGEX_STRING, "");
        return result;
    }

    private static TextAnimationType GetTextAnimationType(string stringVal) {
        TextAnimationType result;
        try {
            result = (TextAnimationType)Enum.Parse(typeof(TextAnimationType), stringVal, true);
        } catch (ArgumentException) {
            Debug.LogError("Invalid Text Animation Type: " + stringVal);
            result = TextAnimationType.none;
        }
        return result;
    }

    private static int VisibleCharactersUpToIndex(string message, int index) {
        int result = 0;
        bool insideBrackets = false;
        for (int i = 0; i < index; i++) {
            if (message[i] == '<') {
                insideBrackets = true;
            } else if (message[i] == '>') {
                insideBrackets = false;
                result--;
            }
            if (!insideBrackets) {
                result++;
            } else if (i + 6 < index && message.Substring(i, 6) == "sprite") {
                result++;
            }
        }
        return result;
    }
    private static readonly Color32 clear = new Color32(0, 0, 0, 0);
    private const int SCALE_STEPS = 5;
    private const float CHAR_ANIM_TIME = 0.07f;
    private static readonly Vector3 vecZero = Vector3.zero;
    public IEnumerator AnimateTextIn(List<DialogueCommand> commands, string processedMessage, AudioClip voice_sound, Action onFinish) {
        textAnimating = true;
        float secondsPerCharacter = 1f / 150f;
        float timeOfLastCharacter = 0;

        TextAnimInfo[] textAnimInfo = SeparateOutTextAnimInfo(commands);
        TMP_TextInfo textInfo = textBox.textInfo;
        for (int i = 0; i < textInfo.meshInfo.Length; i++) //Clear the mesh 
        {
            TMP_MeshInfo meshInfer = textInfo.meshInfo[i];
            if (meshInfer.vertices != null) {
                for (int j = 0; j < meshInfer.vertices.Length; j++) {
                    meshInfer.vertices[j] = vecZero;
                }
            }
        }

        textBox.text = processedMessage;
        textBox.ForceMeshUpdate();

        TMP_MeshInfo[] cachedMeshInfo = textInfo.CopyMeshInfoVertexData();
        Color32[][] originalColors = new Color32[textInfo.meshInfo.Length][];
        for (int i = 0; i < originalColors.Length; i++) {
            Color32[] theColors = textInfo.meshInfo[i].colors32;
            originalColors[i] = new Color32[theColors.Length];
            Array.Copy(theColors, originalColors[i], theColors.Length);
        }
        int charCount = textInfo.characterCount;
        float[] charAnimStartTimes = new float[charCount];
        for (int i = 0; i < charCount; i++) {
            charAnimStartTimes[i] = -1; //indicate the character as not yet started animating.
        }
        int visableCharacterIndex = 0;
        while (true) {
            if (stopAnimating) {
                for (int i = visableCharacterIndex; i < charCount; i++) {
                    charAnimStartTimes[i] = Time.unscaledTime;
                }
                visableCharacterIndex = charCount;
                FinishAnimating(onFinish);
            }
            if (ShouldShowNextCharacter(secondsPerCharacter, timeOfLastCharacter)) {
                if (visableCharacterIndex < charCount) {
                    for (int i = 0; i < commands.Count; i++) {
                        DialogueCommand command = commands[i];
                        if (command.position == visableCharacterIndex) {
                            switch (command.type) {
                                case DialogueCommandType.Pause:
                                    timeOfLastCharacter = Time.unscaledTime + command.floatValue;
                                    break;
                                case DialogueCommandType.TextSpeedChange:
                                    secondsPerCharacter = 1f / command.floatValue;
                                    break;
                            }
                            commands.RemoveAt(i);
                            i--;
                        }
                    }
                    if (ShouldShowNextCharacter(secondsPerCharacter, timeOfLastCharacter)) {
                        charAnimStartTimes[visableCharacterIndex] = Time.unscaledTime;
                        if (textInfo.characterInfo[visableCharacterIndex].character != ' ') {
                            PlayDialogueSound(voice_sound);
                        }
                        visableCharacterIndex++;
                        timeOfLastCharacter = Time.unscaledTime;
                        if (visableCharacterIndex == charCount) {
                            FinishAnimating(onFinish);
                        }
                    }
                }
            }
            for (int j = 0; j < charCount; j++) {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[j];
                if (charInfo.isVisible) //Invisible characters have a vertexIndex of 0 because they have no vertices and so they should be ignored to avoid messing up the first character in the string whic also has a vertexIndex of 0
                {
                    int vertexIndex = charInfo.vertexIndex;
                    int materialIndex = charInfo.materialReferenceIndex;
                    Color32[] destinationColors = textInfo.meshInfo[materialIndex].colors32;
                    Color32 theColor = j < visableCharacterIndex ? originalColors[materialIndex][vertexIndex] : clear;
                    destinationColors[vertexIndex + 0] = theColor;
                    destinationColors[vertexIndex + 1] = theColor;
                    destinationColors[vertexIndex + 2] = theColor;
                    destinationColors[vertexIndex + 3] = theColor;

                    Vector3[] sourceVertices = cachedMeshInfo[materialIndex].vertices;
                    Vector3[] destinationVertices = textInfo.meshInfo[materialIndex].vertices;
                    float charSize = 0;
                    float charAnimStartTime = charAnimStartTimes[j];
                    if (charAnimStartTime >= 0) {
                        float timeSinceAnimStart = Time.unscaledTime - charAnimStartTime;
                        charSize = Mathf.Min(1, timeSinceAnimStart / CHAR_ANIM_TIME);
                    }

                    Vector3 animPosAdjustment = GetAnimPosAdjustment(textAnimInfo, j, Time.unscaledTime)* textAnimationScale;
                    Vector3 offset = (sourceVertices[vertexIndex + 0] + sourceVertices[vertexIndex + 2]) / 2;
                    destinationVertices[vertexIndex + 0] = ((sourceVertices[vertexIndex + 0] - offset) * charSize) + offset + animPosAdjustment;
                    destinationVertices[vertexIndex + 1] = ((sourceVertices[vertexIndex + 1] - offset) * charSize) + offset + animPosAdjustment;
                    destinationVertices[vertexIndex + 2] = ((sourceVertices[vertexIndex + 2] - offset) * charSize) + offset + animPosAdjustment;
                    destinationVertices[vertexIndex + 3] = ((sourceVertices[vertexIndex + 3] - offset) * charSize) + offset + animPosAdjustment;
                }
            }
            textBox.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            for (int i = 0; i < textInfo.meshInfo.Length; i++) {
                TMP_MeshInfo theInfo = textInfo.meshInfo[i];
                theInfo.mesh.vertices = theInfo.vertices;
                textBox.UpdateGeometry(theInfo.mesh, i);
            }
            yield return null;
        }
    }

    private void FinishAnimating(Action onFinish) {
        textAnimating = false;
        stopAnimating = false;
        onFinish?.Invoke();
    }

    private const float NOISE_MAGNITUDE_ADJUSTMENT = 0.09f;
    private const float NOISE_FREQUENCY_ADJUSTMENT = 12f;
    private Vector3 GetAnimPosAdjustment(TextAnimInfo[] textAnimInfo, int charIndex, float time) {
        float x = 0;
        float y = 0;
        for (int i = 0; i < textAnimInfo.Length; i++) {
            TextAnimInfo info = textAnimInfo[i];
            if (charIndex >= info.startIndex && charIndex < info.endIndex) {
                if (info.type == TextAnimationType.shake) {
                    x += (Mathf.PerlinNoise((charIndex + time) * NOISE_FREQUENCY_ADJUSTMENT, 0) - 0.5f) * NOISE_MAGNITUDE_ADJUSTMENT;
                    y += (Mathf.PerlinNoise((charIndex + time) * NOISE_FREQUENCY_ADJUSTMENT, 1000) - 0.5f) * NOISE_MAGNITUDE_ADJUSTMENT;
                } else if (info.type == TextAnimationType.wave) {
                    y += Mathf.Sin((charIndex * 1.5f) + (time * 6)) * 0.07f;
                }
            }
        }
        return new Vector3(x, y, 0);
    }

    private static bool ShouldShowNextCharacter(float secondsPerCharacter, float timeOfLastCharacter) {
        return (Time.unscaledTime - timeOfLastCharacter) > secondsPerCharacter;
    }
    public void SkipToEndOfCurrentMessage() {
        if (textAnimating) {
            stopAnimating = true;
        }
    }

    private float timeUntilNextDialogueSound = 0;
    private float lastDialogueSound = 0;
    private void PlayDialogueSound(AudioClip voice_sound) {
        if (Time.unscaledTime - lastDialogueSound > timeUntilNextDialogueSound) {
            timeUntilNextDialogueSound = UnityEngine.Random.Range(0.02f, 0.08f);
            lastDialogueSound = Time.unscaledTime;
            audioSourceGroup.PlayFromNextSource(voice_sound); //Use Multiple Audio Sources to allow playing multiple sounds at once
        }
    }

    private static readonly Dictionary<string, float> pauseDictionary = new Dictionary<string, float>      {
        { "tiny", .1f },
        { "short", .25f },
        { "normal", 0.666f },
        { "long", 1f },
        { "read", 2f },
    };

    private TextAnimInfo[] SeparateOutTextAnimInfo(List<DialogueCommand> commands) {
        List<TextAnimInfo> tempResult = new List<TextAnimInfo>();
        List<DialogueCommand> animStartCommands = new List<DialogueCommand>();
        List<DialogueCommand> animEndCommands = new List<DialogueCommand>();
        for (int i = 0; i < commands.Count; i++) {
            DialogueCommand command = commands[i];
            if (command.type == DialogueCommandType.AnimStart) {
                animStartCommands.Add(command);
                commands.RemoveAt(i);
                i--;
            } else if (command.type == DialogueCommandType.AnimEnd) {
                animEndCommands.Add(command);
                commands.RemoveAt(i);
                i--;
            }
        }
        if (animStartCommands.Count != animEndCommands.Count) {
            Debug.LogError("Unequal number of start and end animation commands. Start Commands: " + animStartCommands.Count + " End Commands: " + animEndCommands.Count);
        } else {
            for (int i = 0; i < animStartCommands.Count; i++) {
                DialogueCommand startCommand = animStartCommands[i];
                DialogueCommand endCommand = animEndCommands[i];
                tempResult.Add(new TextAnimInfo {
                    startIndex = startCommand.position,
                    endIndex = endCommand.position,
                    type = startCommand.textAnimValue
                });
            }
        }
        return tempResult.ToArray();
    }
}

public struct DialogueCommand {
    public int position;
    public DialogueCommandType type;
    public float floatValue;
    public string stringValue;
    public TextAnimationType textAnimValue;
}

public enum DialogueCommandType {
    Pause,
    TextSpeedChange,
    AnimStart,
    AnimEnd
}

public enum TextAnimationType {
    none,
    shake,
    wave
}

public struct TextAnimInfo {
    public int startIndex;
    public int endIndex;
    public TextAnimationType type;
}
