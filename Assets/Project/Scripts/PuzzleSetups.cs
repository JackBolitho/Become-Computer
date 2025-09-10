using UnityEngine;

[CreateAssetMenu(fileName = "Puzzle", menuName = "ScriptableObjects/Puzzle", order = 1)]
public class PuzzleSetups : ScriptableObject
{
    [SerializeField] public string levelName;
    [SerializeField] public int levelIndex;
    [SerializeField] public int[] tapeValues;
    [SerializeField] public int[] goalValues;
    [SerializeField] public int maximumLengthSolution;
    [SerializeField] public string exampleSolution;
}