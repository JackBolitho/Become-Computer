using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class ComputerController : MonoBehaviour
{
    private int[] tapeValues;
    private int[] goalValues;
    [SerializeField] private GameObject tapeNumber; //prefab
    [SerializeField] private float tapeNumberSpacing;
    [SerializeField] private Vector3 centerPosition;
    [SerializeField] private Vector3 printPosition;
    private GameObject[] tapeNumberArray;
    private PlayerControls playerControls;
    private bool playerCanMove = true;
    private int tapePointer = 0;
    private int maximumLengthSolution;
    private int maxLengthColorLabelEnd;
    private string maxLabel = "<color=\"red\">";

    //deals with program
    [SerializeField] TextMeshPro programText;
    [SerializeField] float instructionTimeDelay;
    [SerializeField] private int instructionPointer = 0;
    private List<(int, int[],List<int>, List<GameObject>)> tapeHistory = new List<(int, int[], List<int>, List<GameObject>)>(); //a stack that stores the tape state at the start of running a while loop
    private Dictionary<int, (int, int[], List<int>, List<GameObject>)> startLoopHistory = new Dictionary<int, (int, int[], List<int>, List<GameObject>)>(); //stores the state at outer [
    private int unpairedLoopCount = 0;
    private Dictionary<int,int> startToEnd = new Dictionary<int,int>();
    private Dictionary<int,int> endToStart = new Dictionary<int,int>();
    private string program = "";

    //fibonacci numbers:  .>+.>+[-<<[>>+<<-]>[>+<<+>-]>[<+>-]<.>+]
    
    //deals with movement
    [SerializeField] private float moveAnimationDuration;
    private Vector3 startPos;
    private Vector3 endPos;
    private float elapsedTime;
    private bool isLerping = false;
    [SerializeField] private Animator machineAnimator;

    //level integration
    private List<int> printedValues = new List<int>();
    private List<GameObject> printedNums = new List<GameObject>();
    private List<GameObject> recentPrintedNums = new List<GameObject>();

    //puzzle setup
    [SerializeField] private PuzzleSetups puzzle;
    

    void Awake()
    {
        playerControls = new PlayerControls();
    }

    void Start()
    {
        SetUpPuzzle();
        StartCoroutine(ParseProgram());
    }

    void OnEnable()
    {
        playerControls.Enable();
        playerControls.Controls.Right.performed += IncrementPointer;
        playerControls.Controls.Left.performed += DecrementPointer;
        playerControls.Controls.Up.performed += IncrementValue;
        playerControls.Controls.Down.performed += DecrementValue;
        playerControls.Controls.Undo.performed += Undo;
        playerControls.Controls.StartLoop.performed += SetStartLoop;
        playerControls.Controls.EndLoop.performed += SetEndLoop;
        playerControls.Controls.Restart.performed += Restart;
        playerControls.Controls.Print.performed += Print;
    }

    void OnDisable()
    {
        playerControls.Disable();
        playerControls.Controls.Right.performed -= IncrementPointer;
        playerControls.Controls.Left.performed -= DecrementPointer;
        playerControls.Controls.Up.performed -= IncrementValue;
        playerControls.Controls.Down.performed -= DecrementValue;
        playerControls.Controls.Undo.performed -= Undo;
        playerControls.Controls.StartLoop.performed -= SetStartLoop;
        playerControls.Controls.EndLoop.performed -= SetEndLoop;
        playerControls.Controls.Restart.performed -= Restart;
        playerControls.Controls.Print.performed -= Print;
    }
    
    //sets global variables to start the puzzle
    private void SetUpPuzzle()
    {
        tapeValues = (int[])puzzle.tapeValues.Clone();
        goalValues = (int[])puzzle.goalValues.Clone();
        tapeNumberArray = new GameObject[tapeValues.Length];
        maximumLengthSolution = puzzle.maximumLengthSolution;
        maxLengthColorLabelEnd = maximumLengthSolution + maxLabel.Length;

        InitializeTapeValues();
    }
    
    //instantiate the gameobjects for the tape
    private void InitializeTapeValues()
    {
        for (int i = 0; i < tapeValues.Length; i++)
        {
            GameObject newTapeNum = Instantiate(tapeNumber);
            newTapeNum.transform.SetParent(this.transform);
            newTapeNum.transform.localPosition = centerPosition + new Vector3(i * tapeNumberSpacing, 0f, 0f);
            newTapeNum.GetComponent<TextMeshPro>().text = tapeValues[i].ToString();

            tapeNumberArray[i] = newTapeNum;
        }
    }

    //triggers the lerping animation that moves the parent of the tape gameobjects
    private void StartLerp(Vector3 start, Vector3 end)
    {
        startPos = start;
        endPos = end;
        isLerping = true;
        elapsedTime = 0f;
    }

    private void Update()
    {
        if (isLerping)
        {
            elapsedTime += Time.deltaTime;
            float percentageComplete = elapsedTime / moveAnimationDuration;
            gameObject.transform.position = Vector3.Lerp(startPos, endPos, Mathf.SmoothStep(0, 1, percentageComplete));

            if (gameObject.transform.position.Equals(endPos))
            {
                isLerping = false;
            }
        }
    }

    //take an array of tape values the size of the tape, and replaces the tape with those values
    private void UndoTapeHistory()
    {
        if(tapeHistory.Count > 0)
        {
            int tapePointerVal = tapeHistory[tapeHistory.Count-1].Item1;
            int[] tapeVals = tapeHistory[tapeHistory.Count-1].Item2;
            List<int> printedVals = tapeHistory[tapeHistory.Count-1].Item3;
            List<GameObject> nextRecentPrintedObjs = tapeHistory[tapeHistory.Count-1].Item4;

            tapeHistory.RemoveAt(tapeHistory.Count-1);

            //assign tape vals
            for(int i = 0; i < tapeNumberArray.Length; i++)
            {
                tapeValues = tapeVals;
                tapeNumberArray[i].GetComponent<TextMeshPro>().text = tapeVals[i].ToString();
            }

            //remove recently printed objects
            for(int i = recentPrintedNums.Count-1; i >= 0; i--){
                Destroy(recentPrintedNums[i]);
                printedNums.Remove(recentPrintedNums[i]);
            }

            recentPrintedNums = nextRecentPrintedObjs;
            printedValues = printedVals;

            MovePointerToPosition(tapePointerVal);
            MoveAllPrintedNums();
        }
    }

    private void SaveStartLoopHistory(int index)
    {
        int[] newArray = new int[tapeValues.Length];
        tapeValues.CopyTo(newArray, 0);
        (int, int[], List<int>, List<GameObject>) historyPair = (tapePointer, newArray, new List<int>(printedValues), new List<GameObject>(recentPrintedNums));
        startLoopHistory.Add(index, historyPair);
    }

    private void LoadStartLoopHistory(int index)
    {
        
    }

    //deletes a printed number and cleans up
    private void UndoPrint()
    {
        GameObject num = printedNums[printedNums.Count - 1];
        printedValues.RemoveAt(printedValues.Count - 1);
        recentPrintedNums.Remove(num);
        printedNums.RemoveAt(printedNums.Count - 1);
        Destroy(num);

        MoveAllPrintedNums();
    }

    //save the current values in the tape, and put them in the tape history
    private void SaveTapeValues()
    {
        int[] newArray = new int[tapeValues.Length];
        tapeValues.CopyTo(newArray, 0);
        (int, int[], List<int>, List<GameObject>) historyPair = (tapePointer, newArray, new List<int>(printedValues), new List<GameObject>(recentPrintedNums));
        recentPrintedNums.Clear();
        tapeHistory.Add(historyPair);
    }

    //move the tape pointer to a specific location on the tape, and move all tape gameobjects accordingly
    private bool MovePointerToPosition(int newtapePointer)
    {
        if (newtapePointer >= 0 && newtapePointer < tapeValues.Length)
        {
            tapePointer = newtapePointer;

            //TODO: incorperate number movement with new tape animation

            //move tape graphics
            StartLerp(gameObject.transform.position, new Vector3(-newtapePointer * tapeNumberSpacing, 0f, 0f));
            return true;
        }
        return false;
    }

    // returns the success of the pointer move, and offsets the pointer by val
    private bool MovePointer(int val)
    {
        return MovePointerToPosition(tapePointer + val);
    }

    private void SetValue(int val)
    {
        //pointerAnimator.SetTrigger("WriteValue");
        tapeValues[tapePointer] = val;
        tapeNumberArray[tapePointer].GetComponent<TextMeshPro>().text = val.ToString();
    }

    private void Restart(InputAction.CallbackContext context)
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void IncrementPointer(InputAction.CallbackContext context)
    {
        if (playerCanMove)
        {
            if (IncrementPointer())
            {
                WriteToProgram(">");
                instructionPointer++;
            }
        }
    }

    private bool IncrementPointer()
    {
        bool pointerMoved =  MovePointer(1);
        if (pointerMoved)
        {
            machineAnimator.SetTrigger("IncrementPointer");
        }
        return pointerMoved;
    }

    private void DecrementPointer(InputAction.CallbackContext context)
    {
        if (playerCanMove)
        {
            if (DecrementPointer())
            {
                WriteToProgram("<");
                instructionPointer++;
            }
        }
    }

    private bool DecrementPointer()
    {
        bool pointerMoved =  MovePointer(-1);
        if (pointerMoved)
        {
            machineAnimator.SetTrigger("DecrementPointer");
        }
        return pointerMoved;
    }

    private void IncrementValue(InputAction.CallbackContext context)
    {
        if (playerCanMove)
        {
            WriteToProgram("+");
            IncrementValue();
            instructionPointer++;
        }
    }

    private void IncrementValue()
    {
        SetValue(tapeValues[tapePointer] + 1);
    }

    private void DecrementValue(InputAction.CallbackContext context)
    {
        if (playerCanMove)
        {
            WriteToProgram("-");
            DecrementValue();
            instructionPointer++;
        }
    }

    private void DecrementValue()
    {
        SetValue(tapeValues[tapePointer] - 1);
    }

    private void Print(InputAction.CallbackContext context)
    {
        if (playerCanMove)
        {
            WriteToProgram(".");
            PrintToScreen();
            if (CheckPuzzleCorrectness())
            {
                Debug.Log("PUZZLE SOLVED!");
            }
            instructionPointer++;
        }
    }

    private void PrintToScreen()
    {
        int printedValue = tapeValues[tapePointer];

        GameObject printedNum = Instantiate(tapeNumber);
        printedNums.Add(printedNum);
        recentPrintedNums.Add(printedNum);
        printedValues.Add(printedValue);

        printedNum.GetComponent<TextMeshPro>().text = printedValue.ToString();
        printedNum.transform.position = centerPosition;
        MoveAllPrintedNums();
    }

    private void MoveAllPrintedNums()
    {
        Vector3 originPos = printPosition - new Vector3(tapeNumberSpacing/2 * (printedNums.Count-1), 0, 0);
        for(int i = 0; i < printedNums.Count; i++)
        {
            printedNums[i].transform.position = originPos + new Vector3(tapeNumberSpacing * i, 0, 0);
        }
    }
    
    //verifies whether or not the printed numbers are equal to the puzzle solution
    private bool CheckPuzzleCorrectness()
    {
        if (goalValues.Length != printedValues.Count)
        {
            return false;
        }

        for (int i = 0; i < goalValues.Length; i++)
        {
            if (goalValues[i] != printedValues[i])
            {
                return false;
            }
        }
        return true;
    }

    private void SetStartLoop(InputAction.CallbackContext context)
    {
        if (playerCanMove)
        {
            WriteToProgram("[");
            instructionPointer++;
            unpairedLoopCount++;
        }
    }

    private void SetEndLoop(InputAction.CallbackContext context)
    {
        if(playerCanMove && unpairedLoopCount > 0)
        {
            unpairedLoopCount--;

            //exit out if the backtrack loop is not present
            int pairIndex = IdentifyBacktrackLoop(instructionPointer);
            if(pairIndex == -1){
                return;
            }

            if(unpairedLoopCount == 0)
            {
                // jump to matching [ if the current value is not 0
                if (tapeValues[tapePointer] != 0){
                    instructionPointer = endToStart[instructionPointer];
                }

                WriteToProgram("]");

                StartCoroutine("ParseProgram");
            }
            else{
                WriteToProgram("]");
                instructionPointer++;
            }
        }
    }
    
    private void Undo(InputAction.CallbackContext context)
    {
        if(program.Length > 0)
        {
            char currChar = program[program.Length-1];
            switch(currChar){
                case '>':
                    instructionPointer = program.Length-1;
                    MovePointer(-1);
                    break;
                case '<':
                    instructionPointer = program.Length-1;
                    MovePointer(1);
                    break;
                case '+':
                    instructionPointer = program.Length-1;
                    SetValue(tapeValues[tapePointer] - 1);
                    break;
                case '-':
                    instructionPointer = program.Length-1;
                    SetValue(tapeValues[tapePointer] + 1);
                    break;
                case '[':
                    instructionPointer = program.Length-1;
                    unpairedLoopCount--;
                    break;
                case ']':
                    //remove from dictionaries        
                    instructionPointer = program.Length-1;
                    int valToRemove = endToStart[instructionPointer];
                    startToEnd.Remove(valToRemove);
                    endToStart.Remove(instructionPointer);

                    //reset tape
                    instructionPointer = program.Length-1;
                    unpairedLoopCount++;
                    if (unpairedLoopCount == 1)
                    {
                        UndoTapeHistory();
                    }
                    break;
                case '.':
                    UndoPrint();
                    instructionPointer--;
                    break;
                default:
                    break;
            }
            program = program.Substring(0, program.Length-1);
            SetAsProgram(program);
        }
    }

    private void SetAsProgram(string str){
        program = str;
        programText.text = program;
    }

    private void WriteToProgram(string str){
        
        if(program.Length == maximumLengthSolution){
            program += maxLabel;
        }
        program += str;
        programText.text = program;
    }

    //adds the start and end indices of a matching pair to a dictionary 
    private void IdentifyLoop(int loopStartIndex)
    {
        //find the end point of the loop
        int loopEndIndex = FindLoopEnd(loopStartIndex);
        if(loopEndIndex == -1){
            Debug.LogError("While loop is not closed! Syntax error!");
            return;
        }

        //add to dictionaries
        startToEnd.Add(loopStartIndex, loopEndIndex);
        endToStart.Add(loopEndIndex, loopStartIndex);
    }

    //finds the adjacent pair of [], where loopStartIndex is ] and [ is returned, or -1 if not present
    private int IdentifyBacktrackLoop(int loopStartIndex)
    {
        //find the end point of the loop
        int loopEndIndex = FindLoopStart(loopStartIndex);
        if(loopEndIndex == -1){
            Debug.LogError("While loop is not closed! Syntax error!");
            return -1;
        }

        //add to dictionaries
        startToEnd.Add(loopEndIndex, loopStartIndex);
        endToStart.Add(loopStartIndex, loopEndIndex);
        return loopEndIndex;
    }

    //returns the tapePointer of the matching '[' to start position
    private int FindLoopStart(int endingIndex)
    {
        int tempPointer = endingIndex-1;
        int pairIdentifier = 0;
        while(tempPointer >= 0)
        {
            //mark interior loops
            if(program[tempPointer] == ']'){
                pairIdentifier++;
            }else if (program[tempPointer] == '['){
                if(pairIdentifier == 0){
                    return tempPointer;
                }else{
                    pairIdentifier--;
                }
            }

            tempPointer--;
        }
        return -1;
    }

    //returns the tapePointer of the closest ']' to start position
    private int FindLoopEnd(int startingIndex)
    {
        int tempPointer = startingIndex+1;
        int pairIdentifier = 0;
        while(tempPointer < program.Length)
        {
            //exit when no such loop start exists
            if(tempPointer == 0){
                return -1;
            }

            //mark interior loops
            if(program[tempPointer] == '['){
                pairIdentifier++;
            }else if (program[tempPointer] == ']'){
                if(pairIdentifier == 0){
                    return tempPointer;
                }else{
                    pairIdentifier--;
                }
            }

            tempPointer++;
        }
        return -1;
    }

    //run the program starting at the location of the instruction pointer
    private IEnumerator ParseProgram()
    {
        playerCanMove = false;
        SaveTapeValues();

        while (instructionPointer < program.Length)
        {
            char currChar = program[instructionPointer];
            switch (currChar)
            {
                case '>':
                    IncrementPointer();
                    break;
                case '<':
                    DecrementPointer();
                    break;
                case '+':
                    IncrementValue();
                    break;
                case '-':
                    DecrementValue();
                    break;
                case '[':
                    if (!startToEnd.ContainsKey(instructionPointer))
                    {
                        IdentifyLoop(instructionPointer);
                    }
                    //jump to the matching closing brace
                    if (tapeValues[tapePointer] == 0)
                    {
                        instructionPointer = startToEnd[instructionPointer];
                    }
                    break;
                case ']':
                    //jump to the matching opening brace
                    if (tapeValues[tapePointer] != 0)
                    {
                        instructionPointer = endToStart[instructionPointer];
                    }
                    break;
                case '.':
                    PrintToScreen();
                    break;
                default:
                    break;
            }
            instructionPointer++;

            yield return new WaitForSeconds(instructionTimeDelay);
        }

        playerCanMove = true;
    }

    private void PrintDictionary()
    {
        string contents = "";
        foreach(int key in startToEnd.Keys){
            contents += key + ": " + startToEnd[key] + "\n";
        }
        Debug.Log(contents);
    }
}
